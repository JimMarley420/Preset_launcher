# Releases

## Versioning

GitHub's latest published stable release is the baseline. The existing 1.2.6 tag has no v prefix; new tags use vMAJOR.MINOR.PATCH. CircleCI reads commits after the baseline tag and selects the highest required bump.

| Commit | Bump | Example from 1.2.6 |
| --- | --- | --- |
| fix: or perf: | Patch | 1.2.7 |
| feat: | Minor | 1.3.0 |
| feat!:, fix!: or BREAKING CHANGE: footer | Major | 2.0.0 |
| docs:, ci:, test:, refactor:, chore: only | None | No publication |

Use a Conventional Commit on a branch or in the squash-merge title. A user-facing behavior fix is a fix, even if implemented by refactoring. Non-conventional commits do not trigger a release. Multiple changes between releases produce one highest bump.

The release plan in artifacts/release-plan.json is the source of the published assembly version, installer version, registry DisplayVersion and asset filename. Branch CI artifacts are not production releases. Local builds default to 0.0.0 and automatic update checks are disabled for that version.

## Update policy

Version significance and update requirements are separate. Updates are optional by default. A maintainer can add this exact footer to a releasing conventional commit:

    fix: prevent data loss during import

    Update-Required: true

The generated release notes contain an HTML comment:

    <!-- presetlauncher:min-supported=1.2.7 -->

The minimum supported version advances to the new release when this footer is present. Otherwise the previous minimum is retained. Later optional releases still require users below that floor to update. Major changes are not automatically mandatory. The 1.2.6 legacy release has an implicit floor of 0.0.0.

The app checks GitHub at startup and in Settings. Optional updates offer Install, Later or Skip this version; manual checks ignore skipped-version preferences. Required updates block navigation once known, with retry or exit rather than skip. A known floor is cached and survives settings reset. If GitHub is unreachable before a mandatory floor is known, the offline app remains usable. If a required update is already known, the app offers retry or exit. This is application policy, not tamper-resistant DRM.

Reserve required releases for security, data integrity, compatibility or a deliberately necessary migration, not routine cosmetic changes.

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

## Migration from the legacy update server

Installed versions through 1.2.6 cannot discover GitHub updates on their own. After the first verified release is published:

- Download its exact installer from GitHub and verify SHA256SUMS.txt.
- Copy it to the old host at /PresetLauncher/PresetLauncher_Setup_<version>.exe, using the numeric filename without v.
- Only once the installer is reachable, update /PresetLauncher/version.txt to the numeric version.
- Test the legacy updater on an isolated Windows VM and verify that data survives and the new app checks GitHub. Keep this bridge available for users who return later, or instruct them to install the latest release manually.

CI does not change the old host. Do not shut it down prematurely. Older clients cannot enforce the new required-update policy until upgraded.

## Verification and security

The smoke test installs and removes the new package. For releasing changes it also verifies the previous public installer digest, installs it, upgrades in place, and checks application version, registry and preservation of disposable user data. It does not launch the UI. Before the first release, manually test both languages, optional skip, mandatory update, offline retry, download errors, canceled installation and the legacy bridge in a disposable Windows VM.

The new updater checks GitHub's HTTPS metadata and verifies downloaded bytes. A checksum is not independent code signing: configure Authenticode signing before claiming publisher verification or SmartScreen trust. Limit repository and publication access.
