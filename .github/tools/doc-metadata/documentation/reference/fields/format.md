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

# format

Kind: metadata output setting.

`format` selects the managed metadata block format.

| Property | Value |
| --- | --- |
| JSON type | string |
| Allowed values | `yaml-front-matter`, `comment-block` |
| Default behavior | The default manifest uses `yaml-front-matter`; scoped include entries inherit the effective default. |
| Controls | How the managed metadata header is written and parsed. |

`comment-block` metadata requires [`commentStart`](comment-start.md) and [`commentEnd`](comment-end.md). YAML front matter supports [`placement`](placement.md) `top` only.

See [metadata](../metadata.md).
