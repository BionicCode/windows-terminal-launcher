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

# historyLimit

Kind: presentation setting.

`historyLimit` limits generated embedded Change History entries.

| Property | Value |
| --- | --- |
| JSON type | integer or null |
| Constraints | Integer values must be 0 or greater. |
| Default behavior | The default manifest uses `20`; plain text defaults to `0`. |
| Controls | How many generated history entries are kept in the rich presentation region. |

`0` suppresses embedded history entries. `null` is accepted by the schema and the implementation treats it as no trimming limit.

See [presentation](../presentation.md).
