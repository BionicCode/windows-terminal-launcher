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

# ManifestDocument

The top-level manifest object. This type has no parent.

## Shape

```json
{
  "$schema": "./sync-manifest.schema.json",
  "schema_version": 1,
  "entries": [
    {
      "source_repo": "BionicCode/template-visual-studio-repository",
      "source_ref": "main",
      "source_path": "README.md",
      "target_directory": "",
      "direction": "source_to_target",
      "lifecycle_policy": "seed_once",
      "uniqueness_policy": "none",
      "managed_scope": "whole_file"
    }
  ]
}
```

## Fields

| Field | Required | Type | Description |
|---|---:|---|---|
| `$schema` | No | string | Relative schema path for editor tooling. |
| `schema_version` | Yes | integer | Must be `1`. |
| `entries` | Yes | `ManifestEntry[]` | Non-empty managed entry list. |

## Child Values

| Child | Parent property | Type |
|---|---|---|
| Managed entries | `entries[]` | `ManifestEntry` |

Old top-level array manifests are rejected. Wrap the array under `entries` and add `schema_version`.
