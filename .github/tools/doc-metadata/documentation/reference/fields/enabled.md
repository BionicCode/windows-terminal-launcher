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

# enabled

Kind: presentation setting.

`enabled` controls whether the rich managed presentation region is generated when the file format supports it.

| Property | Value |
| --- | --- |
| JSON type | boolean |
| Allowed values | `true`, `false` |
| Default behavior | The default manifest uses `true`; plain text files use compact output by default. |
| Controls | Rich presentation generation, not file selection. |

Disabling presentation does not make a file ungoverned. It only changes generated presentation output.

See [presentation](../presentation.md).
