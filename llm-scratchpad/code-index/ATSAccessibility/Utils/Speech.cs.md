# Speech.cs

## class Speech (line 7)

### Fields
- private static readonly Regex RichTextTagsRegex (line 9)
- private static readonly Regex SpriteTagRegex (line 11)
- private static readonly Regex TmpSpriteTagRegex (line 13)
  Matches TextMeshPro shorthand bracket tags like [Food Raw], [Water].
- private static readonly Regex WhitespaceRegex (line 15)
- private static bool _initialized (line 38)
- private static bool _available (line 39)

### Properties
- public static bool IsInitialized { get; } (line 41)
- public static bool IsAvailable { get; } (line 42)

### Methods (Tolk P/Invoke)
- private static extern void Tolk_Load() (line 20)
- private static extern void Tolk_Unload() (line 23)
- private static extern bool Tolk_Output(string str, bool interrupt) (line 25)
- private static extern bool Tolk_TrySAPI(bool trySAPI) (line 28)
- private static extern bool Tolk_HasSpeech() (line 31)
- private static extern IntPtr Tolk_DetectScreenReader() (line 34)

### Methods
- public static bool Initialize() (line 47)
  Loads Tolk, enables SAPI fallback, detects screen reader. Must be called after SetDllDirectory in Plugin.Awake().
- public static void Shutdown() (line 81)
- private static string FilterRichText(string text) (line 99)
  Converts sprite tags to text first (grade sprites become "0 star" etc.), then strips remaining HTML tags, TMP bracket tags, and normalizes whitespace.
- private static string ConvertSpriteTags(string text) (line 126)
  Converts grade sprite names ("grade0"-"grade3") to "0 star"-"3 star". Other sprites are removed.
- public static string CleanResourceName(string name) (line 159)
  Strips "XXX Node " prefix from resource names (e.g. "Wood Node Moss Broccoli - Small" -> "Moss Broccoli - Small").
- public static void Say(string message, bool interrupt = true) (line 177)
  Filters rich text then calls Tolk_Output. No-ops if not available or message is empty.
- public static void Stop() (line 193)
  Interrupts current speech by outputting an empty string with interrupt=true.
