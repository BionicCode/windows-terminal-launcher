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

# createdField

Kind: metadata output setting.

`createdField` names the managed metadata initialization timestamp field.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1; effective value must not be blank. |
| Default behavior | The default manifest uses `Created`. |
| Controls | The field name for the timestamp written when metadata is initialized. |

For migrated existing files, this timestamp represents metadata initialization time unless a separate history-inference feature is added.

See [metadata](../metadata.md).
