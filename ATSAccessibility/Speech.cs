using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility
{
    public static class Speech
    {
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
        /// Removes Unity rich text like <color>, <b>, <i>, <size>, <sprite>, etc.
        /// </summary>
        private static string FilterRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove all rich text tags (anything between < and >)
            text = Regex.Replace(text, "<[^>]+>", "");

            // Normalize whitespace (collapse multiple spaces/newlines)
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
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
