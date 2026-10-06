---
Version: 3
Created: 2026-05-25T23:40:38+00:00
Updated: 2026-10-05T15:02:47+00:00
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

# Repository instruction router

## Scope and precedence

- Follow higher-priority system, tool, safety, and platform instructions first. Within the repository task context, user instructions override repository guidance.
- This router and `.agent/instructions/` define the portable shared baseline. Keep them concise, practical, and repository-agnostic. Repository facts and commands belong in [`.agent/REPOSITORY.md`](.agent/REPOSITORY.md) or applicable local overlays.
- More specific `AGENTS.override.md` or `AGENTS.md` files refine guidance for their directory. Follow local exceptions only when safe and when they do not weaken validation, review, or correctness requirements.
- Populated repository-local settings take precedence over generic defaults. Template placeholders never override concrete settings in protected legacy blocks. Surface material conflicts before dependent work; do not invent missing repository facts.
- Preserve unrelated worktree state. Passing tests alone does not establish correctness, and instruction changes must preserve applicable validation, documentation, and review duties.

## Start every task

1. Read [ENGINEERING.md](.agent/instructions/ENGINEERING.md). Also read [`.agent/REPOSITORY.md`](.agent/REPOSITORY.md) if present. Determine the task mode and smallest relevant scope from the user's request and repository evidence.
2. Inspect applicable nested instruction files along the selected target paths, from the repository root toward each target. At each directory prefer `AGENTS.override.md` over `AGENTS.md`. Do this explicitly: target-file location alone does not cause automatic loading in a root-started session. Retain protected local blocks in legacy bridges as local overlays.
3. Load every instruction file selected by the routing table before the planning, review, editing, or validation it governs. Routes combine; no row replaces another. Re-evaluate when scope expands. Links alone are not automatic imports.
4. If a mandatory shared file cannot be read, stop the dependent work and report the missing file. If local configuration is absent, use concrete protected legacy settings and repository evidence; ask only for material facts that inspection cannot resolve.

## Task modes and authorization

Use review-only mode for review, analysis, root-cause investigation, or design feedback. Do not modify source, test, configuration, or documentation unless the user asks. A code review may update `.agent/REVIEW.md` only after findings are determined, under CODE_REVIEW.md. Do not run builds, tests, or formatters unless asked. Prefer static reasoning and inspect actual call paths.

Use implementation mode when asked for changes, fixes, refactors, tests, cleanup, or feature work. Apply IMPLEMENTATION.md, including validation, documentation, separate self-review, and the completion report. A plan-only request does not authorize edits or command execution beyond the requested inspection. Explicit user approval of a plan authorizes implementation within its scope.

## Deterministic routing

Read all matching files before dependent work. The exact prompt `<review>` invokes the code-review workflow.

| Objective trigger | Required file | Timing |
| --- | --- | --- |
| Every repository task | [ENGINEERING.md](.agent/instructions/ENGINEERING.md) | At task start. |
| Requested source, test, configuration, workflow, documentation, or instruction changes; explicit commit or pull-request preparation | [IMPLEMENTATION.md](.agent/instructions/IMPLEMENTATION.md) | Before implementing or preparing the requested artifact; task mode still controls execution. |
| A code-review request, including exactly `<review>` | [CODE_REVIEW.md](.agent/instructions/CODE_REVIEW.md) | Before review. Ordinary analysis/design feedback and implementation self-review do not invoke backlog maintenance. |
| .NET / Visual Studio code, projects, solutions, MSBuild, SDKs, packages, analyzers, or their validation | [DOTNET.md](.agent/instructions/DOTNET.md) | Before domain planning, review, editing, or validation, regardless of working directory. |
| Planning, creating, changing, reviewing, or diagnosing tests, fixtures, assertions, or test execution | [TESTING.md](.agent/instructions/TESTING.md) | Before test work, wherever tests live and for any language. |
| Public APIs/external behavior; repository infrastructure; build/test/package/CI/release/deployment configuration; instruction files; schema/manifest/configuration/template/generated/copied files; security-sensitive behavior, paths, file I/O, commands, secrets, permissions, network access; migrations/compatibility/deprecations; cross-cutting refactors | [GUARDRAILS.md](.agent/instructions/GUARDRAILS.md) | Before guarded planning or editing; apply its relevant criteria during review/analysis. |
| Documentation, comments, examples, instruction Markdown, or behavior changes requiring documentation | [DOCUMENTATION.md](.agent/instructions/DOCUMENTATION.md) | Before documentation work and before planning or editing changes that require documentation. |
| Completed code-review findings needing identifier reconciliation, or an explicit user request concerning the backlog | [`.agent/REVIEW.md`](.agent/REVIEW.md) | Only after independent findings are determined; explicit backlog requests follow their requested scope. Never load as general implementation context. |

Examples: production .NET review loads ENGINEERING, CODE_REVIEW, and DOTNET; test review also loads TESTING. Implementation of production code and tests loads ENGINEERING, IMPLEMENTATION, DOTNET, and TESTING, plus GUARDRAILS and DOCUMENTATION whenever their triggers match. Build/configuration changes load GUARDRAILS; .NET build changes also load DOTNET. Documentation-only changes load ENGINEERING, IMPLEMENTATION, and DOCUMENTATION; instruction Markdown also loads GUARDRAILS.

## Ownership and compatibility

Shared rules live in the seven canonical files under `.agent/instructions/`. Root compatibility files, nested entry points, and Copilot files are thin bridges with protected repository-specific fences. Keep those fences intact. Synchronize shared canonical files as whole files; preserve local configuration separately. `.agent/REPOSITORY.md`, `.agent/REVIEW.md`, and `CLAUDE.md` are seeded only when absent, and existing copies must remain untouched.

Do not move portable correctness requirements into a personal Codex-global file. Detailed recurring workflows may become Skills in a later audit; this partition retains their rules. See the [architecture and rollout contract](docs/agent-instruction-architecture.md).

<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section. -->
# Repository Specifics

Fill in or edit this section per repository. Everything above this section is intended to remain stable across repositories.

## Solution and Structure
- Primary solution name: `<SolutionName>`
- Source root(s): `src/`
- Test root: `test/`
- Unit test project name: `<SolutionName>.Tests`
- Documentation location: `docs/`

## Build and Validation
- Preferred restore command: `<fill me>`
- Preferred build command: `<fill me>`
- Preferred unit test command: `<fill me>`
- Preferred integration test command: `<optional>`
- Preferred style or analyzer validation command: `<optional>`

## Repository Conventions
- Preferred frameworks, languages, or libraries: `<fill me>`
- Naming conventions beyond language defaults: `<fill me>`
- Commit / PR conventions: `<fill me>`
- Documentation-specific expectations: `See DOCUMENTATION.md if present.`
- Any allowed exceptions to the shared rules above: `<fill me>`

## Optional Specialized Instruction Files
- Additional instruction files or skills used by this repository: `<optional>`
- Paths where nested instructions intentionally override this file: `<optional>`
- Specialized review checklist files, if any: `<optional>`

<!-- END REPOSITORY SPECIFICS -->
