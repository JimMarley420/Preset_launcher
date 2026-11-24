# Releases

## Versioning

GitHub's latest published stable release is the baseline. The existing 1.2.6 tag has no v prefix; new tags use vMAJOR.MINOR.PATCH. CircleCI reads merged PR titles and labels after the baseline tag and selects the highest required bump. On the initial migration from 1.2.6, commits through 8535d8e943a6503d4982ff9a5164ef66392ba3ea are grandfathered; later commits must map to a merged PR.

| Commit | Bump | Example from 1.2.6 |
| --- | --- | --- |
| fix: or perf: | Patch | 1.2.7 |
| feat: | Minor | 1.3.0 |
| feat!:, fix!: or BREAKING CHANGE: footer | Major | 2.0.0 |
| docs:, ci:, test:, refactor:, chore:, build: only | None | No publication |

Use a Conventional Commit PR title and preserve it when squash-merging. A user-facing behavior fix is a fix, even if implemented by refactoring. Invalid PR titles and direct or ambiguous main commits block release planning. Multiple changes between releases produce one highest bump.

The release plan in artifacts/release-plan.json is the source of the published assembly version, installer version, registry DisplayVersion and asset filename. Branch CI artifacts are not production releases. Local builds default to 0.0.0 and automatic update checks are disabled for that version.

## Pull request validation

The required validate-pr-title job checks the actual open PR title and labels via the public GitHub API. Open a PR before running it. A stacked PR from this repository can validate against another branch when its title is non-releasing. Production versioning still uses PRs merged into main. A releasing PR must have exactly one of the three update labels below; non-releasing PRs must have none. Only the maintainer selects labels.

After changing a title or label, rerun the workflow: metadata changes do not change the commit SHA and may not trigger CircleCI automatically. A successful commit status cannot guarantee that subsequently edited metadata remains valid at merge time. Release planning on main resolves merged PRs by their squash commit; publication recomputes the plan before creating a draft and again before publishing. A changed decision blocks publication until a fresh pipeline validates it. Do not change labels on merged PRs pending publication unless intentionally correcting a release decision.

## Update policy

| GitHub label | Behavior |
| --- | --- |
| update: optional | Install, later, or skip this version. |
| update: recommended | Install or remind in 24 hours; no permanent skip. |
| update: required | Install or exit. |

There are no policy footers in commits. The generated release notes include two machine-readable comments: presetlauncher:min-supported and presetlauncher:update-policy. The strongest selected policy wins if multiple PRs are included in one release. Required releases advance the minimum supported version; later optional or recommended releases preserve that floor. Users below it must still update.

Recommended reminders are version-specific and stored in settings. Automatic checks at startup respect the 24-hour delay; manual checks and mandatory updates bypass it. A newer release is not suppressed by an old reminder. There is no background reminder while the application stays open.

Known mandatory floors survive settings reset and remain enforced when GitHub cannot be reached. If no mandatory floor is known and GitHub is unavailable, the offline app remains usable. This is application policy, not a tamper-resistant security boundary.

Reserve required updates for security, data integrity, compatibility or a deliberately necessary migration, not routine cosmetic changes.

## CircleCI publication

1. In GitHub Settings > Developer settings > Personal access tokens > Fine-grained tokens, create a token restricted to Preset_launcher with Contents: Read and write. Set an expiry and rotate the token. Do not grant workflow, administration or organization permissions.
2. In CircleCI Organization Settings > Contexts create presetlauncher-release. Restrict it to this project and authorized maintainers where supported. Configure the expression restriction:

       pipeline.git.branch == "main" and not job.ssh.enabled

   Verify that restriction is supported and enforced by your integration before enabling publication. Store the token as GH_RELEASE_TOKEN only in this context. Never put it in project-level variables, checkout, test or fork jobs.
3. Protect main with required reviews and the actual CI check names from a successful branch build. Require reviewed changes to CircleCI and release scripts. Enable immutable releases if available.
4. Merge only after every branch CI job succeeds. A main pipeline calculates the version, builds, tests, packages and checks fresh install and upgrade. Publication is serialized and rejects stale plans or an advanced main branch. Documentation-only changes do not publish.
5. CircleCI creates a draft, uploads the exact smoke-tested installer and SHA256SUMS.txt, verifies the GitHub asset digests, then publishes it as latest. GitHub generates release notes. Never overwrite published assets or reuse a version.

Only publish-release receives the token. PRs and tags cannot publish. Do not require the main-only publication job on PRs.

If publication fails after creating a draft, inspect the draft and its target commit and assets. Existing tags and drafts are deliberately rejected. Delete only an identified abandoned draft and its unpublished tag if present and no published release references it. Then trigger a fresh main pipeline. If main or the latest release advanced, trigger a fresh pipeline rather than rerunning only the stale publication job.


### PROD ruleset

The active PROD ruleset for the default branch requires these CircleCI checks. The optimized workflow keeps all five names and the same CircleCI integration, so the ruleset needs no change.

- `ci/circleci: plan-release`
- `ci/circleci: dependency-audit`
- `ci/circleci: build-windows`
- `ci/circleci: test-windows`
- `ci/circleci: package-installer`

CircleCI also reports `validate-pr-title` and `smoke-installer` individually. The installer jobs report success on fast PRs after checking and logging the lane decision. Windows application compilation still runs in `test-windows`. The main workflow requires a real self-contained build, package integrity verification, and smoke testing before GitHub release publication. Keep `publish-release` outside the required PR checks because it runs only on main.

## Migration from the legacy update server

Installed versions through 1.2.6 cannot discover GitHub updates on their own. After the first verified release is published:

- Download its exact installer from GitHub and verify SHA256SUMS.txt.
- Copy it to the old host at /PresetLauncher/PresetLauncher_Setup_<version>.exe, using the numeric filename without v.
- Only once the installer is reachable, update /PresetLauncher/version.txt to the numeric version.
- Test the legacy updater on an isolated Windows VM and verify that data survives and the new app checks GitHub. Keep this bridge available for users who return later, or instruct them to install the latest release manually.

CI does not change the old host. Do not shut it down prematurely. Older clients cannot enforce the new required-update policy until upgraded.

## Verification and security

The smoke test installs and removes the new package. On full-lane PR branches and for a planned release on main it also verifies the previous public installer digest, installs it, upgrades in place, and checks application version, registry and preservation of disposable user data. It verifies the bundled .NET 10 runtime and the executable and assembly file versions. The smoke test also waits for the installed WinUI main window to initialize after each installation. Before the first release, manually test both languages, optional skip, the 24-hour recommended reminder, mandatory update, offline retry, download errors, canceled installation and the legacy bridge in a disposable Windows VM.

The new updater checks GitHub's HTTPS metadata and verifies downloaded bytes. A checksum is not independent code signing: configure Authenticode signing before claiming publisher verification or SmartScreen trust. Limit repository and publication access.
