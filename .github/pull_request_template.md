## What changed?

<!-- Describe the change and why it is needed. -->

## Related issue

<!-- Link an issue if applicable. -->

## Release impact

<!-- REQUIRED TITLE: feat(scope): description | fix(scope): description | perf: description | feat!: breaking change | docs: description | ci: description | test: description | refactor: description | chore: description | build: description | style: description | revert: description
The scope is optional. CircleCI validates the actual PR title.
Choose one release impact below. If a mandatory update is intended, add Update-Required: true as a standalone line outside this comment. See docs/RELEASING.md. -->

- [ ] Patch (fix or performance improvement)
- [ ] Minor (new functionality)
- [ ] Major (breaking change)
- [ ] No release (internal or documentation change)

<!-- Keep exactly one release impact checked. The commit title/footer, not these informational checkboxes, determines the release. -->

## Verification

<!-- List the checks you actually ran, Windows version and results. Explain anything not tested. -->

For launcher changes, specify the mode tested (all, missing only, or close), delays, elevation and already-running programs where relevant. For data changes, use disposable presets and verify persistence after restart.

## Screenshots

<!-- Before/after screenshots for UI changes, if applicable. -->

- [ ] The change is limited to the stated scope.
- [ ] Documentation and both localizations are updated where applicable.
- [ ] No secrets, personal data or generated build artifacts are included.
