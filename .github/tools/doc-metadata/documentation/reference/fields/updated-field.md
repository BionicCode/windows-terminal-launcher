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

# updatedField

Kind: metadata output setting.

`updatedField` names the managed body-content-change timestamp field.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1; effective value must not be blank. |
| Default behavior | The default manifest uses `Updated`. |
| Controls | The field name for the timestamp refreshed when governed body content changes. |

Metadata-only repairs should not create a new body-change timestamp.

See [metadata](../metadata.md).
