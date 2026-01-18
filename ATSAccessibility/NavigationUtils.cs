namespace ATSAccessibility
{
    /// <summary>
    /// Shared navigation utilities for index wrapping and other common operations.
    /// </summary>
    public static class NavigationUtils
    {
        /// <summary>
        /// Wraps an index within bounds using modulo arithmetic.
        /// Handles negative direction values correctly.
        /// </summary>
        /// <param name="current">Current index</param>
        /// <param name="direction">Direction to move (-1 for prev, +1 for next)</param>
        /// <param name="count">Total number of items (must be > 0)</param>
        /// <returns>Wrapped index within [0, count)</returns>
        public static int WrapIndex(int current, int direction, int count)
        {
            if (count <= 0) return 0;
            return (current + direction % count + count) % count;
        }
    }
}
