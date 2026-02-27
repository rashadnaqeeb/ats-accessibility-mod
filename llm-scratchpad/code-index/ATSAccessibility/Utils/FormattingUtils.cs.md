# FormattingUtils.cs
Shared formatting helpers previously duplicated across reflection files.

## class FormattingUtils (line 8)

### Methods
- public static string FormatTime(float seconds) (line 12)
  Formats seconds to "m:ss" or "h:mm:ss". Returns "0:00" for invalid/zero values.
- public static string YearToRoman(int year) (line 24)
  Converts integer year to Roman numeral string (e.g., 3 -> "III"). Returns year.ToString() for non-positive values.
