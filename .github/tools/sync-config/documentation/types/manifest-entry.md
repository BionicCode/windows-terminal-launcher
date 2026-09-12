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

# ManifestEntry

One managed source selection and destination directory. `ManifestEntry` is valid only as an item of `ManifestDocument.entries`.

## Example

```json
{
  "source_repo": "BionicCode/template-visual-studio-repository",
  "source_ref": "main",
  "source_path": "AGENTS.md",
  "target_directory": "",
  "direction": "source_to_target",
  "lifecycle_policy": "enforce",
  "uniqueness_policy": "none",
  "managed_scope": "whole_file"
}
```

## Fields

| Field | Required | Type | Description |
|---|---:|---|---|
| `source_repo` | Yes | string | Source repository in `owner/repository` form. |
| `source_ref` | Yes | string | Source branch, tag, or commit SHA. |
| `source_path` | Conditional | `RepoRelativeFilePath` | Exact source file. Required when `source_glob` is absent. |
| `source_glob` | Conditional | `SourceSelector` | Source pattern. Required when `source_path` is absent. |
| `target_directory` | Yes | `TargetDirectory` | Directory-only root for computed target files. |
| `glob` | Conditional | `GlobOptions` | Optional settings for `source_glob`; forbidden with `source_path`. |
| `direction` | Yes | `Direction` | Sync direction. |
| `lifecycle_policy` | Yes | `LifecyclePolicy` | Enforce, seed, or skip behavior. |
| `uniqueness_policy` | Yes | `UniquenessPolicy` | Optional basename uniqueness policy. |
| `managed_scope` | Yes | `ManagedScope` | Whole-file or marker-scoped sync. |
| `markers` | Conditional | `Markers` | Required for marker scopes; forbidden for `whole_file`. |

## Child Values

| Child | Parent property | Type |
|---|---|---|
| Exact source selector | `source_path` | `RepoRelativeFilePath` |
| Glob source selector | `source_glob` | `SourceSelector` |
| Glob options | `glob` | `GlobOptions` |
| Target directory | `target_directory` | `TargetDirectory` |
| Marker delimiters | `markers` | `Markers` |

## Validation

- Exactly one of `source_path` or `source_glob` is required.
- `glob` is valid only with `source_glob`.
- `target_directory` is required and directory-only.
- Exact-file targets are computed from `target_directory + basename(source_path)`.
- Glob targets are computed from `target_directory + matched source path relative to glob_base`.
- Duplicate computed targets fail before writes.
