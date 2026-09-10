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

# exclude pattern

Kind: selector.

An exclude pattern removes files from include participation.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1. Use repository-root-relative forward-slash paths or glob patterns. |
| Default behavior | The default manifest uses an empty `exclude` array. |
| Controls | Candidate removal after include matching. |

Exclude patterns do not configure metadata or presentation output.

See [exclude](../exclude.md).
