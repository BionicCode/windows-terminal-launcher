---
Version: 1
Created: 2026-06-01T00:00:00+00:00
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

# Document Eligibility

`documentEligibility` is a current compatibility filter for manifest matches.

It filters include-selected candidates before analysis or mutation by extension, denied path, extensionless handling, strict UTF-8 decoding, and binary detection. It is currently implemented and present in the default manifest, but it should not be treated as a future governance redesign decision.

## Fields

| Field | Default behavior | Purpose |
| --- | --- | --- |
| [`allowedExtensions`](fields/allowed-extensions.md) | `.md`, `.markdown`, `.txt` | Base eligible document extensions. |
| [`additionalAllowedExtensions`](fields/additional-allowed-extensions.md) | `[]` | Extensions appended to the allowed set. |
| [`deniedExtensions`](fields/denied-extensions.md) | `[]` | Extensions that are always ineligible. |
| [`deniedPaths`](fields/denied-paths.md) | `[]` | Repository-relative path or glob patterns that are always ineligible. |
| [`allowExtensionless`](fields/allow-extensionless.md) | `false` | Allows files with no extension. |
| [`failOnIneligibleMatches`](fields/fail-on-ineligible-matches.md) | `false` | Fails validation when manifest globs match ineligible files. |

Unknown `documentEligibility` fields fail validation.

## Behavior Notes

- Denied extensions win over allowed extensions.
- Invalid UTF-8 and binary-like files are reported as ineligible and are not rewritten.
- When `failOnIneligibleMatches` is false, ineligible matches are reported without failing the run.
- When `failOnIneligibleMatches` is true, ineligible matches become validation failures.

## See Also

- [Manifest](manifest.md)
- [Include](include.md)
