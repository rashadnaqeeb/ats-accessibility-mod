using System;
using System.Text;

namespace ATSAccessibility {
	/// <summary>
	/// Shared formatting helpers previously duplicated across reflection files.
	/// </summary>
	public static class FormattingUtils {
		/// <summary>
		/// Format time in seconds to mm:ss or h:mm:ss string.
		/// </summary>
		public static string FormatTime(float seconds) {
			if (seconds <= 0) return "0:00";

			var ts = TimeSpan.FromSeconds(seconds);
			if (ts.TotalHours >= 1)
				return string.Format("{0}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
			return string.Format("{0}:{1:D2}", (int)ts.TotalMinutes, ts.Seconds);
		}

		/// <summary>
		/// Convert year number to Roman numeral string.
		/// </summary>
		public static string YearToRoman(int year) {
			if (year <= 0) return year.ToString();

			var result = new StringBuilder();
			int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
			string[] numerals = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

			for (int i = 0; i < values.Length; i++) {
				while (year >= values[i]) {
					result.Append(numerals[i]);
					year -= values[i];
				}
			}
			return result.ToString();
		}
	}
}
