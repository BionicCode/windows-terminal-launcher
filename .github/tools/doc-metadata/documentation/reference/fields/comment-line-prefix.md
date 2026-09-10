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

# commentLinePrefix

Kind: metadata output setting.

`commentLinePrefix` defines an optional prefix written before metadata lines inside a comment block.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | May be an empty string. |
| Default behavior | No prefix. |
| Controls | Line formatting inside `comment-block` metadata. |

The tool removes the configured prefix while parsing comment-block metadata and writes it while generating metadata lines.

See [metadata](../metadata.md).
