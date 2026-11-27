# Releases

## Versioning

GitHub's latest published stable release is the baseline. The existing 1.2.6 tag has no v prefix. New tags use vMAJOR.MINOR.PATCH. CircleCI reads merged PR titles and labels after the baseline tag and selects the highest required bump. On the initial migration from 1.2.6, commits through 8535d8e943a6503d4982ff9a5164ef66392ba3ea are grandfathered. Later commits must map to a merged PR.

| Commit | Bump | Example from 1.2.6 |
| --- | --- | --- |
| fix: or perf: | Patch | 1.2.7 |
| feat: | Minor | 1.3.0 |
| feat!:, fix!: or BREAKING CHANGE: footer | Major | 2.0.0 |
| docs:, ci:, test:, refactor:, chore:, build: only | None | No publication |

Release classification uses the Conventional Commit PR title, which is preserved by squash merges. User-facing corrections are classified as fixes, including those implemented through refactoring. Invalid PR titles and direct or ambiguous main commits block release planning. Multiple changes between releases produce one highest bump.

The release plan in artifacts/release-plan.json is the source of the published assembly version, installer version, registry DisplayVersion and asset filename. Branch CI artifacts are not production releases. Local builds default to 0.0.0 and automatic update checks are disabled for that version.

## Pull request validation

The validate-pr-title job checks the actual open PR title and stacked-PR eligibility. Validate-update-label independently checks the maintainer-selected update policy via the public GitHub API. Only validate-pr-title must succeed before plan-release. Open PRs targeting main use a provisional optional policy for releasing changes and no policy for non-releasing changes, regardless of their update labels. This does not assign a GitHub label. Branch plans are marked validation-only and cannot be published. The label check remains a required merge check; production planning and publication still validate actual merged PR metadata. If the title is invalid, the label check explicitly defers classification to the title check. Validation requires an eligible open PR. A stacked PR from this repository can validate against another branch when its title is non-releasing. Production versioning still uses PRs merged into main. A releasing PR must have exactly one of the three update labels below. Non-releasing PRs must have none. Only the maintainer selects labels.

Title changes require a new validation run. An update-label-only change on a PR targeting main requires rerunning the label check; CircleCI's rerun-from-failed option can reuse successful technical jobs. Successful technical jobs do not depend on that check and need not be rerun solely because the label changed. Metadata changes do not change the commit SHA and may not trigger CircleCI automatically. A successful commit status cannot guarantee that subsequently edited metadata remains valid at merge time. Release planning on main resolves merged PRs by their squash commit. Publication recomputes the plan before creating a draft and again before publishing. A changed decision blocks publication until a fresh pipeline validates it. Changes to labels on merged PRs pending publication are treated as release decision corrections.

## Update policy

| GitHub label | Behavior |
| --- | --- |
| update: optional | Install, later, or skip this version. |
| update: recommended | Install or remind in 24 hours. No permanent skip. |
| update: required | Install or exit. |

There are no policy footers in commits. The generated release notes include two machine-readable comments: presetlauncher:min-supported and presetlauncher:update-policy. The strongest selected policy wins if multiple PRs are included in one release. Required releases advance the minimum supported version. Later optional or recommended releases preserve that floor. Users below it must still update.

Recommended reminders are version-specific and stored in settings. Automatic checks at startup respect the 24-hour delay. Manual checks and mandatory updates bypass it. A newer release is not suppressed by an old reminder. There is no background reminder while the application stays open.

Known mandatory floors survive settings reset and remain enforced when GitHub cannot be reached. If no mandatory floor is known and GitHub is unavailable, the offline app remains usable. This is application policy, not a tamper-resistant security boundary.

Required updates are reserved for security, data integrity, compatibility and necessary migrations.

## CircleCI publication

### Publication credentials

The `presetlauncher-release` context is restricted to this project and authorized maintainers. Its expression restriction limits access to main pipelines without SSH debugging:

    pipeline.git.branch == "main" and not job.ssh.enabled

| Secret | Purpose |
| --- | --- |
| `GH_RELEASE_TOKEN` | Repository-scoped fine-grained GitHub token with Contents read/write access. |
| `UPDATE_SIGNING_KEY_B64` | Base64-encoded private P-256 update signing key. |

The context restriction must be enforced by the CircleCI integration. Release credentials are not available to checkout, validation or fork jobs. The token has an expiry and does not require workflow, administration or organization permissions.

### Publication lifecycle

A squash merge into main triggers release planning. Releasing changes require the Windows build, regression suite, packaging and installer smoke checks. Non-releasing changes do not create a release. Publication is serialized and rejects stale plans or an advanced main branch.

The publisher creates a draft, uploads the three validated installers and `SHA256SUMS.txt` in deterministic order, verifies asset sizes and digests, then publishes the draft as latest. Published assets and version tags are immutable under the release procedure.

The publisher appends application-facing notes to the full GitHub-generated changelog. It uses each unique PR with a positive rank in the validated release plan, in plan order. A PR description may supply a one-line user-facing summary of up to 240 characters between standalone `<!-- presetlauncher-summary:begin -->` and `<!-- presetlauncher-summary:end -->` markers. Otherwise the publisher uses the PR title without its Conventional Commit prefix.

The client reads one section between standalone `<!-- presetlauncher-app-notes:v1:begin -->` and `<!-- presetlauncher-app-notes:v1:end -->` markers. It shows a localized GitHub fallback for legacy releases and unusable sections. The notes describe the offered release and use the language supplied by the source PRs. The signed update metadata is verified independently.

GitHub API reads retry transient HTTP and network errors up to three times with exponential backoff and jitter. The cumulative sleep budget is 30 seconds per request, including actual sleep time. Requests with rate-limit responses require valid server timing within the remaining budget. Without it, GitHub's minimum 60-second secondary-limit wait exceeds the budget and the request fails. The 30-second socket timeout is not an end-to-end deadline. Release creation, asset uploads and publication are not retried automatically because the outcome of a failed write may be ambiguous. The final main ref check follows release decision collection immediately before publication. The check and PATCH are not atomic.

Only `publish-release` receives the release token. PRs and tags cannot publish; the main-only publication job is not a required PR check.

If publication fails after creating a draft, inspect the draft and its target commit and assets. Existing tags and drafts are deliberately rejected. Delete only an identified abandoned draft and its unpublished tag if present and no published release references it. Then trigger a fresh main pipeline. If main or the latest release advanced, trigger a fresh pipeline rather than rerunning only the stale publication job.


### PROD ruleset

The PROD ruleset requires nine CircleCI status checks:

- `ci/circleci: plan-release`
- `ci/circleci: dependency-audit`
- `ci/circleci: build-windows`
- `ci/circleci: test-windows`
- `ci/circleci: package-installer`
- `ci/circleci: validate-pr-title`
- `ci/circleci: validate-update-label`
- `ci/circleci: smoke-installer`
- `ci/circleci: validate-windows`

CircleCI uses dynamic configuration (`.circleci/config.yml` continues into `.circleci/continue.yml`). The setup job selects a Linux-only lane only for allowlisted non-application paths and a non-releasing plan. A PR must target the current main tip; stale, stacked, mixed, unknown and uncertain changes use the existing Windows lane. Main checks its first-parent diff and the full pending release plan, so a documentation merge cannot suppress a pending application release. The Linux validation job rechecks the scope and plan before the named gates report that Windows is not applicable; it does not produce Windows test evidence. Otherwise the named Linux gates require genuine Windows evidence bound to the commit, version and validation lane. The package and publication checks independently hash the installers and verify the exact asset set. The nine check names are stable; publication runs in a separate serialized job. A stale lane or an expired continuation requires a fresh pipeline. The website deployment remains after publication so release pages never precede the GitHub release. See [CI lanes and measurement](DEVELOPMENT.md#continuous-integration).

Main alone writes verified SDK and dependency caches, with seven-day cache retention. Workspace retention covers workflow retries. The pipeline retains a workspace copy for validation/publication and keeps test reports and smoke logs. GitHub Releases is the permanent distribution location.

## Migration from the legacy update server

Installed versions through 1.2.6 cannot discover GitHub updates on their own. After the first verified release is published:

- Download its exact installer from GitHub and verify SHA256SUMS.txt.
- Copy it to the old host at /PresetLauncher/PresetLauncher_Setup_<version>.exe, using the numeric filename without v.
- Only once the installer is reachable, update /PresetLauncher/version.txt to the numeric version.
- Test the legacy updater on an isolated Windows VM and verify that data survives and the new app checks GitHub. Keep this bridge available for users who return later, or instruct them to install the latest release manually.

CI does not change the legacy host. The bridge remains necessary for clients that have not migrated. Older clients cannot enforce the new required-update policy until upgraded.

## Verification and security

Full validation checks PE machine values for each published app host and key self-contained native libraries. The x64 worker installs, starts and removes x64 and x86 in sequence with isolated state. Windows UI Automation checks that startup has enabled the preset controls and that the installed app can see the fixture preset. The production data and settings readers validate the same fixture in the regression suite. Smoke checks registration, shortcuts and preservation of presets, history, statistics, preferences and both protected policy files. It upgrades from the authenticated matching predecessor where one exists. The first release of each new architecture uses fresh-install smoke. The x64 host checks ARM64 setup rejection. Native ARM64 validation requires a separate smoke test on supported ARM64 hardware; rejection on the x64 worker is not a native ARM64 test.

The updater checks GitHub's HTTPS metadata and verifies downloaded bytes. Checksums and update metadata signatures do not provide Authenticode publisher verification or establish SmartScreen reputation.

In-app updates download and verify the signed architecture-specific installer, then open the normal interactive Inno Setup wizard before closing the application. The temporary installer is pinned and its size and SHA-256 are checked again before launch. Setup uses the existing installation directory, scope and tasks by default. No silent update helper is shipped.

The launcher holds a shared Windows file handle in its installation directory before creating the app window. Setup holds the exclusive handle from preparation through file replacement. The handle applies across accounts and sessions that share the installation, and Windows closes it when its owner exits. The x64 and WOW64 installer smoke tests exercise both directions of this exclusion. If another launch acquires the shared handle before setup starts, setup stops before replacing files. A launcher from before this protocol does not participate.

Versions 1.7.0 and 1.7.1 still contain the previous silent helper and cannot be switched remotely to the interactive flow. If their in-app update fails, close PresetLauncher and install the latest matching architecture directly from GitHub Releases. Preserve the protected update policy and user data. The startup readiness signal and temporary workspace cleanup remain compatible with these earlier helpers.

## Update signing key

The production update signing key is an ECDSA P-256 key generated outside the repository. The public key is pinned in `.circleci/update-policy-public.pem`; the private key is stored as `UPDATE_SIGNING_KEY_B64` in the restricted release context and backed up securely offline. Private key material is excluded from source control, issues, pull requests and logs.

For initial provisioning, the key pair can be generated on a trusted machine:

```bash
umask 077
keydir="$(mktemp -d)"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out "$keydir/update-private.pem"
openssl pkey -in "$keydir/update-private.pem" -pubout -out "$keydir/update-public.pem"
```

The provisioning example writes both files outside the repository; it does not replace the active pinned key. The private PEM is base64-encoded for transfer directly to the restricted CircleCI secret field. Temporary private material is removed after secure backup.

Publication emits exactly one legacy v1 ECDSA P-256 signature and keeps the unsuffixed x64 filename and original policy comments. Existing x64 update clients can verify that installer unchanged.

New clients require `presetlauncher:manifest-v2` in the release body. It contains base64-encoded signed message bytes, a dot and a base64 DER ECDSA P-256/SHA-256 signature. The signed message begins with ASCII `presetlauncher-update-v2` and an LF, followed by compact ASCII JSON with `version`, `minimum_supported_version`, `policy` and `installers`. Installer entries are ordered x64, x86, ARM64, each with `architecture`, `name`, `size` and lowercase `sha256`. Clients verify the original bytes with the existing pinned key, apply the authenticated policy floor and then match the process architecture and GitHub asset metadata. A missing or invalid asset leaves the authenticated minimum in effect. A v1 marker alone cannot authorize a download by a v2 client.

Signed predecessors are checked under their published protocol before reusing their floors and installers. The historical unsigned exceptions are limited to 1.2.6 and v1.3.0. The same restricted release context signs both formats.

The signing key is pinned. A replacement key requires a compatible client migration and transition plan before switching the signer. Clients that miss the transition may require a manual installer. Replacing only the CircleCI secret would invalidate signatures for existing clients.

The protected local policy is stored under `%LocalAppData%\\PresetLauncher\\Security\\update-policy.dat` using Windows DPAPI for the current user. The old minimum in settings is imported only on first initialization. The lock file also records initialization, so deleting the policy file alone blocks startup. A corrupted protected file blocks startup. See the recovery instructions in [SECURITY.md](../SECURITY.md). DPAPI does not prevent a user who controls the machine from deleting, replaying or recreating their own local state or modifying the executable.
