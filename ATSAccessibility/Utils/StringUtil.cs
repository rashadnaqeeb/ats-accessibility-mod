using System.Globalization;
using System.Text;

namespace ATSAccessibility.Utils {
	public static class StringUtil {
		/// <summary>
		/// Strips diacritics and expands ligatures so accented and composed
		/// characters match their plain Latin equivalents (e-acute to e,
		/// c-cedilla to c, oe-ligature to oe, etc.). Used by type-ahead
		/// search to make queries accent-insensitive.
		/// </summary>
		public static string RemoveDiacritics(string text) {
			if (string.IsNullOrEmpty(text)) return text;

			var decomposed = text.Normalize(NormalizationForm.FormD);
			var sb = new StringBuilder(decomposed.Length);
			for (int i = 0; i < decomposed.Length; i++) {
				char c = decomposed[i];
				switch (c) {
					case 'œ': sb.Append("oe"); break;
					case 'Œ': sb.Append("oe"); break;
					case 'æ': sb.Append("ae"); break;
					case 'Æ': sb.Append("ae"); break;
					case 'ß': sb.Append("ss"); break;
					default:
						if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
							sb.Append(c);
						break;
				}
			}
			return sb.ToString();
		}
	}
}
