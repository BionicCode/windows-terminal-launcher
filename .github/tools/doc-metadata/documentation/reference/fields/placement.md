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

# placement

Kind: metadata output setting.

`placement` selects where the managed metadata block is placed.

| Property | Value |
| --- | --- |
| JSON type | string |
| Allowed values | `top`, `bottom` |
| Default behavior | The default manifest uses `top`; scoped include entries inherit the effective default. |
| Controls | Metadata block placement in the governed file. |

`yaml-front-matter` cannot use `bottom`; that combination fails validation. `comment-block` metadata may use `top` or `bottom`.

See [metadata](../metadata.md).
