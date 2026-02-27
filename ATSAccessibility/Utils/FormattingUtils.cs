using System;
using System.Text;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Shared formatting helpers previously duplicated across reflection files.
	/// </summary>
	public static class FormattingUtils {
		/// <summary>
		/// Format time in seconds to mm:ss or h:mm:ss string.
		/// </summary>
		public static string FormatTime(float seconds) {
			if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return "0:00";

			var ts = TimeSpan.FromSeconds(seconds);
			if (ts.TotalHours >= 1)
				return string.Format("{0}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
			return string.Format("{0}:{1:D2}", (int)ts.TotalMinutes, ts.Seconds);
		}

		/// <summary>
		/// Format remaining time as a human-readable string (e.g. "2 minutes 30 seconds remaining").
		/// </summary>
		public static string FormatTimeRemaining(float timeLeft) {
			int seconds = Mathf.RoundToInt(timeLeft);
			if (seconds <= 0)
				return "almost done";
			if (seconds < 60)
				return $"{seconds} seconds remaining";
			int minutes = seconds / 60;
			int remainingSecs = seconds % 60;
			if (remainingSecs > 0)
				return $"{minutes} minutes {remainingSecs} seconds remaining";
			return $"{minutes} minutes remaining";
		}

		/// <summary>
		/// Clean up internal recipe names for display (e.g. "_Recipe_Planks" → ": Planks").
		/// </summary>
		public static string CleanupRecipeName(string name) {
			if (string.IsNullOrEmpty(name)) return name;

			string display = name;
			display = display.Replace("_Recipe_", ": ");
			display = display.Replace("Recipe_", "");
			display = display.Replace("[Mat Processed]", "");
			display = display.Replace("[Mat Raw]", "");
			display = display.Replace("_", " ");

			return display.Trim();
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
