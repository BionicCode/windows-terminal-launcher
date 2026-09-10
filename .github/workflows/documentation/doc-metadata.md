# Document Versioning Workflow

## Purpose

[doc-metadata.yml](../doc-metadata.yml) analyzes governed document metadata, performs safe metadata repairs when allowed, publishes repair results, and enforces a final status gate for the metadata workflow.

## When it runs

| Trigger | Active or passive | Notes |
| --- | --- | --- |
| `workflow_call` | Passive/reusable | Normal repository maintenance routing calls this workflow through the orchestrator. |
| `workflow_dispatch` | Passive/reusable with direct manual entry | Available for targeted manual execution when maintainers intentionally want to run the metadata workflow directly. |

> [!NOTE]
> Normal pull request, push, and schedule routing is owned by [Repository maintenance](repository-maintenance.md), not by this workflow directly.

## Workflow role

This workflow is a passive reusable child workflow.

It preserves the local document-metadata workflow behavior while accepting normalized context from the orchestrator.

## Execution flow

1. `analyze-document-metadata` checks out trusted scripts and working content, materializes the effective event payload, and analyzes governed metadata state.
2. `repair-document-metadata` runs only when repair is required, safe, and allowed for the current repository context.
3. `final-document-metadata-status` gates the overall result and preserves the workflow's final status semantics.

## Examples

| Scenario | What happens | Result |
| --- | --- | --- |
| Pull request metadata validation | The orchestrator calls this workflow with normalized PR base/head context so the analyze job can compare the PR head against the PR base safely. | The workflow reports whether governed metadata is valid before sync is allowed to proceed. |
| Safe automatic repair on a same-repository pull request | When analysis reports `repair_required == true` and `repair_safe == true`, the repair job checks out the PR branch, applies metadata-only repairs, commits them, and pushes back to the PR branch. | The PR is updated in place and the final status reflects the post-repair check result. |
| Branch-based repair PR creation for non-PR runs | On branch-based runs outside `pull_request`, the repair job creates a deterministic `codex/doc-metadata-repair/<safe-target>-<hash>` branch, publishes it, and creates or updates a repair PR. | Maintainers get a repair PR instead of an in-place branch mutation on the source branch. |
| Recursion guard on `codex/doc-metadata-repair/*` | If the current branch is already a doc-metadata repair branch, the workflow still analyzes metadata but skips repair publishing. | The run avoids repair-branch recursion and fails the final status if repair is still required. |

## Inputs

### `workflow_call` inputs

| Input | Purpose |
| --- | --- |
| `effective_event_name` | Effective event type used by the reusable workflow logic. |
| `event_payload_json` | Normalized event payload passed in from the orchestrator. |
| `default_branch` | Effective default branch for trusted checkout and branch comparisons. |
| `ref_name` | Effective branch/ref name. |
| `ref` | Effective full Git ref. |
| `sha` | Effective head SHA for analysis and repair flows. |
| `base_ref` | Effective pull request base branch name. |
| `head_ref` | Effective pull request head branch name. |
| `pull_request_base_sha` | Effective PR base SHA for comparison and merge-base logic. |
| `pull_request_head_sha` | Effective PR head SHA for comparison and repair logic. |
| `pull_request_head_ref` | Effective PR head ref used for repair-branch push behavior. |
| `pull_request_head_repo_full_name` | Effective PR head repository identity for same-repository repair decisions. |
| `pull_request_number` | Effective PR number used for concurrency scoping. |

### `workflow_dispatch`

This workflow currently defines no custom manual-dispatch inputs.

## Permissions and secrets

| Job | Permissions | Secrets |
| --- | --- | --- |
| `analyze-document-metadata` | `contents: read` | uses `github.token` for fetch operations when PR comparison SHAs must be materialized |
| `repair-document-metadata` | `contents: write`, `pull-requests: write` | uses `github.token` for repair push and PR creation/update flows |
| `final-document-metadata-status` | none | none |

## Important behavior notes

- This workflow remains local to this repository.
- It preserves metadata analysis, safe repair behavior, repair branch and repair PR behavior, and the final status gate.
- Runs on `codex/doc-metadata-repair/*` branches still preserve the repair-branch guard behavior.
- The workflow accepts normalized context so the reusable form does not rely on implicit caller event payload shape.

> [!IMPORTANT]
> Keep the existing `final-document-metadata-status` job semantics intact unless there is an intentional behavior change backed by workflow and script review.

> [!WARNING]
> Do not move trigger ownership for normal PR/push/schedule routing back into this file unless the orchestrator architecture is intentionally being removed or redesigned.

## See also

- The workflow's [README.md](../../tools/doc-metadata/documentation/README.md) for detailed information about configuration using the manifest.
- [Document metadata reporting](doc-metadata-reporting.md) for console, JSON, summary, and workflow report outputs.

## Related workflows

- [Repository maintenance](repository-maintenance.md)

## Maintenance notes

- Safe edits here include documentation updates about trigger ownership, input contracts, and orchestration boundaries.
- Do not describe changed PowerShell behavior here unless the PowerShell scripts themselves were actually changed.
- Treat repair-branch naming, safe-repair gating, and final status gating as workflow behavior boundaries that should stay aligned with the YAML and local scripts.
