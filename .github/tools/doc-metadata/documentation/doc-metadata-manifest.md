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

# Document Metadata Manifest

> [!NOTE]
> See the [API reference](doc-metadata-manifest-api.md) for object and field reference pages.

## Purpose

[doc-metadata-manifest.json](../doc-metadata-manifest.json) defines candidate files, candidate removals, metadata defaults, presentation defaults, scoped include settings, and current compatibility eligibility rules.

It is the repository-local configuration surface for document metadata.

## Minimal Shape

```json
{
  "$schema": "./doc-metadata-manifest.schema.json",
  "version": 1,
  "defaults": {
    "metadata": {
      "format": "yaml-front-matter",
      "placement": "top",
      "versionField": "Version",
      "createdField": "Created",
      "updatedField": "Updated",
      "authorField": "Author",
      "versioningMode": "body-content-change",
      "timestampFormat": "rfc3339-utc"
    },
    "presentation": {
      "enabled": true,
      "historyLimit": 20,
      "includeSeparator": true,
      "spacingBreaks": 2
    }
  },
  "documentEligibility": {
    "allowedExtensions": [".md", ".markdown", ".txt"],
    "additionalAllowedExtensions": [],
    "deniedExtensions": [],
    "deniedPaths": [],
    "allowExtensionless": false,
    "failOnIneligibleMatches": false
  },
  "include": ["README.md"],
  "exclude": []
}
```

## Participation Flow

1. [`include`](reference/include.md) selects candidate files.
2. [`exclude`](reference/exclude.md) removes candidates from broad include patterns.
3. [`documentEligibility`](reference/document-eligibility.md) currently filters candidates by extension, denied path, strict UTF-8 text decoding, and binary detection.
4. [`defaults`](reference/defaults.md) and scoped include settings determine metadata and presentation output.

`documentEligibility` is current implemented behavior, but it is not documented here as the preferred future governance model.

## Include Entries

String include entries use defaults:

```json
{
  "include": [
    "README.md",
    "*AGENT*.md",
    "docs/**/*.markdown"
  ]
}
```

Object include entries use [`pattern`](reference/fields/include-pattern.md) plus scoped settings:

```json
{
  "include": [
    {
      "pattern": "src/*AGENT*.md",
      "presentation": {
        "historyLimit": 30,
        "includeSeparator": false,
        "spacingBreaks": 1
      }
    }
  ]
}
```

If multiple include entries match the same file, their effective configuration must be identical. Conflicting matches fail validation instead of being silently merged.

## Broad Include With Exclude

```json
{
  "include": ["docs/**/*"],
  "exclude": ["docs/generated/**"]
}
```

Use `exclude` for intentionally broad globs. Do not rely on workflow `paths` filters to define governed files.

## Plain Text Defaults

`.txt` files are eligible by default, but rich Markdown presentation is disabled by convention. They receive compact metadata and a plain separator with physical blank lines.

```json
{
  "include": [
    {
      "pattern": "**/*.txt",
      "presentation": {
        "enabled": false,
        "historyLimit": 0,
        "includeSeparator": true,
        "spacingBreaks": 2
      }
    }
  ]
}
```

## See Also

- [API reference](doc-metadata-manifest-api.md)
- [Manifest](reference/manifest.md)
- [Metadata](reference/metadata.md)
- [Presentation](reference/presentation.md)
- [Document eligibility](reference/document-eligibility.md)
