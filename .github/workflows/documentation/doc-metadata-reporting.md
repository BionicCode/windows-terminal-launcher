# Document metadata reporting

## Purpose

This document describes the report outputs currently produced by `.github/scripts/doc-metadata/update-doc-metadata.ps1` and consumed by `.github/workflows/doc-metadata.yml`.

It documents current behavior only. Do not infer future canonical current-link formatting or governance redesign from these report shapes.

## Script Outputs

| Output | How it is produced | Shape or contents |
| --- | --- | --- |
| Console report | Always written by `update-doc-metadata.ps1` | Human-readable mode, root, manifest, comparison details, summary counts, and tables for updated, unchanged, skipped, ineligible, repairable, unrecoverable, failed, and stale-check skipped files. |
| `GITHUB_STEP_SUMMARY` | Appended when the environment variable is set | Markdown summary with mode, counts, remediation command, updated files, skipped files, ineligible-match grouping, repairable files, unrecoverable files, and failures. |
| `-ReportOutputPath` | Optional script parameter | Full JSON report written inside the repository root. |
| `-ChangedFilesOutputPath` | Optional script parameter | JSON object with `changedFiles`, populated from `updatedFiles.path`. |
| `-ContentChangeOutputPath` | Optional script parameter | JSON object with `contentChanges`, used by content-change resolution. |

## ReportOutputPath JSON

The full report currently includes these top-level fields:

| Field | Contents |
| --- | --- |
| `mode` | One of the script modes, including `Analyze`, `Bootstrap`, `Update`, `Check`, or `ContentChanges`. |
| `root` | Resolved repository root used by the script. |
| `manifestPath` | Repository-relative manifest path. |
| `comparison` | Comparison mode, optional base/head SHAs, stale-check availability, and reason. |
| `updatedFiles` | Files rewritten by the command. |
| `unchangedFiles` | Files considered but not rewritten. |
| `skippedFiles` | Files skipped with a reason. |
| `failedFiles` | Validation or execution failures with remediation text. |
| `ineligibleFiles` | Manifest matches filtered out by document eligibility. |
| `ignoredByEligibility` | Ineligible entries grouped by the general eligibility path. |
| `ignoredByDeniedPath` | Ineligible entries denied by path. |
| `ignoredByDeniedExtension` | Ineligible entries denied by extension. |
| `ignoredBinaryOrNonText` | Files that are not strict UTF-8 text or appear binary. |
| `staleCheckSkippedFiles` | Files skipped from stale comparison with a reason. |
| `contentChanges` | Content-change classification results for `ContentChanges` mode. |
| `analysis` | Metadata validity, repair flags, repairable files, unrecoverable files, and repair categories. |
| `summaryCounts` | Counts used by console and GitHub summaries. |

## ChangedFilesOutputPath JSON

```json
{
  "changedFiles": ["README.md"]
}
```

Use this output for staging or hooks. Do not parse the human console report for changed paths.

## ContentChangeOutputPath JSON

```json
{
  "contentChanges": [
    {
      "path": "README.md",
      "baseSha": "0123456789abcdef0123456789abcdef01234567",
      "headSha": "abcdef0123456789abcdef0123456789abcdef01",
      "baseExists": true,
      "headExists": true,
      "newFile": false,
      "bodyChanged": true
    }
  ]
}
```

This output is used by the resolver to identify whether a specific commit or range changed a file's managed body content.

## Workflow Files

`doc-metadata.yml` currently creates or reads these files in the working checkout:

| File | Producer | Consumer |
| --- | --- | --- |
| `doc-metadata-event.json` | Workflow materialization step | Analyze, repair analyze, resolver, and post-repair comparison steps. |
| `doc-metadata-analyze-report.json` | Analyze command with `-ReportOutputPath` | Publish analyze outputs. |
| `doc-metadata-repair-analyze-report.json` | Repair analyze command with `-ReportOutputPath` | Repair path selection. |
| `doc-metadata-links.json` | `resolve-content-change-links.ps1` | Repair command through `-HistoryLinkMapPath`. |
| `doc-metadata-changed-files.json` | Repair command with `-ChangedFilesOutputPath` | Repair step decides whether to commit. |
| `doc-metadata-repair-report.json` | Repair command with `-ReportOutputPath` | Post-repair outputs and repair PR body. |
| `doc-metadata-post-check-report.json` | Post-repair Check command with `-ReportOutputPath` | Post-repair outputs and repair PR body. |
| `doc-metadata-repair-summary.md` | Repair report summary step | GitHub step summary and repair PR body. |

## See Also

- [Document metadata workflow](doc-metadata.md)
- [Document metadata tool](../../tools/doc-metadata/documentation/README.md)
