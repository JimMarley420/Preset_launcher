# Security

Do not post exploitable vulnerabilities, credentials or private keys in a public issue.

Use [GitHub private vulnerability reporting](https://github.com/JimMarley420/Preset_launcher/security/advisories/new) if it is enabled. Otherwise, open a public issue asking for a private reporting channel without including vulnerability details.

Include the affected version, Windows version, reproduction steps, expected impact and any relevant evidence with personal information removed. There is no guaranteed response time.

PresetLauncher runs local executables and may request elevation when configured to do so. Only import presets from sources you trust and inspect their launch settings. Password-related model fields and hashing helpers exist, but the current UI does not enforce preset locks. Local JSON files are not encrypted. The update service downloads and executes an installer over HTTPS without an explicit signature or checksum verification step in the application. Closing preset processes may forcibly terminate matching programs; save your work first.
