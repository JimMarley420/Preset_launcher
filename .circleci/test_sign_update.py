import base64
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import release
import sign_update

FIXTURE = Path(__file__).with_name("fixtures") / "signed-update.json"


def write_cross_language_fixture():
    # Regenerate explicitly. OpenSSL ECDSA uses random nonces, so freeze the
    # resulting signatures for deterministic verification in both test suites.
    data = {"version": "2.1.0", "minimum_supported_version": "2.1.0", "policy": "required"}
    tag = "v" + data["version"]
    assets = []
    for index, arch in enumerate(release.ARCHITECTURES, 1):
        name = release.installer_name(data["version"], arch)
        assets.append({"name": name, "state": "uploaded", "size": 100 + index,
                       "browser_download_url": f"https://github.com/{release.REPO}/releases/download/{tag}/{name}",
                       "digest": f"sha256:{str(index) * 64}"})
    predecessor = {"tag_name": tag, "draft": False, "prerelease": False, "assets": assets}
    installers = {arch: release.installer_asset(predecessor, arch) for arch in release.ARCHITECTURES}
    with tempfile.TemporaryDirectory() as directory:
        private = Path(directory) / "cross-language-test-private.pem"
        public = Path(directory) / "cross-language-test-public.pem"
        subprocess.run(["openssl", "genpkey", "-algorithm", "EC", "-pkeyopt",
                        "ec_paramgen_curve:P-256", "-out", str(private)], check=True)
        subprocess.run(["openssl", "pkey", "-in", str(private), "-pubout", "-out", str(public)], check=True)
        with patch.object(sign_update, "PUBLIC_KEY", public), patch.dict(os.environ, {
            "UPDATE_SIGNING_KEY_B64": base64.b64encode(private.read_bytes()).decode("ascii")
        }):
            predecessor["body"] = release.signed_update_markers(data, installers)
            prefix, message = release.manifest_message(data, installers).split(b"\n", 1)
            duplicate = json.loads(message)
            duplicate["installers"].append(duplicate["installers"][1])
            duplicate_message = prefix + b"\n" + json.dumps(duplicate, separators=(",", ":")).encode("ascii")
            encoded = base64.b64encode(duplicate_message).decode("ascii")
            signed_duplicate = f"<!-- presetlauncher:manifest-v2={encoded}.{sign_update.sign_message(duplicate_message)} -->"
        fixture = {"public_key": public.read_text(encoding="ascii"), "release": predecessor,
                   "signed_duplicate": signed_duplicate}
    FIXTURE.write_text(json.dumps(fixture, indent=2) + "\n", encoding="ascii", newline="\n")


class SigningTests(unittest.TestCase):
    def test_cross_language_fixture_matches_publisher_serialization(self):
        fixture = json.loads(FIXTURE.read_text(encoding="ascii"))
        with tempfile.TemporaryDirectory() as directory:
            public = Path(directory) / "fixture.pem"
            public.write_text(fixture["public_key"], encoding="ascii")
            with patch.object(sign_update, "PUBLIC_KEY", public):
                installers = release.verify_previous(fixture["release"])
                self.assertEqual([installers[arch]["size"] for arch in release.ARCHITECTURES], [101, 102, 103])
                self.assertEqual([installers[arch]["sha256"] for arch in release.ARCHITECTURES],
                                 [digit * 64 for digit in "123"])

    def test_signature_verifies_and_changed_payload_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            private = Path(directory) / "private.pem"
            public = Path(directory) / "public.pem"
            sig = Path(directory) / "sig.der"
            subprocess.run(["openssl", "genpkey", "-algorithm", "EC", "-pkeyopt",
                            "ec_paramgen_curve:P-256", "-out", str(private)], check=True)
            subprocess.run(["openssl", "pkey", "-in", str(private), "-pubout", "-out", str(public)], check=True)
            data = {"version": "1.3.1", "minimum_supported_version": "1.3.1", "policy": "required"}
            with patch.object(sign_update, "PUBLIC_KEY", public), patch.dict(os.environ, {
                "UPDATE_SIGNING_KEY_B64": base64.b64encode(private.read_bytes()).decode("ascii")
            }):
                sig.write_bytes(base64.b64decode(sign_update.sign(data, 4, "ab" * 32)))
                command = ["openssl", "dgst", "-sha256", "-verify", str(public), "-signature", str(sig)]
                self.assertEqual(subprocess.run(command, input=sign_update.payload(data, 4, "ab" * 32)).returncode, 0)
                self.assertNotEqual(subprocess.run(command, input=sign_update.payload({**data, "policy": "optional"}, 4, "ab" * 32)).returncode, 0)
                other = Path(directory) / "other.pem"
                subprocess.run(["openssl", "genpkey", "-algorithm", "EC", "-pkeyopt",
                                "ec_paramgen_curve:P-256", "-out", str(other)], check=True)
                subprocess.run(["openssl", "pkey", "-in", str(other), "-pubout", "-out", str(public)], check=True)
                with self.assertRaises(ValueError):
                    sign_update.sign(data, 4, "ab" * 32)

    def test_canonical_payload(self):
        data = {"version": "1.4.0", "minimum_supported_version": "1.4.0", "policy": "required"}
        expected = b"presetlauncher-update-v1\n1.4.0\n1.4.0\nrequired\n4\n" + b"a" * 64 + b"\n"
        self.assertEqual(sign_update.payload(data, 4, "a" * 64), expected)

    def test_multi_architecture_predecessor_must_match_both_signed_protocols(self):
        data = {"version": "1.5.0", "minimum_supported_version": "1.5.0", "policy": "required"}
        tag = "v1.5.0"
        assets = []
        for arch in release.ARCHITECTURES:
            name = release.installer_name(data["version"], arch)
            assets.append({"name": name, "state": "uploaded", "size": 42,
                           "browser_download_url": f"https://github.com/{release.REPO}/releases/download/{tag}/{name}",
                           "digest": f"sha256:{'a' * 64}"})
        predecessor = {"tag_name": tag, "assets": assets}
        installers = {arch: release.installer_asset(predecessor, arch) for arch in release.ARCHITECTURES}
        with tempfile.TemporaryDirectory() as directory:
            key = Path(directory) / "key.pem"
            public = Path(directory) / "public.pem"
            subprocess.run(["openssl", "genpkey", "-algorithm", "EC", "-pkeyopt",
                            "ec_paramgen_curve:P-256", "-out", str(key)], check=True)
            subprocess.run(["openssl", "pkey", "-in", str(key), "-pubout", "-out", str(public)], check=True)
            with patch.object(sign_update, "PUBLIC_KEY", public), patch.dict(os.environ, {
                "UPDATE_SIGNING_KEY_B64": base64.b64encode(key.read_bytes()).decode("ascii")
            }):
                v1 = sign_update.sign(data, 42, "a" * 64)
                message = release.manifest_message(data, installers)
                v2 = base64.b64encode(message).decode("ascii") + "." + sign_update.sign_message(message)
                predecessor["body"] = ("<!-- presetlauncher:min-supported=1.5.0 -->\n"
                                       "<!-- presetlauncher:update-policy=required -->\n"
                                       f"<!-- presetlauncher:signature-v1={v1} -->\n"
                                       f"<!-- presetlauncher:manifest-v2={v2} -->")
                self.assertEqual(release.verify_previous(predecessor), installers)
                assets[2]["size"] = 43
                with self.assertRaisesRegex(ValueError, "does not match"):
                    release.verify_previous(predecessor)


if __name__ == "__main__":
    write_cross_language_fixture()
