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

# Manifest

The document metadata manifest is `.github/tools/doc-metadata/doc-metadata-manifest.json`.

It is the current configuration surface for governed document metadata. The schema currently supports candidate selection, candidate removal, default output settings, scoped include overrides, and a compatibility eligibility filter.

## Fields

| Field | Required | Purpose |
| --- | --- | --- |
| `$schema` | yes | Schema URI. The default manifest uses `./doc-metadata-manifest.schema.json`. |
| `version` | yes | Manifest contract version. The only supported value is `1`. |
| [`defaults`](defaults.md) | yes | Default metadata and presentation settings. |
| [`documentEligibility`](document-eligibility.md) | no | Current compatibility filter for manifest matches. |
| [`include`](include.md) | yes | Candidate governed file patterns or scoped include entries. |
| [`exclude`](exclude.md) | yes | Repository-root-relative patterns removed from candidate participation. |

Unknown top-level fields fail validation.

## Behavior Notes

- `include` and `exclude` are selection inputs.
- `defaults` and scoped include settings control how selected files are written.
- `documentEligibility` is implemented today and filters manifest matches by extension, denied path, and text eligibility. It is documented as current behavior, not as a future governance design decision.

## See Also

- [Manifest guide](../doc-metadata-manifest.md)
- [Manifest API reference](../doc-metadata-manifest-api.md)
