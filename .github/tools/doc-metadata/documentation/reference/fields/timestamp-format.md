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

# timestampFormat

Kind: metadata output setting.

`timestampFormat` selects the generated timestamp format.

| Property | Value |
| --- | --- |
| JSON type | string |
| Allowed values | `rfc3339-utc` |
| Default behavior | The default manifest uses `rfc3339-utc`. |
| Controls | How generated `Created` and `Updated` values are formatted. |

The implemented format writes UTC timestamps with an explicit `+00:00` offset. Other values fail validation.

See [metadata](../metadata.md).
