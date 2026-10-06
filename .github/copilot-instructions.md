---
Version: 2
Created: 2026-05-25T23:40:38+00:00
Updated: 2026-10-05T14:43:13+00:00
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

# Copilot repository bridge

Read and follow the [root repository router](../AGENTS.md) before planning, reviewing, editing, or validating. It defines task modes, precedence, and all additive domain/workflow routes. Explicitly read each routed file; links alone do not load its rules. If a required shared file is missing, stop the dependent work and report the boundary.

Applicable local settings and nested instruction overlays remain effective. Shared rule bodies live only in `.agent/instructions/`; do not maintain a second copy here.

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
