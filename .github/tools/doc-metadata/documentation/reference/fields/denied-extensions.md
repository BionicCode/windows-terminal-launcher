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

# deniedExtensions

Kind: compatibility/governance setting.

`deniedExtensions` defines extensions that are never eligible.

| Property | Value |
| --- | --- |
| JSON type | array of strings |
| Constraints | Extension values normalize to leading-dot lowercase and cannot contain wildcards or path separators. |
| Default behavior | Empty array. |
| Controls | Extension denial after allowed extensions are computed. |

Denied extensions win over `allowedExtensions` and `additionalAllowedExtensions`.

See [documentEligibility](../document-eligibility.md).
