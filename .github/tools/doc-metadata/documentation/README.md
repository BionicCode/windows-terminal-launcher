---
Version: 10
Created: 2026-05-26T19:08:33+00:00
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

# Document Versioning

## What This Tool Does

The document versioning tool keeps human-readable metadata headers current for governed UTF-8 document files. It writes managed fields such as `Version`, `Created`, `Updated`, and `Author`, can add a generated presentation area for Markdown files, and verifies that document revisions match governed body-content changes.

The manifest is the configuration source of truth. The hosted workflow intentionally does not duplicate manifest include patterns in workflow `paths` filters because GitHub evaluates those filters before the workflow starts.

> [!IMPORTANT]
> First-time setup usually requires one Bootstrap or Repair run to initialize existing governed files. For migrated existing files, `Created` means metadata initialization time unless a future Git-history inference feature is added.

## Configuration

The workflow is configured using [doc-metadata-manifest.json](../doc-metadata-manifest.json). Start with the [manifest guide](doc-metadata-manifest.md), then use the [manifest API reference](doc-metadata-manifest-api.md) for object and field pages.

Current manifest references:

- [Manifest](reference/manifest.md)
- [Defaults](reference/defaults.md)
- [Metadata](reference/metadata.md)
- [Presentation](reference/presentation.md)
- [Include](reference/include.md)
- [Exclude](reference/exclude.md)
- [Document eligibility](reference/document-eligibility.md)

## Metadata Output

Markdown files use YAML front matter by default:

```md
---
Version: 2
Created: 2026-05-25T14:05:02+00:00
Updated: 2026-05-26T01:40:38+00:00
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

# Document title
```

Plain text files use compact metadata by default:

```text
---
Version: 2
Created: 2026-05-25T14:05:02+00:00
Updated: 2026-05-26T01:40:38+00:00
Author: BionicCode
---
--------------------------------------------------------------------------------


Document body starts here.
```

Markdown `spacingBreaks` creates explicit `<br>` lines. Plain text `spacingBreaks` creates physical blank lines using the file newline style. The tool never inserts `<br>` into `.txt` files.

## Reading Managed Fields

`Version` is document revision notation. It may be a positive integer or a dotted numeric value such as `2.1`, but it is not SemVer. Automatic increments update the first component only, so `2.1.2` becomes `3`.

`Created` is the metadata initialization timestamp and is immutable after initialization. Generated timestamps are UTC RFC 3339 values with an explicit `+00:00` offset.

`Updated` changes only when governed document body content changes. Generated values are normalized to UTC `+00:00`.

`Author` is the detected content-change author. Local runs prefer `git config user.name`; GitHub repair prefers the content-changing commit author and avoids `github-actions[bot]` when the bot only authored metadata repair.

Generated presentation, current-version links, separators, and embedded history are excluded from body comparison. Git remains the canonical full audit log.

## Common Workflows

Same-repository pull request: Analyze runs read-only. If repair is safe, Repair pushes one metadata commit to the PR branch, then runs Check again because `GITHUB_TOKEN` commits may not trigger all follow-up workflows. Runs on `codex/doc-metadata-repair/` branches skip repair publishing so repair PRs do not recursively create more repair PRs.

Fork pull request: Analyze reports only. The workflow does not run write-capable repair for forks.

Direct push to default branch: Analyze classifies metadata state. Safe repair creates or updates a deterministic bot branch and repair PR instead of pushing to main.

`workflow_dispatch`: Runs the Analyze/Repair/Final Status flow for the selected branch. If no explicit or safely derived comparison context exists, the tool can repair metadata but does not create a new content Change History entry.

Local Bootstrap/Update/Check:

```powershell
pwsh ./.github/scripts/doc-metadata/update-doc-metadata.ps1 -Mode Bootstrap -Root .
pwsh ./.github/scripts/doc-metadata/update-doc-metadata.ps1 -Mode Update -Root .
pwsh ./.github/scripts/doc-metadata/update-doc-metadata.ps1 -Mode Check -Root .
```

## Repair Safety

The tool compares body content after removing the metadata block and the generated presentation/separator region. Generated history, current-version links, separators, and spacing updates cannot trigger a self-perpetuating version bump.

Document history tracks document content versions, not metadata maintenance. Tool-only metadata initialization, formatting repair, timestamp repair, URL repair, and safe tamper restoration are reported in console output, JSON reports, GitHub summaries, and repair PR bodies. They are not embedded as content Change History entries.

Metadata-only repair preserves the previous proven current-version link. A body change with a proven replacement link updates the current-version link. A body change without reliable content-change context clears that top link rather than preserving a link for an older document version.

Manual `Version` increases are allowed as a rebaseline when the body is unchanged and the rest of the managed metadata is valid. Version decreases are rejected by default.

Custom front matter fields are preserved and ignored:

```yaml
---
Version: 2
Created: 2026-05-25T14:05:02+00:00
Updated: 2026-05-26T01:40:38+00:00
Author: BionicCode
Tool: Visual Studio Code
ReviewState: Draft
---
```

> [!WARNING]
> Unsafe repair cases include version rollback, invalid version values, ambiguous malformed metadata, malformed presentation without trusted previous state, and fork PRs where write access is unavailable.

## Links In Generated History

Current behavior emits only proven commit fallback links with link text `View Commit`. `View Changes` is reserved for future verified file-specific changes support and is rejected in managed presentation in the current implementation.

If no reliable content-change context exists, the tool repairs metadata without adding a new current-version link or history entry.

The workflow distinguishes the content-change commit from a later bot repair commit. For each repaired file, the trusted resolver script asks `update-doc-metadata.ps1 -Mode ContentChanges` to compare the managed body at candidate commits. The newest commit that actually changed that file's body becomes the history context. Merge commits with multiple parents are skipped rather than guessed.

## Reporting

The script and workflow emit console output, optional `GITHUB_STEP_SUMMARY` Markdown, full JSON reports, changed-file JSON, content-change JSON, and repair PR summaries.

See [Document metadata reporting](../../../workflows/documentation/doc-metadata-reporting.md) for the current report files and JSON shapes.

## Troubleshooting Terms

`repairableFiles`: hosted repair can safely update these files.

`unrecoverableFiles`: manual intervention is required before repair.

`ineligibleFiles`: the manifest matched files that are not eligible document text files.

`ignoredBinaryOrNonText`: the file is not strict UTF-8 or contains binary data. Convert it to UTF-8 if it should be managed.

`historyTamperDetected`: generated history was edited.

`historyRestoredFromTrustedPrevious`: generated history was restored safely.

> [!TIP]
> `documentEligibility` is implemented today as a compatibility filter. It should not be treated as the future governance model.
