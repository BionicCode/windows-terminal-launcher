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

# Markers

Marker delimiter object used by marker-scoped managed scopes.

## Shape

```json
{
  "start": "<!-- BEGIN REPOSITORY SPECIFICS -->",
  "end": "<!-- END REPOSITORY SPECIFICS -->"
}
```

## Placement

| Property | Value |
|---|---|
| Placement | Nested object |
| Valid parent | [`ManifestEntry`](manifest-entry.md) |
| Parent property | `markers` |

## Attributes

| Attribute | Required | Type | Description |
|---|---:|---|---|
| `start` | Yes | string | Non-empty start delimiter. |
| `end` | Yes | string | Non-empty end delimiter. |

## Rules

- `start` and `end` must not be identical.
- `markers` is required when `managed_scope` is `outside_markers` or `inside_markers`.
- `markers` is forbidden when `managed_scope` is `whole_file`.
- Marker delimiters are exact text delimiters, not regular expressions.
- Marker matching does not trim text, normalize whitespace, case-fold, or use similar-looking text.
- No fuzzy diff, longest-common-subsequence, `difflib`, or heuristic movement detection is used.
- Start and end delimiters may appear anywhere in a line or as full lines.
- A marker block starts immediately after the matched start delimiter and ends immediately before the matched end delimiter.
- Multiple marker blocks are allowed.
- Adjacent marker blocks are allowed.
- Empty inside content and empty outside content are allowed.
- Nested marker blocks are rejected.
- If the source contains neither exact UTF-8 encoded start delimiter bytes nor exact UTF-8 encoded end delimiter bytes, marker-aware scopes behave as byte-level `whole_file`.
- If the source contains either delimiter byte sequence, the source file must contain valid exact marker pairs and strict UTF-8 marker parsing applies.
- Source marker blocks define the authoritative sync regions where deterministic enforcement is possible.
- Target-added fences never create edit permissions.
- Target marker interpretation depends on `managed_scope`.

## `outside_markers`

- Source owns content outside source-defined marker blocks.
- Target owns content inside source-defined marker blocks.
- If the target contains the same number of exact marker blocks as the source, target inner content is preserved by occurrence order.
- If the target contains zero exact marker blocks, the target is valid only when it equals the source outside projection.
- Partial marker block omission and extra exact target marker blocks fail before write.

## `inside_markers`

- Source owns content inside marker blocks.
- Target owns outside content.
- `inside_markers` requires the target to contain the same number of exact marker blocks as the source.
- Source inner content is enforced by occurrence order.
- Same-source-context movement detection is intentionally not attempted because outside content is target-owned.
- Stronger moved-block enforcement requires future marker IDs, named anchors, or source-owned outside context anchors.

## Composition

- When a target file exists, composed output preserves marker delimiter slices from the target file.
- Marker-scoped synchronization uses strict UTF-8 decoding and encoding only after marker-aware mode is entered by finding at least one configured delimiter byte sequence in the source.
- If a binary or non-UTF-8 source file happens to contain one of the configured marker delimiter byte sequences, strict UTF-8 marker parsing applies.
