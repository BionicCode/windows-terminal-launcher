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

# commentEnd

Kind: metadata output setting.

`commentEnd` defines the closing marker for `comment-block` metadata.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1; required when effective `format` is `comment-block`. |
| Default behavior | No default in the manifest. For plain text comment-block output, the script can supply an internal marker if none is set. |
| Controls | How the tool detects and writes the end of a comment metadata block. |

For explicit `comment-block` metadata, provide both [`commentStart`](comment-start.md) and `commentEnd`.

See [metadata](../metadata.md).
