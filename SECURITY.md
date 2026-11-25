# Security

Do not post exploitable vulnerabilities, credentials or private keys in a public issue.

Use [GitHub private vulnerability reporting](https://github.com/JimMarley420/Preset_launcher/security/advisories/new) if it is enabled. Otherwise, open a public issue asking for a private reporting channel without including vulnerability details.

Include the affected version, Windows version, reproduction steps, expected impact and any relevant evidence with personal information removed. There is no guaranteed response time.

PresetLauncher runs local executables and may request elevation when configured to do so. Only import presets from sources you trust and inspect their launch settings. Password-related model fields and hashing helpers exist, but the current UI does not enforce preset locks. Local JSON files are not encrypted. The updater accepts stable releases from this repository and checks the installer size and SHA-256 digest reported by GitHub before opening it. This relies on GitHub HTTPS and repository integrity. It does not provide Authenticode signing or independent publisher authentication. Versions 1.2.6 and earlier use the legacy update server and do not have this verification. Closing preset processes may forcibly terminate matching programs. Save your work first.

Newer clients verify an ECDSA signature over the release policy and installer digest using an embedded public key. The highest known release and minimum supported version are stored separately with Windows DPAPI. This protects against casual configuration edits and unsigned policy changes. It does not prevent the machine owner from modifying the client, deleting or replaying local state. Release signing is not Authenticode signing.

## Update state recovery

If the update state cannot be read, close all PresetLauncher instances. Restore a valid local backup of the Security directory if available. Otherwise, obtain and install the latest trusted release while online, then move the damaged Security directory aside and restart while connected to GitHub. Do not use this procedure to keep an older version. Presets and preferences are stored outside the Security directory.

The protected state detects corruption and removal of the policy file alone. Deleting or restoring the entire directory remains possible for the Windows account owner. A fresh offline installation cannot discover an unknown mandatory update.
