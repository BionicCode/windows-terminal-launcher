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

# failOnIneligibleMatches

Kind: compatibility/governance setting.

`failOnIneligibleMatches` controls whether ineligible manifest matches are reported or fail validation.

| Property | Value |
| --- | --- |
| JSON type | boolean |
| Allowed values | `true`, `false` |
| Default behavior | `false`. |
| Controls | Error behavior for manifest matches filtered by document eligibility. |

When false, ineligible matches are reported and not modified. When true, they become validation failures.

See [documentEligibility](../document-eligibility.md).
