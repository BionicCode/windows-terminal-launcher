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

# Include

`include` lists candidate files for document metadata governance.

Each entry is either an [`include pattern`](fields/include-pattern.md) string or an [`include entry`](include-entry.md) object with scoped settings.

## Entry Forms

| Form | Purpose |
| --- | --- |
| [`include pattern`](fields/include-pattern.md) | Selects candidate files that use manifest defaults. |
| [`include entry`](include-entry.md) | Selects candidate files and optionally overrides metadata or presentation settings. |

## Behavior Notes

- Include entries are repository-root-relative.
- A file must match an include entry before it can be considered by the tool.
- If multiple include entries match the same file, their effective settings must be identical or validation fails.
- `exclude` can remove files from include participation.
- `documentEligibility` currently filters matched candidates by document safety rules.

## See Also

- [Exclude](exclude.md)
- [Manifest](manifest.md)
