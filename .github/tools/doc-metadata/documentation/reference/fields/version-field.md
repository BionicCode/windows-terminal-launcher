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

# versionField

Kind: metadata output setting.

`versionField` names the managed document revision field.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1; effective value must not be blank. |
| Default behavior | The default manifest uses `Version`. |
| Controls | The field name where the tool stores the human-facing document revision. |

The value stored in this field may be a positive integer or a dotted numeric value. Automatic increments update the first component only.

See [metadata](../metadata.md).
