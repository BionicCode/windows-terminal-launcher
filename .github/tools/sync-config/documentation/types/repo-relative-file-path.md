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

# RepoRelativeFilePath

Repository-relative POSIX file path used by `ManifestEntry.source_path`.

| Property | Value |
|---|---|
| Kind | Scalar string |
| Valid parent | `ManifestEntry` |
| Parent property | `source_path` |

Rules:

- Must be non-empty.
- Must be relative to the repository root.
- Must use `/` separators.
- Must not end with `/`.
- Must not contain wildcard syntax.
- Must not contain leading `/`, backslashes, drive prefixes, empty segments, or exact `.` or `..` segments.
- Dot-prefixed ordinary names such as `.github/copilot-instructions.md` are valid.

Use `source_glob` for patterns.

## TargetDirectory

Directory-only target root used by `ManifestEntry.target_directory`.

| Property | Value |
|---|---|
| Kind | Scalar string |
| Valid parent | `ManifestEntry` |
| Parent property | `target_directory` |

Rules:

- `""` means repository root.
- Every non-root directory must end with exactly one `/`.
- The value never includes the target file name.
- Values such as `AGENTS.md`, `.github`, and `.github/AGENTS.md` are invalid because they are not directory syntax.
- `docs.v1/` is valid; dots are ordinary characters.
- Leading `/`, backslashes, drive prefixes, empty segments, and exact `.` or `..` segments are invalid.

Exact-file targets use `target_directory + basename(source_path)`. Glob targets use `target_directory + matched source path relative to glob_base`.
