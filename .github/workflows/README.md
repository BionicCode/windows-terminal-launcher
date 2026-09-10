# Workflow Documentation

This directory contains the repository's GitHub Actions workflow definitions and their workflow documentation.

> [!IMPORTANT]
> [repository-maintenance.yml](repository-maintenance.yml) is the central trigger owner for repository maintenance. The child workflows in this directory are documented as passive/reusable workflows unless they are run directly with `workflow_dispatch`.

## Workflow Overview

| Workflow display name | File | Summary |
| --- | --- | --- |
| [Repository maintenance](documentation/repository-maintenance.md) | [repository-maintenance.yml](repository-maintenance.yml) | Central orchestrator that owns maintenance triggers, normalizes event context, runs document metadata first, and runs managed-file sync last under an explicit skip/failure contract. |
| [Document versioning](documentation/doc-metadata.md) | [doc-metadata.yml](doc-metadata.yml) | Passive reusable workflow that analyzes governed document metadata, performs safe repairs when allowed, and enforces the final metadata status gate. |
| [Sync managed files](documentation/sync-managed-files.md) | [sync-managed-files.yml](sync-managed-files.yml) | Passive reusable local wrapper that inspects the sync manifest and delegates init or sync execution to the shared workflow in `BionicCode/workflows`. |

## Directory Structure

- Workflow YAML files live directly in this directory.
- Per-workflow documentation lives under [documentation/](documentation/).

## Maintenance Notes

- Update this README when workflow files are added, removed, renamed, or materially repurposed.
- Keep the overview table aligned with the current `name:` field and actual workflow behavior in the YAML files.
- Do not document planned workflows here before the corresponding YAML file exists.