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

# allowExtensionless

Kind: compatibility/governance setting.

`allowExtensionless` controls whether files without an extension can be eligible.

| Property | Value |
| --- | --- |
| JSON type | boolean |
| Allowed values | `true`, `false` |
| Default behavior | `false`. |
| Controls | Extensionless-file eligibility for manifest matches. |

Files still must pass strict UTF-8 and binary checks before they can be modified.

See [documentEligibility](../document-eligibility.md).
