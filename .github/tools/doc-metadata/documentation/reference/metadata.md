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

# Metadata

`metadata` defines the managed header contract for a governed document.

In `defaults.metadata`, all core metadata fields are required by schema. In an include entry, `metadata` is optional and each supplied field overrides the inherited default for that pattern.

## Fields

| Field | Default manifest value | Purpose |
| --- | --- | --- |
| [`format`](fields/format.md) | `yaml-front-matter` | Metadata block format. |
| [`placement`](fields/placement.md) | `top` | Metadata block placement. |
| [`versionField`](fields/version-field.md) | `Version` | Managed document revision field name. |
| [`createdField`](fields/created-field.md) | `Created` | Managed initialization timestamp field name. |
| [`updatedField`](fields/updated-field.md) | `Updated` | Managed body-change timestamp field name. |
| [`authorField`](fields/author-field.md) | `Author` | Managed content-change author field name. |
| [`versioningMode`](fields/versioning-mode.md) | `body-content-change` | Version increment policy. |
| [`timestampFormat`](fields/timestamp-format.md) | `rfc3339-utc` | Generated timestamp format. |
| [`commentStart`](fields/comment-start.md) | none | Opening marker for `comment-block` metadata. |
| [`commentLinePrefix`](fields/comment-line-prefix.md) | none | Optional prefix for lines inside comment-block metadata. |
| [`commentEnd`](fields/comment-end.md) | none | Closing marker for `comment-block` metadata. |

Unknown metadata fields fail validation.

## Behavior Notes

- YAML front matter supports `top` placement only.
- `commentStart` and `commentEnd` are required when the effective format is `comment-block`.
- Custom front matter fields outside the managed field names are preserved and ignored by metadata automation.

## See Also

- [Defaults](defaults.md)
- [Include entry](include-entry.md)
