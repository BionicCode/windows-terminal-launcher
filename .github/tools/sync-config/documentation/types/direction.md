---
Version: 1
Created: 2026-05-28T19:51:22+00:00
Updated: 2026-05-28T19:51:22+00:00
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

# Direction

Declares the synchronization direction for a manifest entry.

## Placement

| Property | Value |
|---|---|
| Placement | Enum string |
| Valid parent | [`ManifestEntry`](manifest-entry.md) |
| Parent property | `direction` |

## Values

| Value | Supported | Description |
|---|---:|---|
| `source_to_target` | Yes | Fetch source content and verify or write the caller repository target. |
| `target_to_source` | No | Accepted by the manifest schema and rejected by current execution rules before source fetch or write. |
| `two_way` | No | Accepted by the manifest schema and rejected by current execution rules before source fetch or write. |

## Notes

The current workflow never writes back to source repositories.
