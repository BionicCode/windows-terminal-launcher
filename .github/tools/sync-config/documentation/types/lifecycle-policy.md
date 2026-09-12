---
Version: 1
Created: 2026-05-28T20:29:29+00:00
Updated: 2026-05-28T20:29:29+00:00
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

# LifecyclePolicy

Declares how the workflow treats missing, existing, and changed targets.

## Placement

| Property | Value |
|---|---|
| Placement | Enum string |
| Valid parent | [`ManifestEntry`](manifest-entry.md) |
| Parent property | `lifecycle_policy` |

## Values

| Value | Supported | Pull request behavior | Branch maintenance behavior |
|---|---:|---|---|
| `enforce` | Yes | Fail if the target is missing or differs from source. | Create or update the target from source when missing or changed. |
| `seed_once` | Yes | Fail only if the target is missing. Existing target content may differ from source. | Create the target from source only when missing; never overwrite an existing target. |
| `disabled` | Yes | Skip fetch, compare, and verification. | Skip fetch, write, and PR staging. |

## Notes

`disabled` entries remain schema-valid and still participate in whole-manifest validation such as duplicate target detection.
