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

# deniedPaths

Kind: compatibility/governance setting.

`deniedPaths` defines repository-relative path or glob patterns that are never eligible.

| Property | Value |
| --- | --- |
| JSON type | array of strings |
| Constraints | Safe repository-relative forward-slash paths or glob patterns; absolute paths, Windows separators, and `..` traversal are invalid. |
| Default behavior | Empty array. |
| Controls | Path denial for files that already matched the manifest. |

Use `exclude` for candidate removal. Use `deniedPaths` only for the current compatibility eligibility filter.

See [documentEligibility](../document-eligibility.md).
