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

# versioningMode

Kind: metadata output setting.

`versioningMode` selects the version increment policy.

| Property | Value |
| --- | --- |
| JSON type | string |
| Allowed values | `body-content-change` |
| Default behavior | The default manifest uses `body-content-change`. |
| Controls | When the managed document revision is incremented. |

The only implemented policy increments on governed body-content changes. Other values fail validation.

See [metadata](../metadata.md).
