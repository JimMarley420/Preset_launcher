# Development instructions

- Use the .NET 10 SDK specified in `global.json` and build the WinUI solution on Windows with the x64 configuration.

- Use Conventional Commits for every commit and PR title. Classify the actual user-facing change: fix/perf for corrections, feat for new functionality, and ! or BREAKING CHANGE for incompatibility. Use docs, ci, test, refactor, chore, build, style or revert only when the change warrants it.
- CircleCI computes versions from merged PR metadata. Never manually bump assembly/installer versions, create version tags or publish releases.
- The maintainer alone selects exactly one update label for releasing PRs: update: optional, update: recommended, or update: required. Do not choose or change this label on their behalf. Non-releasing PRs must have no update label.
- Keep English and French UI strings and README documentation in sync.
- Preserve existing tests and required CI checks. Report actual test results and unverified behavior; never weaken checks to make a pipeline green.
- Do not alter GitHub Actions. Use the existing CircleCI workflow and release procedure in docs/RELEASING.md.

## Public communication

Write pull requests, issues, commit messages, release notes and public documentation for a general project audience. Use concise, factual English, except for translated documentation and localized application strings. Do not address the repository owner by name, refer to private conversations, mention assistant activity or include personalized instructions. Describe deployment prerequisites as project requirements. Avoid promotional filler, unnecessary semicolons and decorative long dashes.

## Commit attribution

Maintainer-authorized automated commits use the GitHub identity `JimMarley420` with `186522763+JimMarley420@users.noreply.github.com`. Verify the effective author and committer identity before committing. Do not add unintended co-author trailers. Preserve legitimate human contributor attribution. Existing history must not be rewritten without explicit authorization.
