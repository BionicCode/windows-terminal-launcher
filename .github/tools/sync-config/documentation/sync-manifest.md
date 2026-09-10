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

# Sync Manifest

`sync-manifest.json` tells the reusable workflow which source files are managed and where they are projected in the caller repository. Caller repositories own their manifest; this workflows repository owns the engine, schema, semantic rules, starter template, and copied reference documentation.

## Complete Shape

```json
{
  "$schema": "./sync-manifest.schema.json",
  "schema_version": 1,
  "entries": [
    {
      "source_repo": "BionicCode/workflows",
      "source_ref": "main",
      "source_path": "README.md",
      "target_directory": "",
      "direction": "source_to_target",
      "lifecycle_policy": "enforce",
      "uniqueness_policy": "none",
      "managed_scope": "whole_file"
    },
    {
      "source_repo": "BionicCode/workflows",
      "source_ref": "main",
      "source_glob": ".github/scripts/sync-files-from-manifest/documentation/**/*.md",
      "target_directory": ".github/sync-config/documentation/",
      "glob": {
        "recursive": true,
        "include_hidden": false
      },
      "direction": "source_to_target",
      "lifecycle_policy": "enforce",
      "uniqueness_policy": "none",
      "managed_scope": "whole_file"
    },
    {
      "source_repo": "BionicCode/template-visual-studio-repository",
      "source_ref": "main",
      "source_path": "AGENTS.md",
      "target_directory": "",
      "direction": "source_to_target",
      "lifecycle_policy": "enforce",
      "uniqueness_policy": "none",
      "managed_scope": "outside_markers",
      "markers": {
        "start": "<!-- BEGIN REPOSITORY SPECIFICS -->",
        "end": "<!-- END REPOSITORY SPECIFICS -->"
      }
    }
  ]
}
```

## Type Placement

| Type | Placement | Valid Parent | Parent Property | Description |
|---|---|---|---|---|
| `ManifestDocument` | Top-level object | None | None | Manifest root containing schema metadata and entries. |
| `ManifestEntry` | Array item | `ManifestDocument` | `entries[]` | One exact-file or glob-expanded managed sync definition. |
| `SourceSelector` | Field group | `ManifestEntry` | `source_path` or `source_glob` | Selects either one source file or a source file set. |
| `GlobOptions` | Nested object | `ManifestEntry` | `glob` | Optional controls for `source_glob`; forbidden with `source_path`. |
| `Markers` | Nested object | `ManifestEntry` | `markers` | Required for marker-scoped entries and forbidden for `whole_file`. |
| `RepoRelativeFilePath` | Scalar string | `ManifestEntry` | `source_path` | Exact source file path. |
| `TargetDirectory` | Scalar string | `ManifestEntry` | `target_directory` | Directory-only target root. |
| `Direction` | Enum string | `ManifestEntry` | `direction` | Sync direction. |
| `LifecyclePolicy` | Enum string | `ManifestEntry` | `lifecycle_policy` | Enforce, seed, or skip behavior. |
| `UniquenessPolicy` | Enum string | `ManifestEntry` | `uniqueness_policy` | Optional basename uniqueness policy. |
| `ManagedScope` | Enum string | `ManifestEntry` | `managed_scope` | Whole-file or marker-scoped ownership model. |

## Path Naming Rules

All manifest paths are logical repository-relative POSIX paths. They use `/`; the workflow does not use host OS normalization to accept or repair invalid manifest input.

`source_path`:

- Exactly one source file.
- Non-empty, repository-relative, and file-like.
- No trailing `/`.
- No wildcard syntax. Use `source_glob` for patterns.

`source_glob`:

- Pattern-based source file set.
- Must contain at least one glob metacharacter: `*`, `?`, or `[`.
- `**` is allowed only as a complete path segment and only when `glob.recursive` is `true`.
- `*`, `?`, and character classes match within a single path segment.

`target_directory`:

- Required for every entry.
- Directory-only target directory root. Do not include the target file name.
- `""` means repository root.
- Every non-root value must end with exactly one `/`, for example `.github/` or `docs/reference/`.
- Values such as `AGENTS.md`, `.github`, and `.github/AGENTS.md` are invalid because they are not directory syntax.
- `docs.v1/` is valid; dots are ordinary characters when the value uses directory syntax.

All path fields reject leading `/`, backslashes, drive-qualified values, empty segments, and exact `.` or `..` segments. Dot-prefixed ordinary names such as `.github/` remain valid.

## Source Selection

Exactly one of `source_path` or `source_glob` is required.

`source_path` computes a target file as:

```text
target_directory + basename(source_path)
```

Source parent directories are intentionally flattened in exact-file mode:

| `source_path` | `target_directory` | Computed target |
|---|---|---|
| `AGENTS.md` | `""` | `AGENTS.md` |
| `docs/README.md` | `""` | `README.md` |
| `docs/README.md` | `out/` | `out/README.md` |

`source_glob` expands to many exact-file operations. The computed target is:

```text
target_directory + matched source path relative to glob_base
```

`glob_base` is the longest leading directory prefix before the first segment containing `*`, `?`, or `[`.

| `source_glob` | `glob_base` | Matched source | Relative part | With `target_directory: "out/"` |
|---|---|---|---|---|
| `*.md` | `""` | `README.md` | `README.md` | `out/README.md` |
| `docs/*.md` | `docs/` | `docs/readme.md` | `readme.md` | `out/readme.md` |
| `docs/**/*.md` | `docs/` | `docs/readme.md` | `readme.md` | `out/readme.md` |
| `docs/**/*.md` | `docs/` | `docs/types/markers.md` | `types/markers.md` | `out/types/markers.md` |
| `.github/scripts/**/*.md` | `.github/scripts/` | `.github/scripts/a/b.md` | `a/b.md` | `out/a/b.md` |

`docs/**/*.md` matches both `docs/readme.md` and `docs/types/markers.md` when `glob.recursive` is `true`.

## Glob Options And Hidden Files

`glob` is allowed only with `source_glob`.

| Field | Type | Default | Description |
|---|---|---:|---|
| `recursive` | boolean | `false` | Allows `**` as zero or more complete path segments. |
| `include_hidden` | boolean | `false` | Allows wildcard segments to match dot-prefixed path segments. |

Hidden matching rules:

- `.github/scripts/**/*.md` is allowed with `include_hidden: false` because `.github` is explicit.
- `**/*.md` does not traverse `.github` when `include_hidden: false`.
- `docs/*.md` does not match `docs/.hidden.md` when `include_hidden: false`.
- `docs/.*.md` may match dot-prefixed files in `docs`.

`*.*` only matches names containing a dot. Use `*` for all files, `*.md` for Markdown files, and `**/*.md` with `glob.recursive: true` for recursive Markdown sync.

## Examples

Recursive documentation sync:

```json
{
  "source_repo": "BionicCode/workflows",
  "source_ref": "main",
  "source_glob": ".github/scripts/sync-files-from-manifest/documentation/**/*.md",
  "target_directory": ".github/sync-config/documentation/",
  "glob": {
    "recursive": true
  },
  "direction": "source_to_target",
  "lifecycle_policy": "enforce",
  "uniqueness_policy": "none",
  "managed_scope": "whole_file"
}
```

Top-level Markdown only:

```json
{
  "source_repo": "BionicCode/workflows",
  "source_ref": "main",
  "source_glob": "docs/*.md",
  "target_directory": "docs/",
  "direction": "source_to_target",
  "lifecycle_policy": "enforce",
  "uniqueness_policy": "none",
  "managed_scope": "whole_file"
}
```

Flat all-file sync:

```json
{
  "source_repo": "BionicCode/workflows",
  "source_ref": "main",
  "source_glob": "docs/*",
  "target_directory": "docs/",
  "direction": "source_to_target",
  "lifecycle_policy": "enforce",
  "uniqueness_policy": "none",
  "managed_scope": "whole_file"
}
```

Name-fragment filter:

```json
{
  "source_repo": "BionicCode/workflows",
  "source_ref": "main",
  "source_glob": ".github/scripts/sync-files-from-manifest/documentation/sync-manifest*.md",
  "target_directory": ".github/sync-config/documentation/",
  "direction": "source_to_target",
  "lifecycle_policy": "enforce",
  "uniqueness_policy": "none",
  "managed_scope": "whole_file"
}
```

Marker-scoped repository-specific section:

```json
{
  "source_repo": "BionicCode/template-visual-studio-repository",
  "source_ref": "main",
  "source_path": "AGENTS.md",
  "target_directory": "",
  "direction": "source_to_target",
  "lifecycle_policy": "enforce",
  "uniqueness_policy": "none",
  "managed_scope": "outside_markers",
  "markers": {
    "start": "<!-- BEGIN REPOSITORY SPECIFICS -->",
    "end": "<!-- END REPOSITORY SPECIFICS -->"
  }
}
```

Source placeholder:

```markdown
<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section. -->
<!-- END REPOSITORY SPECIFICS -->
```

Do not append annotations inside a marker delimiter unless the manifest marker string includes that annotation exactly. If no target-owned section is desired, use `whole_file` instead of marker scope.

Source marker blocks define target-owned extension points. If the source contains neither exact UTF-8 encoded start delimiter bytes nor exact UTF-8 encoded end delimiter bytes, no extension points exist and marker-aware scopes behave as byte-level `whole_file`. Target-added fences never create edit permissions. If a binary or non-UTF-8 source file happens to contain one of the configured marker delimiter byte sequences, marker-aware mode is entered and strict UTF-8 marker parsing applies.

## Current Execution Rules

Supported now:

- `source_path`
- `source_glob`
- `direction: source_to_target`
- `managed_scope: whole_file`, `outside_markers`, `inside_markers`
- `lifecycle_policy: enforce`, `seed_once`, `disabled`

Not supported:

- deleting unmatched target files
- `direction: target_to_source`
- `direction: two_way`

Directory listings and recursive source tree listings depend on GitHub API behavior. Recursive tree responses marked truncated fail rather than syncing a partial file set.

## Validate Vs Verify/Sync Timing

`validate` checks local schema and semantic rules only.

PR verification and branch/default maintenance sync perform remote expansion and planning:

- source tree enumeration
- zero-match detection
- generated duplicate target detection
- Git tree truncation failure
- source fetch
- marker parsing and UTF-8 checks
- all-or-nothing expected-output planning

No local writes happen until every entry and expanded file plans successfully. Unmatched target files are not deleted.
