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

## Purpose
This file refines the repository-wide `AGENTS.md` for work under the `test/` folder.

Follow user instructions first.
Follow the repository root `AGENTS.md` second.
This file adds stricter rules for automated tests and overrides broader guidance only for test code.

---

## Test Philosophy
- Treat test code as production-quality code for readability, maintainability, and review rigor.
- Prefer tests that are fast, isolated, repeatable, self-checking, and timely.
- Write tests as executable specifications of observable behavior and contract expectations.
- Prefer testing public behavior and observable outcomes over private implementation details.
- When a test is difficult to write or requires excessive setup, treat that as design feedback about the production code.

---

## Test Scope and Failure Discipline
- Each test should verify one behavior, one rule, one contract, or one failure mode.
- A test should have one clear reason to fail.
- Default to one logical assertion per test.
- Multiple assertions are allowed only when they verify one inseparable outcome and would fail for the same underlying defect.
- Prefer one Act step per test.
- If a test starts covering multiple cases, branches, outcomes, or repair paths, split it into separate tests or convert it into a parameterized test.
- Do not add extra assertions "for completeness" when they test separate behavior.
- If a failure would reasonably require multiple unrelated production fixes, the test is too broad and must be split.

---

## Independence and Isolation
- Tests must not depend on execution order.
- Tests must not depend on shared mutable state left behind by other tests.
- Tests must be able to run individually, repeatedly, and in parallel unless the repository explicitly defines a constrained integration-test path.
- Prefer fresh test data and fresh system-under-test instances per test.
- Use shared fixtures only when setup cost is meaningfully high and the shared fixture does not create hidden coupling between tests.
- When shared fixtures are necessary, keep fixture state immutable where practical or reset state explicitly between tests.
- Do not make one test responsible for preparing state that another test relies on.

---

## Naming Conventions
- Test names must be expressive enough that a reader can understand the scenario and expected outcome without opening the test body.
- Prefer the pattern `Should_<ExpectedBehavior>_When_<StateUnderTest>` for test method names.
- Good examples:
  - `Should_ThrowArgumentNullException_When_ArgumentIsNull`
  - `Should_ReturnEmptySequence_When_SourceHasNoItems`
  - `Should_SetIsReadOnlyToTrue_When_AccessModeIsReadOnly`
- Use domain language and contract language in test names.
- Prefer explicit names over vague names such as `Works`, `HandlesCase1`, `Test1`, or `HappyPath`.
- Do not encode incidental implementation details in the test name unless they are part of the contract being verified.
- For parameterized tests, keep the method name stable and expressive; let the supplied data express the case variations.
- Name test classes after the type, feature, or behavior under test, for example `<TypeName>Tests` or `<FeatureName>Tests`.

---

## Test Structure
- Use the Arrange / Act / Assert structure unless the framework or repository uses an equivalent pattern that is already well established.
- Keep Arrange focused on only the state required for the current test.
- Keep Act to a single behavioral trigger.
- Keep Assert explicit and behavior-oriented.
- Prefer helper methods, builders, or factory methods over bulky per-test setup duplication when they improve readability without hiding important state.
- Do not hide critical setup inside deep helper layers that make the scenario hard to see.
- Keep the happy path through the test readable from top to bottom.

---

## Assertions and Expected Outcomes
- Use assertions that make the expected behavior obvious.
- Prefer precise assertions over broad truthy checks.
- Assert the externally relevant outcome, not incidental implementation details.
- For exceptions, assert the specific exception type and any contract-relevant details when meaningful, such as parameter name.
- When checking returned state, prefer the smallest assertion set that proves the intended behavior.
- If verifying several independent properties of one returned object would create multiple reasons to fail, split the test by behavior instead of asserting every property in one method.
- Avoid assertion groups that bundle unrelated expectations into one test.

---

## Parameterized Tests and Data
- Use parameterized tests when the same behavior is exercised across multiple input/output pairs.
- Prefer parameterization over loops inside a test method.
- Do not combine unrelated scenarios into one parameterized test merely because the method signature allows it.
- Keep inline test data small, readable, and directly relevant to the behavior being verified.
- If test data becomes noisy or obscures intent, move it to well-named constants, factory methods, builders, or dedicated test-data helpers.
- Avoid magic values when a named constant would better explain intent.

---

## Test Logic and Readability
- Avoid branching, loops, computed expectations, and other logic inside tests when a simpler expression of the scenario is possible.
- Prefer explicit inputs and expected outputs over reconstructing production logic inside the test.
- Do not make the reader reverse-engineer a lambda, helper, or data source just to understand what is being tested.
- Use descriptive local names in tests; avoid cryptic abbreviations such as `sut2`, `obj`, `val`, `arr`, or single-letter lambda parameters when a clearer name is available.
- Prefer structure and naming over comments; add comments only when important intent or a non-obvious caveat would otherwise remain hidden.

---

## Test Doubles
- Use the simplest test double that satisfies the test.
- Prefer real collaborators when they are cheap, deterministic, and keep the test simple.
- Use stubs or fakes to control inputs.
- Use mocks or interaction verification only when the interaction itself is part of the behavior under test.
- Do not over-specify collaborator interactions when the observable result is what really matters.
- Do not verify every call simply because the mocking framework makes it easy.

---

## Async and Time-Sensitive Tests
- Test asynchronous code asynchronously.
- Prefer `Task` and `Task<T>`-based test flows; avoid `async void` in test code except where the framework explicitly requires it.
- Do not rely on arbitrary sleeps or timing races when a deterministic synchronization point is available.
- Abstract clocks, timers, randomness, and environment-dependent values when they affect repeatability.
- Keep time-sensitive tests deterministic enough that failures indicate a product issue or a real test-design issue rather than scheduler luck.

---

## What Not to Test Directly
- Do not write tests against private methods directly unless the user explicitly requests white-box tests.
- Prefer validating private implementation through the public or externally observable behavior that depends on it.
- Do not lock tests to incidental formatting, internal call order, or private data layout unless those are intentional parts of the contract.

---

## Organization Under `test/`
- Keep automated test projects under the repository root `test/` folder.
- Name generated unit test projects `<SolutionName>.Tests` unless the repository defines a more specific test-project naming convention.
- Keep test-only helpers, fixtures, builders, and sample inputs close to the owning test project unless the repository already defines a shared test-assets location.
- Separate true unit tests from slower integration, filesystem, network, database, UI, or end-to-end tests when the repository distinguishes them.
- If the repository uses multiple test types, keep the test type obvious from project name, folder path, or test metadata.

---

## Validation and Reporting
- When changing tests, report whether the relevant test scope passed.
- If a test fails because of an unrelated existing issue, state that explicitly and continue validating the remaining relevant scope where possible.
- Do not claim a test is flaky without evidence. Explain the suspected source of non-determinism.
- If a new or changed test requires unusual setup, explain why simpler alternatives were insufficient.
- If you intentionally keep multiple assertions in one test, be prepared to explain why they still represent one logical assertion and one reason to fail.

---

## Review Priorities for Test Code
When reviewing or generating tests, prioritize in this order:
1. Behavioral focus and one clear reason to fail
2. Independence and repeatability
3. Readability and naming quality
4. Correctness of assertions and expected outcomes
5. Minimal, relevant setup
6. Appropriate use of parameterization and test doubles
7. Execution speed relative to test type

<!-- BEGIN REPOSITORY SPECIFICS -->
<!-- Repository owners may edit only this section -->

<!-- END REPOSITORY SPECIFICS -->