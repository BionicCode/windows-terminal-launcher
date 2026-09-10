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

# commentStart

Kind: metadata output setting.

`commentStart` defines the opening marker for `comment-block` metadata.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1; required when effective `format` is `comment-block`. |
| Default behavior | No default in the manifest. For plain text comment-block output, the script can supply an internal marker if none is set. |
| Controls | How the tool detects and writes the start of a comment metadata block. |

For explicit `comment-block` metadata, provide both `commentStart` and [`commentEnd`](comment-end.md).

See [metadata](../metadata.md).
