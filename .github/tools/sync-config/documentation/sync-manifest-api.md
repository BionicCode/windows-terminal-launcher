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

# Sync Manifest API Reference

This is the public manifest API for `sync-files-from-manifest.yml`.

## Feature Support Matrix

| Feature | Schema-valid | Runtime-supported | Notes |
|---|---:|---:|---|
| `source_path` exact-file sync | Yes | Yes | One source file maps to one computed target file. |
| `source_glob` directory/file-set sync | Yes | Yes | Expands matching source files into exact-file sync operations. |
| `source_to_target` | Yes | Yes | Current executable direction. |
| `whole_file` | Yes | Yes | Byte-for-byte sync. |
| `outside_markers` | Yes | Yes | Source owns outside content; target owns marker bodies. |
| `inside_markers` | Yes | Yes | Source owns marker bodies; target owns outside content. |
| `target_to_source` | Yes | No | Rejected by current execution rules. |
| `two_way` | Yes | No | Rejected by current execution rules. |
| Delete unmatched targets | No | No | Future delete policy required. |

Marker-aware scopes behave as byte-level `whole_file` when the source contains neither exact UTF-8 encoded start delimiter bytes nor exact UTF-8 encoded end delimiter bytes. If the source contains either delimiter byte sequence, strict UTF-8 marker parsing and marker-scope rules apply.

## Type Index

| Type | Placement | Summary |
|---|---|---|
| [`ManifestDocument`](types/manifest-document.md) | Top-level object | Root object containing schema metadata and entries. |
| [`ManifestEntry`](types/manifest-entry.md) | Array item | One managed source selection and target directory. |
| [`SourceSelector`](types/source-selector.md) | Field group | Exactly one of `source_path` or `source_glob`, plus optional `glob` for patterns. |
| [`RepoRelativeFilePath`](types/repo-relative-file-path.md) | Scalar string | Exact repository-relative POSIX source file path. |
| [`TargetDirectory`](types/repo-relative-file-path.md#targetdirectory) | Scalar string | Directory-only target root. |
| [`Markers`](types/markers.md) | Nested object | Marker delimiters for marker-scoped entries. |
| [`Direction`](types/direction.md) | Enum string | Sync direction. |
| [`LifecyclePolicy`](types/lifecycle-policy.md) | Enum string | Enforce, seed, or skip behavior. |
| [`UniquenessPolicy`](types/uniqueness-policy.md) | Enum string | Optional basename uniqueness policy. |
| [`ManagedScope`](types/managed-scope.md) | Enum string | Whole-file or marker-scoped ownership. |

## ManifestEntry Fields

| Field | Required | Type | Description |
|---|---:|---|---|
| `source_repo` | Yes | string | Source repository in `owner/repository` form. |
| `source_ref` | Yes | string | Source branch, tag, or commit SHA. |
| `source_path` | XOR | `RepoRelativeFilePath` | Exact source file. Wildcards are rejected. |
| `source_glob` | XOR | string | Repository-relative POSIX glob pattern. Must contain `*`, `?`, or `[`. |
| `target_directory` | Yes | `TargetDirectory` | Directory root for computed target files. |
| `glob` | Only with `source_glob` | `GlobOptions` | Pattern options. Rejected with `source_path`. |
| `direction` | Yes | `Direction` | Only `source_to_target` executes currently. |
| `lifecycle_policy` | Yes | `LifecyclePolicy` | `enforce`, `seed_once`, or `disabled`. |
| `uniqueness_policy` | Yes | `UniquenessPolicy` | `basename_unique` or `none`. |
| `managed_scope` | Yes | `ManagedScope` | `whole_file`, `outside_markers`, or `inside_markers`. |
| `markers` | Conditional | `Markers` | Required for marker scopes; forbidden for `whole_file`. |

## GlobOptions

| Field | Type | Default | Description |
|---|---|---:|---|
| `recursive` | boolean | `false` | Enables `**` as zero or more complete path segments. |
| `include_hidden` | boolean | `false` | Allows wildcard segments to match dot-prefixed path segments. |

The `glob` object is closed. Unknown option fields are invalid.

`source_glob` containing a `**` segment is valid only when `glob.recursive` is explicitly `true`. Omitted `glob`, empty `glob`, omitted `recursive`, `recursive: false`, and non-boolean `recursive` are invalid for `**` patterns.

## Runtime Target Computation

Exact-file entries:

```text
computed target = target_directory + basename(source_path)
```

Glob entries:

```text
computed target = target_directory + matched source path relative to glob_base
```

`glob_base` is the longest leading directory prefix before the first source glob segment containing `*`, `?`, or `[`.

## Validation And Runtime Errors

Schema/local validation can reject:

- both `source_path` and `source_glob`
- neither `source_path` nor `source_glob`
- `glob` with `source_path`
- unknown manifest-entry fields
- stale removed destination fields
- non-directory `target_directory`
- invalid POSIX path segments, backslashes, absolute paths, drive-qualified values, `.`, or `..`
- wildcard syntax in `source_path`
- `source_glob` without a glob metacharacter
- `source_glob` with invalid `**` usage
- unknown or non-boolean `glob` option fields

Verify/sync planning can reject:

- `source_glob` matched zero files
- source tree response was truncated
- duplicate generated computed targets
- source fetch failure
- marker parse failure in an expanded file when the source contains at least one configured marker delimiter byte sequence
- marker-scoped UTF-8 decode failure when marker-aware mode is entered
- reserved or unsafe computed target paths

`validate` is local-only. Remote expansion, zero-match detection, generated duplicate detection, source tree truncation, source fetching, marker parsing, and write planning happen during PR verification or branch/default maintenance sync before writes.
