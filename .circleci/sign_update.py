"""Sign the exact release policy and installer metadata with the pinned P-256 key."""
import base64
import os
from pathlib import Path
import subprocess
import sys
import tempfile

PUBLIC_KEY = Path(__file__).with_name("update-policy-public.pem")


def payload(data, size, digest):
    return ("presetlauncher-update-v1\n"
            f"{data['version']}\n{data['minimum_supported_version']}\n"
            f"{data['policy']}\n{size}\n{digest.lower()}\n").encode("ascii")


def validate_public_key():
    result = subprocess.run(["openssl", "pkey", "-pubin", "-in", str(PUBLIC_KEY),
                             "-pubcheck", "-text", "-noout"], check=True,
                            capture_output=True, text=True)
    if "ASN1 OID: prime256v1" not in result.stdout:
        raise ValueError("The public key must use P-256")


def verify(data, size, digest, signature):
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "signature.der"
        path.write_bytes(base64.b64decode(signature, validate=True))
        subprocess.run(["openssl", "dgst", "-sha256", "-verify", str(PUBLIC_KEY),
                        "-signature", str(path)], input=payload(data, size, digest),
                       check=True, stdout=subprocess.DEVNULL)


def sign(data, size, digest):
    validate_public_key()
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
        encoded = base64.b64encode(signature.read_bytes()).decode("ascii")
        verify(data, size, digest, encoded)
        return encoded


if __name__ == "__main__":
    if sys.argv[1:] != ["check-key"]:
        sys.exit("usage: sign_update.py check-key")
    validate_public_key()
