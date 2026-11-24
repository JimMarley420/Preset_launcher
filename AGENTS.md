# Development instructions

- Use Conventional Commits for every commit and PR title. Classify the actual user-facing change: fix/perf for corrections, feat for new functionality, and ! or BREAKING CHANGE for incompatibility. Use docs, ci, test, refactor, chore, build, style or revert only when the change warrants it.
- CircleCI computes versions from merged PR metadata. Never manually bump assembly/installer versions, create version tags or publish releases.
- The maintainer alone selects exactly one update label for releasing PRs: update: optional, update: recommended, or update: required. Do not choose or change this label on their behalf. Non-releasing PRs must have no update label.
- Keep English and French UI strings and README documentation in sync.
- Preserve existing tests and required CI checks. Report actual test results and unverified behavior; never weaken checks to make a pipeline green.
- Do not alter GitHub Actions. Use the existing CircleCI workflow and release procedure in docs/RELEASING.md.
