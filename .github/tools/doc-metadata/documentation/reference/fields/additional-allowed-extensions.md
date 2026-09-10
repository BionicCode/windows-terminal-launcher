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

# additionalAllowedExtensions

Kind: compatibility/governance setting.

`additionalAllowedExtensions` appends extra eligible document extensions.

| Property | Value |
| --- | --- |
| JSON type | array of strings |
| Constraints | Extension values normalize to leading-dot lowercase and cannot contain wildcards or path separators. |
| Default behavior | Empty array. |
| Controls | Additional extension eligibility after the base allowed set is built. |

Denied extensions still win even when an extension is also allowed.

See [documentEligibility](../document-eligibility.md).
