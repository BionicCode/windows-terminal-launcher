---
Version: 1
Created: 2026-05-25T23:40:38+00:00
Updated: 2026-05-26T19:08:34+00:00
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

# AGENT_GUARDRAILS.md 

Version: x

## Purpose

This file defines cross-repository guardrails for coding agents working in repositories.

Use this file together with:

- `AGENTS.md`
- `DOCUMENTATION.md`
- `src/AGENTS.md` when working under `src/`
- `test/AGENTS.md` when working under `test/`
- `.github/instructions/*.instructions.md` when working through GitHub Copilot instruction surfaces

These guardrails are not a replacement for repository-specific instructions. Repository-specific commands, frameworks, paths, and exceptions belong in the `Repository Specifics` section of `AGENTS.md` or in a nested `AGENTS.md`.

## When to read this file

Read and apply this file before planning or editing when the task touches any of these:

- public APIs or externally consumed behavior;
- build, test, packaging, CI, release, or deployment configuration;
- repository instruction files such as `AGENTS.md`, `.github/copilot-instructions.md`, `.github/instructions/*.instructions.md`, or `DOCUMENTATION.md`;
- schema, manifest, configuration, template, generated, or copied files;
- security-sensitive behavior, path handling, file I/O, command execution, secrets, permissions, or network access;
- migrations, compatibility behavior, or deprecations;
- cross-cutting refactors.

## Non-negotiable repository guardrails

- Do not weaken validation, tests, analyzers, documentation requirements, or review requirements to make a task easier.
- Do not modify unrelated worktree state.
- Do not perform broad cleanup, formatting, package updates, dependency upgrades, or generated-file refreshes unless they are necessary for the task or explicitly requested.
- Do not change public contracts silently. Public behavior changes require matching code, tests, docs, examples, and migration notes where relevant.
- Do not claim a task is complete until the implementation has been checked against the task goal, public contract, and acceptance criteria.
- Do not treat passing tests as sufficient when the tests merely mirror the implementation.
- Do not hide uncertainty. If a path, dependency, generated source, or runtime behavior cannot be verified, report the boundary.

## Protected instruction surfaces

Treat these files as repository-governance surfaces:

- `AGENTS.md`
- `AGENT_GUARDRAILS.md`
- `DOCUMENTATION.md`
- `.github/copilot-instructions.md`
- `.github/instructions/*.instructions.md`
- nested `AGENTS.md` files

When modifying these files:

- preserve the stable global character of the inherited template instructions;
- keep repository-specific edits inside designated repository-specific sections or nested files;
- do not remove marker fences unless explicitly requested;
- keep mirrored or derived instruction files synchronized when the repository owns the copy;
- prominently report which instruction surfaces changed and why.

## Public contract consistency

When a task changes a public or user-visible contract, update every relevant surface in the same change.

Check for consistency across:

- implementation;
- tests;
- JSON/XML/YAML/schema files;
- README files;
- Markdown docs;
- examples and templates;
- generated or copied instruction files;
- CI/workflow inputs;
- command-line usage;
- migration or compatibility notes.

Examples of public contracts include:

- public .NET APIs;
- CLI arguments;
- workflow inputs and outputs;
- manifest fields;
- configuration keys;
- file formats;
- documented validation behavior;
- documented diagnostics and error behavior.

## Path, file, and security guardrails

For code that handles paths, files, archives, command execution, or generated output:

- treat user-controlled paths as untrusted;
- reject traversal rather than normalizing it away when the contract forbids traversal;
- distinguish logical repository paths from host OS paths;
- keep symlink, junction, and generated-file behavior explicit;
- avoid writing outside the intended root;
- avoid partial writes when planning can fail;
- document whether write behavior is truly transactional or only best-effort;
- never expose secrets, tokens, private keys, or local machine paths in generated output unless explicitly required and safe.

## Schema and configuration guardrails

When changing schema/configuration behavior:

- update the schema and runtime validation together;
- add direct schema validation tests when editor or CI validation depends on the schema;
- keep examples valid against the current schema;
- reject stale public fields when the contract removes them;
- document new required fields, optional fields, defaults, and invalid combinations;
- prefer explicit failure over silent fallback for malformed public configuration.

## Build and dependency guardrails

When touching build, package, CI, or dependency files:

- inspect repository-level files such as `.editorconfig`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`, solution files, project files, and workflow files;
- do not upgrade packages, SDKs, target frameworks, analyzers, or CI actions unless the task requires it;
- explain compatibility impact when such changes are necessary;
- run the smallest relevant validation command and report exact results.

## Test guardrails

Tests must validate the external behavior or documented invariant.

Do not:

- rewrite tests merely to fit a broken implementation;
- assert private implementation details unless the task explicitly requires white-box tests;
- combine unrelated behaviors into one broad test;
- use sleeps, timing races, shared mutable state, or order-dependent test behavior when deterministic alternatives exist.

Do:

- add regression tests for fixed bugs when practical;
- include boundary tests for invalid inputs, missing values, first/second item numbering, path edge cases, newline variants, non-ASCII input, and culture/casing behavior when relevant;
- keep tests readable as executable specifications.

## Documentation guardrails

Documentation is part of done when behavior, public surface, workflow, configuration, schema, diagnostics, caveats, or usage changes.

Documentation must:

- match implemented behavior;
- avoid stale examples;
- show current supported shapes;
- state important unsupported scenarios;
- describe migration impact when public contracts change;
- avoid presenting deferred or planned behavior as current.

If documentation was not updated, the completion report must explain why it was not needed.

## Completion report checklist

For guarded tasks, report:

- files changed;
- public contract impact;
- tests changed or added;
- documentation changed or intentionally not changed;
- validation commands and exact results;
- any unverified paths or tool limitations;
- intentionally retained legacy names or compatibility paths;
- unrelated dirty worktree state, if present;
- follow-up migrations or downstream updates.

<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section -->

<!-- END REPOSITORY SPECIFICS -->