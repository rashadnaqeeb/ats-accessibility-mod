using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility
{
    public static class Speech
    {
        // Static compiled regex patterns for text filtering (reused across calls)
        private static readonly Regex RichTextTagsRegex =
            new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex SpriteTagRegex =
            new Regex(@"<sprite\s+name=([^>]+)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TmpSpriteTagRegex =
            new Regex(@"\[[^\]]+\]\s*", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex =
            new Regex(@"\s+", RegexOptions.Compiled);

        // Tolk P/Invoke declarations
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output(string str, bool interrupt);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_TrySAPI(bool trySAPI);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_HasSpeech();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        // State tracking
        private static bool _initialized = false;
        private static bool _available = false;

        public static bool IsInitialized => _initialized;
        public static bool IsAvailable => _available;

        /// <summary>
        /// Initialize Tolk. Must be called after SetDllDirectory in Plugin.Awake().
        /// </summary>
        public static bool Initialize()
        {
            if (_initialized) return _available;

            try
            {
                Tolk_Load();
                Tolk_TrySAPI(true); // Enable SAPI fallback for users without screen readers

                _available = Tolk_HasSpeech();
                _initialized = true;

                // Log which screen reader was detected
                IntPtr readerPtr = Tolk_DetectScreenReader();
                string reader = readerPtr != IntPtr.Zero
                    ? Marshal.PtrToStringUni(readerPtr)
                    : "SAPI (fallback)";
                Debug.Log($"[ATSAccessibility] Speech initialized with: {reader}");

                return _available;
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError($"[ATSAccessibility] Tolk.dll not found: {ex.Message}");
                _initialized = true;
                _available = false;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Speech init failed: {ex.Message}");
                _initialized = true;
                _available = false;
                return false;
            }
        }

        /// <summary>
        /// Shutdown Tolk and release resources.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            try
            {
                Tolk_Unload();
                Debug.Log("[ATSAccessibility] Speech shutdown");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Speech shutdown error: {ex.Message}");
            }
            finally
            {
                _initialized = false;
                _available = false;
            }
        }

        /// <summary>
        /// Filter out rich text tags from speech output.
        /// Converts meaningful sprites (like star icons) to text, then removes remaining tags.
        /// </summary>
        private static string FilterRichText(string text)
        {
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
        private static string ConvertSpriteTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Pattern: <sprite name=...>
            // Grade sprites use asset names like "grade0", "grade1", "grade2", "grade3"
            return SpriteTagRegex.Replace(
                text,
                match =>
                {
                    string spriteName = match.Groups[1].Value.Trim().ToLowerInvariant();

                    // Check for grade sprites by name pattern
                    // Primary format: "grade0", "grade1", "grade2", "grade3"
                    if (spriteName == "grade0" || spriteName.EndsWith("grade0")) return "0 star";
                    if (spriteName == "grade1" || spriteName.EndsWith("grade1")) return "1 star";
                    if (spriteName == "grade2" || spriteName.EndsWith("grade2")) return "2 star";
                    if (spriteName == "grade3" || spriteName.EndsWith("grade3")) return "3 star";

                    // Fallback patterns: "0 star", "1 star", etc. or just numbers
                    if (spriteName == "0 star" || spriteName == "0") return "0 star";
                    if (spriteName == "1 star" || spriteName == "1") return "1 star";
                    if (spriteName == "2 star" || spriteName == "2") return "2 star";
                    if (spriteName == "3 star" || spriteName == "3") return "3 star";

                    // For other sprites, just remove them
                    return "";
                }
            );
        }

        /// <summary>
        /// Clean resource names by stripping verbose prefixes.
        /// E.g., "Wood Node Moss Broccoli - Small" → "Moss Broccoli - Small"
        /// </summary>
        public static string CleanResourceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Strip "XXX Node " prefix (e.g., "Wood Node ", "Universal Node ")
            int nodeIndex = name.IndexOf(" Node ");
            if (nodeIndex >= 0)
            {
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
        public static void Say(string message, bool interrupt = true)
        {
            if (!_available || string.IsNullOrEmpty(message)) return;

            try
            {
                string filtered = FilterRichText(message);
                if (!string.IsNullOrEmpty(filtered))
                {
                    Tolk_Output(filtered, interrupt);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Speech error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop any current speech.
        /// </summary>
        public static void Stop()
        {
            if (!_available) return;

            try
            {
                // Output empty string with interrupt to stop speech
                Tolk_Output("", true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Speech stop error: {ex.Message}");
            }
        }
    }
}
