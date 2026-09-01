# Development Workflow

This repository uses a small, repeatable pipeline so each Codex session can work from repository context instead of relying on chat history.

## Branch model

- `main` should remain the known-good baseline.
- Each implementation or fix uses a dedicated short-lived branch.
- Prefer one concern per PR.
- Do not mix cleanup, features, and unrelated fixes unless they are inseparable.

Suggested branch names:

- `foundation/wpf-project`
- `feature/core-overlay-interaction`
- `hardening/pre-test`
- `fix/<short-problem-name>`

## Standard task pipeline

### 1. Orient

Before editing:

- read `AGENTS.md`,
- read `docs/PROJECT_PLAN.md`,
- inspect current `main`,
- inspect open/recent PRs when relevant,
- identify the current phase and the exact task boundary.

### 2. Plan the smallest change

State internally what files/areas need to change and what is deliberately out of scope.

Do not redesign the project while implementing a narrow task.

### 3. Implement

- use the existing architecture,
- keep dependencies minimal,
- keep platform-specific/native code isolated,
- comment only where behavior would otherwise be non-obvious, especially Win32 hit testing/DPI details.

### 4. Verify

For code changes, run the strongest checks available in the environment:

- restore,
- build,
- tests if present,
- Release build,
- publish when packaging is affected.

Inspect warnings and generated output. Do not report a check as passed if it could not be run.

### 5. Self-audit

Before opening a PR, review the diff for:

- scope creep,
- accidental behavior changes,
- unnecessary dependencies,
- dead/debug code,
- hard-coded monitor/DPI assumptions,
- input/focus regressions,
- portability regressions,
- documentation that is now stale.

### 6. Open PR

PR description should contain:

- what changed,
- why,
- verification actually performed,
- manual verification still required,
- known limitations/risks.

Keep the description practical, not ceremonial.

### 7. Audit before merge

Review the PR against the current phase acceptance criteria and `AGENTS.md`.

If an issue is found, iterate on the same branch/PR until the task is actually complete. Merge only after the relevant checks are green and the change is coherent.

## Manual-test boundary

Build/CI success is not proof that transparent hit testing behaves correctly on Windows.

For interaction-sensitive work, PR notes must explicitly say **manual Windows test required** until a human has tested it on the target machine.

## Documentation policy

The repository documentation is durable project memory.

Update it when:

- requirements change,
- a technical decision changes,
- packaging changes,
- a known limitation becomes important,
- the project moves to a materially different phase.

Do not update documentation merely to create activity. Keep it short enough that an agent can reread it every task.

## Release/publish policy

For the MVP, prioritize a portable Windows build over installer infrastructure.

Before treating a build as test-ready:

- build Release,
- publish using the documented configuration,
- confirm the expected executable/output exists,
- record whether the build is framework-dependent or self-contained,
- record approximate output size if self-contained,
- do not claim compatibility with locked-down corporate PCs without testing their policy.

## Immediate pipeline

Current planned sequence:

`docs baseline -> foundation -> core interaction -> pre-test hardening -> real-machine test -> fixes -> MVP polish/release candidate`

Do not skip directly to optional polish before the first real-machine test.
