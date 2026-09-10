---
Version: 2
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

# Manifest API Reference

This page is the navigation index for the document metadata manifest contract. The schema remains the source of truth for validation.

## Object References

| Object | Purpose |
| --- | --- |
| [Manifest](reference/manifest.md) | Top-level manifest fields and validation boundaries. |
| [Defaults](reference/defaults.md) | Default metadata and presentation settings. |
| [Metadata](reference/metadata.md) | Managed metadata block settings. |
| [Presentation](reference/presentation.md) | Generated presentation settings. |
| [Include](reference/include.md) | Candidate file selection. |
| [Include entry](reference/include-entry.md) | Object include entry with scoped settings. |
| [Exclude](reference/exclude.md) | Candidate removal patterns. |
| [Document eligibility](reference/document-eligibility.md) | Current compatibility filter for manifest matches. |

## Metadata Fields

| Field | Reference |
| --- | --- |
| `format` | [format](reference/fields/format.md) |
| `placement` | [placement](reference/fields/placement.md) |
| `versionField` | [versionField](reference/fields/version-field.md) |
| `createdField` | [createdField](reference/fields/created-field.md) |
| `updatedField` | [updatedField](reference/fields/updated-field.md) |
| `authorField` | [authorField](reference/fields/author-field.md) |
| `versioningMode` | [versioningMode](reference/fields/versioning-mode.md) |
| `timestampFormat` | [timestampFormat](reference/fields/timestamp-format.md) |
| `commentStart` | [commentStart](reference/fields/comment-start.md) |
| `commentLinePrefix` | [commentLinePrefix](reference/fields/comment-line-prefix.md) |
| `commentEnd` | [commentEnd](reference/fields/comment-end.md) |

## Presentation Fields

| Field | Reference |
| --- | --- |
| `enabled` | [enabled](reference/fields/enabled.md) |
| `historyLimit` | [historyLimit](reference/fields/history-limit.md) |
| `includeSeparator` | [includeSeparator](reference/fields/include-separator.md) |
| `spacingBreaks` | [spacingBreaks](reference/fields/spacing-breaks.md) |

## Eligibility Fields

| Field | Reference |
| --- | --- |
| `allowedExtensions` | [allowedExtensions](reference/fields/allowed-extensions.md) |
| `additionalAllowedExtensions` | [additionalAllowedExtensions](reference/fields/additional-allowed-extensions.md) |
| `deniedExtensions` | [deniedExtensions](reference/fields/denied-extensions.md) |
| `deniedPaths` | [deniedPaths](reference/fields/denied-paths.md) |
| `allowExtensionless` | [allowExtensionless](reference/fields/allow-extensionless.md) |
| `failOnIneligibleMatches` | [failOnIneligibleMatches](reference/fields/fail-on-ineligible-matches.md) |

## Pattern Fields

| Field | Reference |
| --- | --- |
| include pattern / `pattern` | [include pattern](reference/fields/include-pattern.md) |
| exclude pattern | [exclude pattern](reference/fields/exclude-pattern.md) |

## See Also

- [Manifest guide](doc-metadata-manifest.md)
- [Document metadata overview](README.md)
