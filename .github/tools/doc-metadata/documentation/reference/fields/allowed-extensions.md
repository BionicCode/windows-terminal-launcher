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

# allowedExtensions

Kind: compatibility/governance setting.

`allowedExtensions` defines the base set of eligible document extensions for manifest matches.

| Property | Value |
| --- | --- |
| JSON type | array of strings |
| Constraints | Extension values normalize to leading-dot lowercase and cannot contain wildcards or path separators. |
| Default behavior | If omitted, runtime defaults are `.md`, `.markdown`, and `.txt`. If present, this replaces that base set. |
| Controls | Which manifest matches are eligible document files. |

Use [`additionalAllowedExtensions`](additional-allowed-extensions.md) when the default base set should be extended rather than replaced.

See [documentEligibility](../document-eligibility.md).
