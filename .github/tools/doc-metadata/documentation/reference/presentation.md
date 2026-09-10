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

# Presentation

`presentation` controls generated human-facing metadata presentation around the managed document header.

It does not change which files are selected. Selection is controlled by `include`, `exclude`, and the current compatibility eligibility filter.

## Fields

| Field | Default manifest value | Purpose |
| --- | --- | --- |
| [`enabled`](fields/enabled.md) | `true` | Enables the rich managed presentation region when supported. |
| [`historyLimit`](fields/history-limit.md) | `20` | Limits embedded history entries. |
| [`includeSeparator`](fields/include-separator.md) | `true` | Adds a generated separator before the document body. |
| [`spacingBreaks`](fields/spacing-breaks.md) | `2` | Controls generated spacing after the presentation or separator. |

Unknown presentation fields fail validation.

## Behavior Notes

- Markdown files can receive a rich managed presentation region.
- Plain text files use compact metadata by default and never receive Markdown or HTML presentation.
- Scoped include entries can override presentation settings for a matching pattern.

## See Also

- [Defaults](defaults.md)
- [Include entry](include-entry.md)
