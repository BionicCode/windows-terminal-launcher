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

# ManagedScope

Declares which portion of the target file is managed by the workflow.

## Placement

| Property | Value |
|---|---|
| Placement | Enum string |
| Valid parent | [`ManifestEntry`](manifest-entry.md) |
| Parent property | `managed_scope` |

## Values

| Value | Supported | Markers | Description |
|---|---:|---|---|
| `whole_file` | Yes | Forbidden | Source owns the entire target file. Sync is byte-for-byte and is suitable for text or binary files that can be fetched through the GitHub source API. |
| `outside_markers` | Yes | Required | Source owns content outside source-defined marker blocks. Target owns content inside those source-defined blocks. Existing targets may keep all marker blocks or omit all marker blocks. |
| `inside_markers` | Yes | Required | Source owns content inside marker blocks. Target owns outside content. Existing targets must keep matching exact marker blocks by occurrence order. |

## Notes

Marker-aware scopes first inspect the source bytes for the exact UTF-8 encoded start and end delimiter byte sequences. If the source contains neither exact UTF-8 encoded start delimiter bytes nor exact UTF-8 encoded end delimiter bytes, no extension points exist and the entry behaves as byte-level `whole_file`.

If the source contains either delimiter byte sequence, marker-aware synchronization is text-file behavior. Source and target marker-scoped files are decoded and encoded as strict UTF-8.

Marker validation criteria:

- `markers` is required for `outside_markers` and `inside_markers`.
- `markers` is forbidden for `whole_file`.
- `markers.start` and `markers.end` are required non-empty strings.
- `markers.start` and `markers.end` must not be identical.
- Marker delimiters are matched as exact text, not regular expressions.
- Source-side marker blocks define authoritative extension points where deterministically enforceable.
- Target-added fences never create edit permissions.
- Source and target marker blocks are matched by occurrence order.
- `outside_markers` allows complete omission of all target-owned marker blocks when the target equals the source outside projection.
- `outside_markers` rejects partial marker block omission and extra exact marker blocks until marker IDs or named blocks exist.
- `inside_markers` requires matching exact marker block count and occurrence order, but does not prove same-source-context position because outside content is target-owned.
- Stronger moved-block enforcement requires future marker IDs, named anchors, or source-owned outside context anchors.
