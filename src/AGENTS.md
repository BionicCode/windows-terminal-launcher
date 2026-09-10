---
Version: 1
Created: 2026-05-25T23:40:39+00:00
Updated: 2026-05-26T19:08:35+00:00
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

# AGENTS.md

## Scope
These instructions apply to .NET / Visual Studio work in this subtree.
Read them together with the repository root `AGENTS.md` and any more specific `AGENTS.md` file in a deeper directory.
User instructions override this file.

---

## Repository contract files
Before planning, editing, or validating .NET code, inspect the repository files that can change effective build, analysis, test, packaging, and language behavior.

Check these when present:
- `/.editorconfig`
- `/Directory.Build.props`
- `/Directory.Build.targets`
- `/Directory.Packages.props`
- `/global.json`
- relevant `*.sln`, `*.csproj`, `*.props`, `*.targets`, and repository build/test scripts

Do not assume project files tell the full story when shared repository-level configuration exists.

Important:
- Treat `Directory.Build.props` and `Directory.Build.targets` as part of the effective project definition.
- If repository-level settings conflict with local assumptions, follow the effective repository configuration instead of generic .NET defaults.

---

## Command policy
Prefer repository-local or task-local scope over whole-repo commands when possible.
Prefer repository-provided scripts or wrappers when they are the documented validation path.

Typical .NET command sequence:

```bash
# If restore is required
 dotnet restore <solution-or-project>

# Build
 dotnet build <solution-or-project> --no-restore

# Smallest relevant test scope
 dotnet test <test-project-or-solution> --no-build

# Apply style/analyzer fixes from .editorconfig and analyzers
 dotnet format <solution-or-project> --no-restore

# Final verification: fail if any formatting/analyzer drift remains
 dotnet format <solution-or-project> --no-restore --verify-no-changes
```

Rules:
- Do not use `dotnet format --verify-no-changes` as the only formatting step for implementation tasks. That only checks; it does not fix.
- If `--no-restore` fails because restore has not yet happened, run restore once and continue.
- If tests fail, do not stop at the first red run. Investigate, fix, and rerun the smallest relevant build/test scope until the targeted tests pass or a concrete blocker prevents further progress.
- Treat red tests caused by your changes as part of the task, not as optional follow-up work.
- If a test is broken for reasons unrelated to your change, identify the evidence clearly and continue validating the remaining relevant scope where possible.
- If the repo uses a different validation entry point, such as scripts, CI wrappers, `Makefile` targets, or PowerShell helpers, prefer that path and mention it in the report.
- Never finish an implementation task while knowingly leaving newly introduced analyzer/style violations unresolved unless the user explicitly allows it.
- Never finish an implementation task while knowingly leaving newly failing relevant tests unresolved unless the user explicitly allows it or you have hit a concrete blocker that you report.

---

## .editorconfig and analyzer compliance
Treat `.editorconfig`, analyzer configuration, and project analysis settings as part of the repository contract.

For .NET repositories:
- Assume `.editorconfig` is authoritative for whitespace, code style, naming, and analyzer configuration.
- Respect warnings and errors from SDK analyzers and any repository-installed analyzers.
- Prefer fixing the code over suppressing the diagnostic.
- If a violation appears to be a false positive, document it and use the narrowest justified suppression only if the user allows repository rule exceptions.

Important:
- Command-line `dotnet build` does not automatically enforce all IDE code-style diagnostics unless the project enables build enforcement for code-style analysis.
- Therefore, for implementation tasks, do not rely on build alone to prove `.editorconfig` compliance. Run `dotnet format` as part of validation.
- If the repository expects IDE-style rules to fail on build, honor project-level enforcement such as `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors`, `AnalysisLevel`, and related settings when they are configured in shared props/targets files.

---

## Naming and readability expectations
Follow `.editorconfig` naming rules exactly, but do not stop there.

Also apply these readability rules:
- Prefer intention-revealing names for types, members, locals, parameters, lambda parameters, and type parameters.
- Avoid cryptic or low-information abbreviations when a descriptive name materially improves readability.
- Prefer names such as `count`, `index`, `file`, `directory`, `entry`, `item`, `message`, `result`, or `cancellationToken` over names such as `cnt`, `idx`, `f`, `dir`, `e`, `x`, `msg`, or `ct` unless the shorter form is a well-established domain term.
- Name lambda parameters after the role or item being processed so readers do not need to inspect surrounding types just to infer what is being enumerated.
- Use comments to explain intent, invariants, caveats, or trade-offs, not to compensate for poor naming.
- Improve names, structure, or method extraction before adding explanatory comments for code that is only hard to read because the naming is weak.

---

## Design expectations for .NET code
In addition to repository-wide engineering rules:
- Prefer cohesive types with clear responsibilities.
- Apply single-responsibility thinking when adding or refactoring classes and methods.
- Keep public APIs explicit and unsurprising.
- Avoid leaking infrastructure, UI, serialization, SDK, or persistence concerns across boundaries unless the repository design clearly requires it.
- Prefer constructor injection and explicit dependencies over hidden global state when the repository uses dependency injection.
- Keep extension methods, helpers, and utility classes narrowly scoped and domain-appropriate; do not use them to hide poor design.

---

## Documentation expectations
When code changes affect public APIs, externally consumed behavior, important invariants, non-obvious algorithms, caveats, or usage patterns:
- update XML documentation comments for relevant public and protected APIs,
- add concise source comments where names and structure alone do not explain intent,
- and update or add a small Markdown documentation file when consumers need usage guidance, examples, data-model explanation, or behavior notes.

Minimum expectations:
- Do not add comments that merely restate obvious syntax.
- Do add comments for design intent, invariants, caveats, edge cases, unsupported scenarios, trade-offs, and surprising behavior.
- Use proper XML documentation tags and language-aware markup such as `<see cref="..."/>` and `<see langword="null"/>` where appropriate.
- Keep documentation aligned with the implemented behavior; stale documentation is a defect.

---

## Test project conventions
When adding or generating new automated tests for this repository:
- place test projects under `/test` at repository root,
- create `/test` if it does not already exist,
- and name generated unit test projects `<SolutionName>.Tests` unless the repository already defines a stricter convention.

---

## Reporting requirements for implementation tasks
When you changed code, report:
- what you changed,
- which commands you ran,
- whether build passed,
- whether tests passed,
- whether formatting/analyzer verification passed,
- whether documentation was updated and at what level,
- and any remaining warnings/errors or blockers with a reason.

<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section -->

<!-- END REPOSITORY SPECIFICS -->
