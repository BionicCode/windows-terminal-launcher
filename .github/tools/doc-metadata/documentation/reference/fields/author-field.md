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

# authorField

Kind: metadata output setting.

`authorField` names the managed content-change author field.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1; effective value must not be blank. |
| Default behavior | The default manifest uses `Author`. |
| Controls | The field name where the tool writes the detected content-change author. |

Local runs prefer `git config user.name`. Hosted repair uses comparison context to avoid attributing document content changes to metadata repair automation when possible.

See [metadata](../metadata.md).
