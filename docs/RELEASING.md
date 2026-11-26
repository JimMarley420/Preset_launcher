# Releases

## Versioning

GitHub's latest published stable release is the baseline. The existing 1.2.6 tag has no v prefix. New tags use vMAJOR.MINOR.PATCH. CircleCI reads merged PR titles and labels after the baseline tag and selects the highest required bump. On the initial migration from 1.2.6, commits through 8535d8e943a6503d4982ff9a5164ef66392ba3ea are grandfathered. Later commits must map to a merged PR.

| Commit | Bump | Example from 1.2.6 |
| --- | --- | --- |
| fix: or perf: | Patch | 1.2.7 |
| feat: | Minor | 1.3.0 |
| feat!:, fix!: or BREAKING CHANGE: footer | Major | 2.0.0 |
| docs:, ci:, test:, refactor:, chore:, build: only | None | No publication |

Use a Conventional Commit PR title and preserve it when squash-merging. A user-facing behavior fix is a fix, even if implemented by refactoring. Invalid PR titles and direct or ambiguous main commits block release planning. Multiple changes between releases produce one highest bump.

The release plan in artifacts/release-plan.json is the source of the published assembly version, installer version, registry DisplayVersion and asset filename. Branch CI artifacts are not production releases. Local builds default to 0.0.0 and automatic update checks are disabled for that version.

## Pull request validation

The validate-pr-title job checks the actual open PR title and stacked-PR eligibility. Validate-update-label independently checks the maintainer-selected update policy via the public GitHub API. Only validate-pr-title must succeed before plan-release. Open PRs targeting main use a provisional optional policy for releasing changes and no policy for non-releasing changes, regardless of their update labels. This does not assign a GitHub label. Branch plans are marked validation-only and cannot be published. The label check remains a required merge check; production planning and publication still validate actual merged PR metadata. If the title is invalid, the label check explicitly defers classification to the title check. Open a PR before running it. A stacked PR from this repository can validate against another branch when its title is non-releasing. Production versioning still uses PRs merged into main. A releasing PR must have exactly one of the three update labels below. Non-releasing PRs must have none. Only the maintainer selects labels.

After changing a title, rerun the workflow. After changing only an update label on a PR targeting main, rerun the failed label check using CircleCI's rerun-from-failed workflow option where available. Successful technical jobs do not depend on that check and need not be rerun solely because the label changed. Metadata changes do not change the commit SHA and may not trigger CircleCI automatically. A successful commit status cannot guarantee that subsequently edited metadata remains valid at merge time. Release planning on main resolves merged PRs by their squash commit. Publication recomputes the plan before creating a draft and again before publishing. A changed decision blocks publication until a fresh pipeline validates it. Do not change labels on merged PRs pending publication unless intentionally correcting a release decision.

## Update policy

| GitHub label | Behavior |
| --- | --- |
| update: optional | Install, later, or skip this version. |
| update: recommended | Install or remind in 24 hours. No permanent skip. |
| update: required | Install or exit. |

There are no policy footers in commits. The generated release notes include two machine-readable comments: presetlauncher:min-supported and presetlauncher:update-policy. The strongest selected policy wins if multiple PRs are included in one release. Required releases advance the minimum supported version. Later optional or recommended releases preserve that floor. Users below it must still update.

Recommended reminders are version-specific and stored in settings. Automatic checks at startup respect the 24-hour delay. Manual checks and mandatory updates bypass it. A newer release is not suppressed by an old reminder. There is no background reminder while the application stays open.

Known mandatory floors survive settings reset and remain enforced when GitHub cannot be reached. If no mandatory floor is known and GitHub is unavailable, the offline app remains usable. This is application policy, not a tamper-resistant security boundary.

Reserve required updates for security, data integrity, compatibility or a deliberately necessary migration, not routine cosmetic changes.

## CircleCI publication

1. In GitHub Settings > Developer settings > Personal access tokens > Fine-grained tokens, create a token restricted to Preset_launcher with Contents: Read and write. Set an expiry and rotate the token. Do not grant workflow, administration or organization permissions.
2. In CircleCI Organization Settings > Contexts create presetlauncher-release. Restrict it to this project and authorized maintainers where supported. Configure the expression restriction:

       pipeline.git.branch == "main" and not job.ssh.enabled

   Verify that restriction is supported and enforced by your integration before enabling publication. Store the token as GH_RELEASE_TOKEN only in this context. Never put it in project-level variables, checkout, test or fork jobs.
3. Protect main with required reviews and the actual CI check names from a successful branch build. Require reviewed changes to CircleCI and release scripts. Enable immutable releases if available.
4. Merge only after every branch CI job succeeds. A main pipeline calculates the version, builds, tests, packages and checks fresh install and upgrade. Publication is serialized and rejects stale plans or an advanced main branch. Documentation-only changes do not publish.
5. CircleCI creates a draft, uploads the three validated installers and SHA256SUMS.txt in deterministic order, verifies the GitHub asset sizes and digests, then publishes it as latest. GitHub generates release notes. Never overwrite published assets or reuse a version.

GitHub API reads retry transient HTTP and network errors up to three times with exponential backoff and jitter. The cumulative sleep budget is 30 seconds per request, including actual sleep time. Requests with rate-limit responses require valid server timing within the remaining budget. Without it, GitHub's minimum 60-second secondary-limit wait exceeds the budget and the request fails. The 30-second socket timeout is not an end-to-end deadline. Release creation, asset uploads and publication are not retried automatically because the outcome of a failed write may be ambiguous. The final main ref check follows release decision collection immediately before publication. The check and PATCH are not atomic.

Only publish-release receives the token. PRs and tags cannot publish. Do not require the main-only publication job on PRs.

If publication fails after creating a draft, inspect the draft and its target commit and assets. Existing tags and drafts are deliberately rejected. Delete only an identified abandoned draft and its unpublished tag if present and no published release references it. Then trigger a fresh main pipeline. If main or the latest release advanced, trigger a fresh pipeline rather than rerunning only the stale publication job.


### PROD ruleset

Require these eight CircleCI status checks in the PROD ruleset.

- `ci/circleci: plan-release`
- `ci/circleci: dependency-audit`
- `ci/circleci: build-windows`
- `ci/circleci: test-windows`
- `ci/circleci: package-installer`
- `ci/circleci: validate-pr-title`
- `ci/circleci: validate-update-label`
- `ci/circleci: smoke-installer`

CircleCI validates on one Windows VM. The named Linux gates require Windows evidence bound to the commit, version and validation lane. The package and publication checks independently hash the installers and verify the exact asset set. Keep the eight required names and the separately serialized publication job. See [CI lanes and measurement](DEVELOPMENT.md#continuous-integration).

Main alone writes verified SDK and dependency caches. Use the existing seven-day retention for new caches. Check usage and cap workspace retention at the period required for workflow retries. The pipeline retains a workspace copy for validation/publication and keeps test reports and smoke logs. GitHub Releases is the permanent distribution location.

## Migration from the legacy update server

Installed versions through 1.2.6 cannot discover GitHub updates on their own. After the first verified release is published:

- Download its exact installer from GitHub and verify SHA256SUMS.txt.
- Copy it to the old host at /PresetLauncher/PresetLauncher_Setup_<version>.exe, using the numeric filename without v.
- Only once the installer is reachable, update /PresetLauncher/version.txt to the numeric version.
- Test the legacy updater on an isolated Windows VM and verify that data survives and the new app checks GitHub. Keep this bridge available for users who return later, or instruct them to install the latest release manually.

CI does not change the old host. Do not shut it down prematurely. Older clients cannot enforce the new required-update policy until upgraded.

## Verification and security

Full validation checks PE machine values for each published app host and key self-contained native libraries. The x64 worker installs, starts and removes x64 and x86 in sequence with isolated state. Windows UI Automation checks that startup has enabled the preset controls and that the installed app can see the fixture preset. The production data and settings readers validate the same fixture in the regression suite. Smoke checks registration, shortcuts and preservation of presets, history, statistics, preferences and both protected policy files. It upgrades from the authenticated matching predecessor where one exists. The first release of each new architecture uses fresh-install smoke. The x64 host checks ARM64 setup rejection. Obtain maintainer approval after native ARM64 smoke on a supported Windows ARM64 device before calling that target fully verified.

The new updater checks GitHub's HTTPS metadata and verifies downloaded bytes. A checksum is not independent code signing: configure Authenticode signing before claiming publisher verification or SmartScreen trust. Limit repository and publication access.

## Update signing key

Before merging the signed-updater change, generate the production P-256 key outside the repository on a trusted machine. Keep a secure offline backup. Never commit the private key or paste it into an issue, PR or chat.

```bash
umask 077
keydir="$(mktemp -d)"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out "$keydir/update-private.pem"
openssl pkey -in "$keydir/update-private.pem" -pubout -out .circleci/update-policy-public.pem
```

Commit only `.circleci/update-policy-public.pem` to the updater branch. Store the base64 encoding of the private PEM as `UPDATE_SIGNING_KEY_B64` in the existing restricted `presetlauncher-release` context. Use a trusted local environment to encode and transfer it directly to the CircleCI secret field. Do not print it in CI logs. Remove the temporary private file after securely backing it up.

Publication emits exactly one legacy v1 ECDSA P-256 signature and keeps the unsuffixed x64 filename and original policy comments. Existing x64 update clients can verify that installer unchanged.

New clients require `presetlauncher:manifest-v2` in the release body. It contains base64-encoded signed message bytes, a dot and a base64 DER ECDSA P-256/SHA-256 signature. The signed message begins with ASCII `presetlauncher-update-v2` and an LF, followed by compact ASCII JSON with `version`, `minimum_supported_version`, `policy` and `installers`. Installer entries are ordered x64, x86, ARM64, each with `architecture`, `name`, `size` and lowercase `sha256`. Clients verify the original bytes with the existing pinned key, apply the authenticated policy floor and then match the process architecture and GitHub asset metadata. A missing or invalid asset leaves the authenticated minimum in effect. A v1 marker alone cannot authorize a download by a v2 client.

Signed predecessors are checked under their published protocol before reusing their floors and installers. The historical unsigned exceptions are limited to 1.2.6 and v1.3.0. The same restricted release context signs both formats.

The signing key is pinned. A replacement key requires a compatible client migration and transition plan before switching the signer. Clients that miss the transition may require a manual installer. Do not replace the CircleCI secret alone.

The protected local policy is stored under `%LocalAppData%\\PresetLauncher\\Security\\update-policy.dat` using Windows DPAPI for the current user. The old minimum in settings is imported only on first initialization. The lock file also records initialization, so deleting the policy file alone blocks startup. A corrupted protected file blocks startup. See the recovery instructions in [SECURITY.md](../SECURITY.md). DPAPI does not prevent a user who controls the machine from deleting, replaying or recreating their own local state or modifying the executable.
