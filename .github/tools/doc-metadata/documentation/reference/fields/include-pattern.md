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

# include pattern

Kind: selector.

An include pattern selects candidate files from the repository.

| Property | Value |
| --- | --- |
| JSON type | string |
| Constraints | Minimum length 1. Use repository-root-relative forward-slash paths or glob patterns. |
| Default behavior | No implicit include patterns; the manifest must list them. |
| Controls | Candidate file selection before exclude and eligibility filtering. |

Use `**` for nested path matching. A string include entry uses manifest defaults; an include object uses `pattern` plus scoped settings.

See [include](../include.md) and [include entry](../include-entry.md).
