---
Version: 1
Created: 2026-06-01T00:00:00+00:00
Updated: 2026-06-01T00:00:00+00:00
Author: BionicCode
---
<!-- doc-metadata-presentation:start -->
<details>
<summary>Change History</summary>


</details>

---

<br>
<br>
<!-- doc-metadata-presentation:end -->

# Exclude

`exclude` lists repository-root-relative patterns removed from include participation.

The default manifest value is an empty array.

## Entry Form

| Form | Purpose |
| --- | --- |
| [`exclude pattern`](fields/exclude-pattern.md) | Removes matching paths from candidate participation. |

## Behavior Notes

- Exclude patterns apply after include patterns select candidates.
- Exclude patterns are useful for broad include cases such as `docs/**/*` with generated subdirectories removed.
- Exclude does not define metadata or presentation settings.

## Example

```json
{
  "include": ["docs/**/*"],
  "exclude": ["docs/generated/**"]
}
```

## See Also

- [Include](include.md)
- [Manifest](manifest.md)
