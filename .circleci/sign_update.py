"""Sign the exact release policy and installer metadata with the pinned P-256 key."""
import base64
import os
from pathlib import Path
import subprocess
import tempfile

PUBLIC_KEY = Path(__file__).with_name("update-policy-public.pem")


def payload(data, size, digest):
    return ("presetlauncher-update-v1\n"
            f"{data['version']}\n{data['minimum_supported_version']}\n"
            f"{data['policy']}\n{size}\n{digest.lower()}\n").encode("ascii")


def sign(data, size, digest):
    # The key never enters a command argument or the checkout.
    private = base64.b64decode(os.environ["UPDATE_SIGNING_KEY_B64"], validate=True)
    message = payload(data, size, digest)
    with tempfile.TemporaryDirectory() as directory:
        key = Path(directory) / "key.pem"
        signature = Path(directory) / "signature.der"
        key.write_bytes(private)
        key.chmod(0o600)
        public = subprocess.run(["openssl", "pkey", "-in", str(key), "-pubout"],
                                check=True, capture_output=True).stdout
        if public.strip() != PUBLIC_KEY.read_bytes().strip():
            raise ValueError("Signing key does not match the pinned public key")
        subprocess.run(["openssl", "dgst", "-sha256", "-sign", str(key),
                        "-out", str(signature)], input=message, check=True)
        subprocess.run(["openssl", "dgst", "-sha256", "-verify", str(PUBLIC_KEY),
                        "-signature", str(signature)], input=message,
                       check=True, stdout=subprocess.DEVNULL)
        return base64.b64encode(signature.read_bytes()).decode("ascii")
