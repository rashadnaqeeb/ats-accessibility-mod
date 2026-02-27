# NavigationUtils.cs
Shared navigation utilities for index wrapping and other common operations.

## class NavigationUtils (line 7)

### Methods
- public static int WrapIndex(int current, int direction, int count) (line 16)
  Wraps an index within [0, count) using modulo arithmetic. Correctly handles negative direction values. Returns 0 if count <= 0.
- public static string GetDirection(int dx, int dy) (line 26)
  Returns cardinal/intercardinal direction string for a delta vector using 2:1 ratio for diagonal vs cardinal. Returns empty string if both dx and dy are zero.
