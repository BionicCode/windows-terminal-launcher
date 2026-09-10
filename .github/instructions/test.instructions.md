---
Version: 1
Created: 2026-05-25T23:40:37+00:00
Updated: 2026-05-26T19:08:33+00:00
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

Follow the repository-wide engineering and validation standards defined in the root [AGENTS.md](../../test/AGENTS.md).
Treat that AGENTS.md as the source of truth.
The current file is based on that AGENTS.md the root folder "test" and may get outdated.

---
applyTo: "test/**/*"
---

Follow the test-folder rules for files under /test.

Key rules:
- Tests must be independent and order-independent.
- Each test should verify one behavior and have one logical reason to fail.
- Prefer one logical assertion per test; split tests when assertions verify different outcomes.
- Use expressive names in the form:
  Should_<ExpectedBehavior>_When_<StateUnderTest>
- Prefer parameterized tests only for the same behavior across multiple inputs.
- Do not hide scenarios behind excessive test helper indirection.
- Keep Arrange, Act, Assert structure clear.
- When this Copilot surface supports agent instructions, also follow /test/AGENTS.md.

<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section -->

<!-- END REPOSITORY SPECIFICS -->
