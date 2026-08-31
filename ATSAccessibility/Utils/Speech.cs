using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility.Utils {
	public static class Speech {
		// Static compiled regex patterns for text filtering (reused across calls)
		private static readonly Regex RichTextTagsRegex =
			new Regex("<[^>]+>", RegexOptions.Compiled);
		private static readonly Regex SpriteTagRegex =
			new Regex(@"<sprite\s+name=([^>]+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex TmpSpriteTagRegex =
			new Regex(@"\[[^\]]+\]\s*", RegexOptions.Compiled);
		private static readonly Regex WhitespaceRegex =
			new Regex(@"\s+", RegexOptions.Compiled);

		// Prism P/Invoke declarations. The library name "prism" resolves to prism.dll on Windows.
		// Vendored prism.dll is v0.18.2. prism_init accepts NULL config (uses defaults), which
		// avoids declaring PrismConfig here — its layout changes between Prism versions and a
		// mismatched struct silently corrupts memory. Prism strings are UTF-8, never ANSI:
		// text must cross as UTF-8 bytes or non-ASCII input is rejected as PRISM_ERROR_INVALID_UTF8.
		private const int PRISM_OK = 0;
		private const int PRISM_ERROR_NOT_SPEAKING = 10;

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_init(IntPtr cfg);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern void prism_shutdown(IntPtr ctx);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_registry_acquire_best(IntPtr ctx);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_backend_name(IntPtr backend);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern int prism_backend_speak(IntPtr backend, byte[] textUtf8, [MarshalAs(UnmanagedType.I1)] bool interrupt);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern int prism_backend_stop(IntPtr backend);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern void prism_backend_free(IntPtr backend);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_error_string(int error);

		[DllImport("prism", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr prism_version_string();

		/// <summary>
		/// Encode a string as null-terminated UTF-8 bytes for Prism.
		/// </summary>
		private static byte[] ToUtf8(string text) {
			byte[] bytes = new byte[System.Text.Encoding.UTF8.GetByteCount(text) + 1];
			System.Text.Encoding.UTF8.GetBytes(text, 0, text.Length, bytes, 0);
			return bytes;
		}

		/// <summary>
		/// Read a null-terminated UTF-8 string returned by Prism.
		/// (Marshal.PtrToStringUTF8 is unavailable on net472.)
		/// </summary>
		private static string FromUtf8(IntPtr ptr) {
			if (ptr == IntPtr.Zero) return null;
			int len = 0;
			while (Marshal.ReadByte(ptr, len) != 0) len++;
			if (len == 0) return string.Empty;
			byte[] bytes = new byte[len];
			Marshal.Copy(ptr, bytes, 0, len);
			return System.Text.Encoding.UTF8.GetString(bytes);
		}

		// State tracking
		private static IntPtr _context = IntPtr.Zero;
		private static IntPtr _backend = IntPtr.Zero;
		private static bool _initialized = false;
		private static bool _available = false;

		public static bool IsInitialized => _initialized;
		public static bool IsAvailable => _available;

		/// <summary>
		/// Initialize Prism. Must be called after SetDllDirectory in Plugin.Awake().
		/// </summary>
		public static bool Initialize() {
			if (_initialized) return _available;

			try {
				// NULL config = library defaults; see comment on the P/Invoke block.
				_context = prism_init(IntPtr.Zero);
				if (_context == IntPtr.Zero) {
					Debug.LogError("[ATSAccessibility] prism_init returned null");
					_initialized = true;
					_available = false;
					return false;
				}

				_backend = prism_registry_acquire_best(_context);
				_available = _backend != IntPtr.Zero;
				_initialized = true;

				if (_available) {
					// Cosmetic only, in its own try/catch: a stale prism.dll can
					// predate an export used here (EntryPointNotFoundException),
					// and a decorative log line must never discard a working
					// backend by falling into the outer catch.
					try {
						string name = FromUtf8(prism_backend_name(_backend)) ?? "unknown";
						string version = FromUtf8(prism_version_string()) ?? "unknown";
						Debug.Log($"[ATSAccessibility] Speech initialized with: {name} (Prism {version})");
					} catch (Exception ex) {
						Debug.LogWarning($"[ATSAccessibility] Speech initialized; backend/version info unavailable: {ex.Message}");
					}
				} else {
					Debug.LogWarning("[ATSAccessibility] No Prism speech backend available");
				}

				return _available;
			} catch (DllNotFoundException ex) {
				Debug.LogError($"[ATSAccessibility] prism.dll not found: {ex.Message}");
				_initialized = true;
				_available = false;
				return false;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Speech init failed: {ex.Message}");
				_initialized = true;
				_available = false;
				return false;
			}
		}

		/// <summary>
		/// Shutdown Prism and release resources.
		/// </summary>
		public static void Shutdown() {
			if (!_initialized) return;

			try {
				if (_backend != IntPtr.Zero) {
					prism_backend_free(_backend);
					_backend = IntPtr.Zero;
				}
				if (_context != IntPtr.Zero) {
					prism_shutdown(_context);
					_context = IntPtr.Zero;
				}
				Debug.Log("[ATSAccessibility] Speech shutdown");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Speech shutdown error: {ex.Message}");
			} finally {
				_initialized = false;
				_available = false;
			}
		}

		/// <summary>
		/// Filter out rich text tags from speech output.
		/// Converts meaningful sprites (like star icons) to text, then removes remaining tags.
		/// </summary>
		private static string FilterRichText(string text) {
			if (string.IsNullOrEmpty(text)) return text;

			// Convert sprite tags to readable text BEFORE removing tags
			// (e.g., recipe grade stars become "1 star", "2 star", etc.)
			text = ConvertSpriteTags(text);

			// Remove all remaining rich text tags (anything between < and >)
			text = RichTextTagsRegex.Replace(text, "");

			// Remove TextMeshPro shorthand sprite tags like [Food Raw], [Water], etc.
			text = TmpSpriteTagRegex.Replace(text, "");

			// Remove empty brackets/parentheses left behind by sprite tags
			text = text.Replace("[]", "");
			text = text.Replace("()", "");

			// Normalize whitespace (collapse multiple spaces/newlines)
			text = WhitespaceRegex.Replace(text, " ");

			return text.Trim();
		}

		/// <summary>
		/// Convert sprite tags to readable text.
		/// E.g., recipe grade sprites become "0 star", "1 star", etc.
		/// </summary>
		private static string ConvertSpriteTags(string text) {
			if (string.IsNullOrEmpty(text)) return text;

			// Pattern: <sprite name=...>
			// Grade sprites use asset names like "grade0", "grade1", "grade2", "grade3"
			return SpriteTagRegex.Replace(
				text,
				match => {
					string spriteName = match.Groups[1].Value.Trim().ToLowerInvariant();

					// Check for grade sprites by name pattern
					// Primary format: "grade0", "grade1", "grade2", "grade3"
					if (spriteName == "grade0" || spriteName.EndsWith("grade0")) return Strings.Get("common.sprite.grade_0");
					if (spriteName == "grade1" || spriteName.EndsWith("grade1")) return Strings.Get("common.sprite.grade_1");
					if (spriteName == "grade2" || spriteName.EndsWith("grade2")) return Strings.Get("common.sprite.grade_2");
					if (spriteName == "grade3" || spriteName.EndsWith("grade3")) return Strings.Get("common.sprite.grade_3");

					// Fallback patterns: "0 star", "1 star", etc. or just numbers
					if (spriteName == "0 star" || spriteName == "0") return Strings.Get("common.sprite.grade_0");
					if (spriteName == "1 star" || spriteName == "1") return Strings.Get("common.sprite.grade_1");
					if (spriteName == "2 star" || spriteName == "2") return Strings.Get("common.sprite.grade_2");
					if (spriteName == "3 star" || spriteName == "3") return Strings.Get("common.sprite.grade_3");

					// For other sprites, just remove them
					return "";
				}
			);
		}

		/// <summary>
		/// Clean resource names by stripping verbose prefixes.
		/// E.g., "Wood Node Moss Broccoli - Small" → "Moss Broccoli - Small"
		/// </summary>
		public static string CleanResourceName(string name) {
			if (string.IsNullOrEmpty(name)) return name;

			// Strip "XXX Node " prefix (e.g., "Wood Node ", "Universal Node ")
			int nodeIndex = name.IndexOf(" Node ");
			if (nodeIndex >= 0) {
				return name.Substring(nodeIndex + 6); // 6 = " Node ".Length
			}

			return name;
		}

		/// <summary>
		/// Speak the given message.
		/// Rich text tags are automatically stripped before speaking.
		/// </summary>
		/// <param name="message">Text to speak</param>
		/// <param name="interrupt">If true, interrupts any current speech</param>
		public static void Say(string message, bool interrupt = true) {
			if (!_available || string.IsNullOrEmpty(message)) return;

			try {
				string filtered = FilterRichText(message);
				if (string.IsNullOrEmpty(filtered)) return;

				int err = prism_backend_speak(_backend, ToUtf8(filtered), interrupt);
				if (err != PRISM_OK) {
					Debug.LogWarning($"[ATSAccessibility] Speech error: {DescribePrismError(err)}");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Speech error: {ex.Message}");
			}
		}

		/// <summary>
		/// Stop any current speech.
		/// </summary>
		public static void Stop() {
			if (!_available) return;

			try {
				int err = prism_backend_stop(_backend);
				if (err != PRISM_OK && err != PRISM_ERROR_NOT_SPEAKING) {
					Debug.LogWarning($"[ATSAccessibility] Speech stop error: {DescribePrismError(err)}");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Speech stop error: {ex.Message}");
			}
		}

		private static string DescribePrismError(int err) {
			return FromUtf8(prism_error_string(err)) ?? $"error code {err}";
		}
	}
}
