# Changes since v1.1.0

## Bug fixes
- Type-ahead search no longer rolls back characters when no matches found; keeps full search term

## Internal
- Use StringBuilder in TypeAheadSearch to reduce GC allocations during search
