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

# Defaults

`defaults` defines the metadata and presentation settings inherited by manifest include entries.

String include entries use these values directly. Object include entries inherit these values and may override metadata or presentation settings for that pattern.

## Fields

| Field | Required | Purpose |
| --- | --- | --- |
| [`metadata`](metadata.md) | yes | Default managed metadata block settings. |
| [`presentation`](presentation.md) | yes | Default generated presentation settings. |

Unknown `defaults` fields fail validation.

## Behavior Notes

- Defaults are applied before scoped include overrides.
- File-format conventions are applied by the script after defaults are read. Markdown files support rich presentation. Plain text files use compact output by default.
- Conflicting include entries that match the same file must resolve to identical effective settings.

## See Also

- [Manifest](manifest.md)
- [Include entry](include-entry.md)
