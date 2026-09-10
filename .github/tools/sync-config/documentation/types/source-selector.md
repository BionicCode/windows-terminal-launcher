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

# SourceSelector

Field group owned by `ManifestEntry`. Exactly one selector is required.

| Field | Required | Type | Description |
|---|---:|---|---|
| `source_path` | XOR | `RepoRelativeFilePath` | Exact source file. Wildcards are not interpreted. |
| `source_glob` | XOR | string | Repository-relative POSIX glob pattern. |
| `glob` | Only with `source_glob` | object | Options for `source_glob`; rejected with `source_path`. |

## `source_path`

`source_path` selects exactly one source file. It remains exact-file only. Wildcards are invalid here; use `source_glob` for patterns.

The computed target file is:

```text
target_directory + basename(source_path)
```

## `source_glob`

`source_glob` selects many source files and expands them into exact-file sync operations during verify/sync planning.

Rules:

- Must be repository-relative and use `/` separators.
- Must contain at least one glob metacharacter: `*`, `?`, or `[`.
- `**` is valid only as a complete path segment and only with `glob.recursive: true`.
- `docs/**/*.md` matches both `docs/readme.md` and `docs/types/markers.md` when recursive matching is enabled.
- Zero matches fail during verify/sync planning before writes.
- Git tree truncation fails before writes.

## `glob`

| Field | Type | Default | Description |
|---|---|---:|---|
| `recursive` | boolean | `false` | Allows `**` as zero or more complete path segments. |
| `include_hidden` | boolean | `false` | Allows wildcard segments to match dot-prefixed path segments. |

Unknown `glob` option fields are invalid.

Hidden matching:

- `.github/scripts/**/*.md` is allowed with `include_hidden: false` because `.github` is explicit.
- `**/*.md` does not traverse `.github` when `include_hidden: false`.
- `docs/*.md` does not match `docs/.hidden.md` when `include_hidden: false`.
- `docs/.*.md` may match dot-prefixed files in `docs`.
