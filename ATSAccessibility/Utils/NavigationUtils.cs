using System;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Shared navigation utilities for index wrapping and other common operations.
	/// </summary>
	public static class NavigationUtils {
		/// <summary>
		/// Wraps an index within bounds using modulo arithmetic.
		/// Handles negative direction values correctly.
		/// </summary>
		/// <param name="current">Current index</param>
		/// <param name="direction">Direction to move (-1 for prev, +1 for next)</param>
		/// <param name="count">Total number of items (must be > 0)</param>
		/// <returns>Wrapped index within [0, count)</returns>
		public static int WrapIndex(int current, int direction, int count) {
			if (count <= 0) return 0;
			return ((current + direction) % count + count) % count;
		}

		/// <summary>
		/// Returns a cardinal/intercardinal direction string for a delta vector.
		/// Uses a 2:1 ratio to decide between cardinal and diagonal directions.
		/// Returns empty string if dx and dy are both zero.
		/// </summary>
		public static string GetDirection(int dx, int dy) {
			if (dx == 0 && dy == 0) return "";

			int absDx = Math.Abs(dx);
			int absDy = Math.Abs(dy);

			// Only use diagonal if both axes are significant (within 2:1 ratio)
			bool useNS = absDy > 0 && absDy * 2 >= absDx;
			bool useEW = absDx > 0 && absDx * 2 >= absDy;

			string ns = useNS ? (dy > 0 ? "north" : "south") : "";
			string ew = useEW ? (dx > 0 ? "east" : "west") : "";

			if (string.IsNullOrEmpty(ns)) return ew;
			if (string.IsNullOrEmpty(ew)) return ns;
			return ns + ew;
		}
	}
}
