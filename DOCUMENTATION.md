---
Version: 1.3
Created: 2026-05-25T23:40:38+00:00
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

# DOCUMENTATION.md

## Purpose
This file defines repository documentation expectations for Codex and human contributors.

Documentation is part of the implementation. When behavior, public surface, or important design intent changes, update the relevant documentation in the same change.

---

## Documentation layers
Use the smallest documentation layer that fully explains the change:

1. **Source comments** for non-obvious implementation intent.
2. **XML documentation comments** for API surface.
3. **Markdown documentation** for consumer guidance, data structures, workflows, behavior notes, and examples. Markdown files have to go under top-level [/docs](/docs/) directory in the repository root. You can create subfolder if appropriate.

Do not push everything into one layer.

---

## 1) Source comments inside code
Use regular `//` comments deliberately and only when the code would otherwise hide important intent.

Add source comments for:
- design rationale,
- invariants that must remain true,
- algorithm behavior that is not obvious from the code shape,
- performance-sensitive trade-offs,
- protocol or format quirks,
- caveats and limitations,
- deliberately unsupported scenarios,
- edge-case handling,
- risk boundaries or failure modes,
- and why a seemingly strange implementation is necessary.

Do **not** add comments that merely narrate obvious syntax or restate a good symbol name.

Bad:
```csharp
// Checks if the sequence is empty.
if (!items.Any())
{
    ...
}
```

Good:
```csharp
// Materialize once because upstream enumeration is stateful and may read from a forward-only FIT stream.
var records = source.ToList();
```

Good:
```csharp
// Keep the original field order because downstream CSV consumers compare columns positionally.
```

Comment style:
- Prefer single-line `//` comments over block comments.
- Place the comment on its own line above the relevant code.
- Start with an uppercase letter.
- Keep comments concise but meaningful.
- When a limitation matters to callers or maintainers, state it explicitly.

Use `TODO`, `HACK`, or similar markers only when necessary, and include concrete context.

Better:
```csharp
// TODO: Preserve developer field units once the SDK decoder exposes the original scale metadata.
```

Worse:
```csharp
// TODO: Fix this.
```

---

## 2) XML documentation comments for APIs
Use XML documentation comments for public types and public members at a minimum. Also document protected or extension-oriented members when callers are expected to depend on them.

When XML docs are required:
- new public classes, records, structs, interfaces, enums, and delegates,
- new public constructors, methods, properties, events, and fields,
- changed public behavior,
- changed nullability, exception behavior, units, ordering guarantees, or mutation semantics.

Minimum XML documentation quality:
- Every documented API should have a clear `<summary>`.
- Use complete sentences.
- Document each parameter with `<param>` when parameters are non-trivial or behavior depends on them.
- Use `<returns>` for methods and properties where the return meaning is not already obvious.
- Use `<exception>` for meaningful documented exceptions.
- Use `<remarks>` for caveats, ordering guarantees, performance notes, mutability, threading, or format constraints.
- Use `<example>` when a short usage snippet materially helps consumers.

Prefer semantic XML tags over plain text:
- Use `<see cref="T:Namespace.TypeName"/>` or `<see cref="MemberName"/>` for code references.
- Use `<paramref name="value"/>` for parameter references.
- Use `<typeparamref name="T"/>` for generic type parameters.
- Use `<c>` for inline code and `<code>` for multi-line code.
- Use `<see langword="null"/>`, `<see langword="true"/>`, and similar language keywords instead of quoted plain text where appropriate.

Examples:

```csharp
/// <summary>
/// Tries to locate the session message that best represents the completed activity.
/// </summary>
/// <param name="messages">The decoded FIT messages in source order.</param>
/// <returns>
/// The selected session message, or <see langword="null"/> when the FIT file does not contain session data.
/// </returns>
/// <remarks>
/// When multiple session messages are present, the implementation currently prefers the last complete message.
/// This matches Garmin export behavior observed in the repository test data but may require revision for multi-sport activities.
/// </remarks>
public static SessionMesg? TryGetPrimarySession(IReadOnlyList<Mesg> messages)
```

```csharp
/// <summary>
/// Gets the average speed in meters per second.
/// </summary>
/// <remarks>
/// The value is the decoded FIT session field value before any user-facing unit conversion.
/// </remarks>
public double AverageSpeedMetersPerSecond { get; }
```

Avoid weak summaries such as:
- “Gets or sets the value.”
- “Initializes a new instance.” without saying what the type represents.
- “Does processing.”

If the repository enables XML documentation file generation, keep public XML docs warning-free.

---

## 3) Markdown documentation files
Add or update a small Markdown document when code comments and XML comments are not enough for a consumer or maintainer to understand how to use the API or data model.

Typical triggers:
- a new library surface,
- a new workflow,
- non-obvious data structures,
- serialization or file-format behavior,
- mapping rules,
- extension points,
- or behavior that benefits from short examples.

A Markdown doc should usually include:
- purpose,
- where the entry points are,
- important types or data structures,
- key invariants or assumptions,
- usage examples,
- caveats or unsupported cases,
- and versioning or compatibility notes when relevant.

Keep it small and practical. Favor one focused document over a vague wall of text.

Suggested outline:

```md
# Feature or API name

## Purpose

## Entry points

## Data model / important types

## Usage examples

## Guarantees and caveats

## Limitations / unsupported scenarios
```

---

## Choosing the right level
Use this rule of thumb:
- **Would a maintainer misunderstand why the code looks this way?** Add a source comment.
- **Would a caller misunderstand how to use the API?** Add or update XML docs.
- **Would a consumer need a bigger-picture explanation or example?** Add or update Markdown docs.

Often the correct answer is more than one layer.

---

## Documentation is part of done
For implementation tasks, documentation is part of done when relevant.

Before finishing, verify:
- comments explain non-obvious intent rather than narrate syntax,
- public APIs changed by the task have appropriate XML docs,
- `null`, `true`, `false`, and code symbols use semantic XML markup where appropriate,
- Markdown docs are updated when consumers need examples or data-model explanation,
- and no stale comments remain that contradict the implementation.

<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section -->

<!-- END REPOSITORY SPECIFICS -->
