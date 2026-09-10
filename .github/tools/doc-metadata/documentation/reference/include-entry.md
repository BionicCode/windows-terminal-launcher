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

# Include Entry

An include entry object selects candidate files and may apply scoped settings.

Use object entries when one pattern needs metadata or presentation behavior different from manifest defaults.

## Fields

| Field | Required | Purpose |
| --- | --- | --- |
| [`pattern`](fields/include-pattern.md) | yes | Repository-root-relative glob pattern. |
| [`metadata`](metadata.md) | no | Scoped metadata settings inherited from `defaults.metadata`. |
| [`presentation`](presentation.md) | no | Scoped presentation settings inherited from `defaults.presentation`. |

Unknown include-entry fields fail validation.

## Behavior Notes

- Scoped metadata and presentation objects override only supplied fields.
- The effective settings for a file are computed from defaults plus the matching include entry.
- Multiple matching include entries are allowed only when their effective settings are identical.

## Example

```json
{
  "pattern": "docs/**/*.md",
  "presentation": {
    "historyLimit": 10
  }
}
```

## See Also

- [Include](include.md)
- [Defaults](defaults.md)
