# Contributing

## Before changing code

Search [open issues](https://github.com/JimMarley420/Preset_launcher/issues) first. For a substantial feature or architectural change, open an issue describing the problem before investing time in implementation. Small fixes and documentation corrections can go straight to a pull request.

## Local development

Fork the repository, clone your fork and create a branch from `main`. Install the .NET 10 SDK specified in `global.json` and Visual Studio 2026 with WinUI development tools. Follow the [development notes](docs/DEVELOPMENT.md) to build the Windows application. Keep changes focused and follow the existing C# and XAML conventions.

When changing user-facing text, update both English and French localization entries. Keep both README files in sync when changing installation or usage instructions.

## Commit messages

Use Conventional Commits. `fix:` and `perf:` produce patch releases, `feat:` produces a minor release, and `!` or a `BREAKING CHANGE:` footer produces a major release. Documentation, tests, CI, build tooling and internal refactoring alone do not produce a release. Use a conventional title when squash-merging. The PR title determines the release bump automatically. The maintainer selects the update label for releases. See [release operations](docs/RELEASING.md).

## Submitting a pull request

Explain what changed, why it changed and how you verified it. Include screenshots for visual changes, and describe any checks you could not run. Avoid unrelated formatting changes, generated build output, credentials, private keys and personal preset data.

Contributions are submitted under the project's [MIT license](LICENSE).
