#!/usr/bin/env python3
"""Check a built customer app's public APNs marker against its actual signature.

Reads only the specified artifact; no provisioning profiles, keychains or local signing inputs.
The exported IPA is checked after export, because distribution export can re-sign an archive.
"""
import argparse
from pathlib import Path
import plistlib
import subprocess
import tempfile
import zipfile


def validate_environment(marker, signed, expected=None):
    known = {"development", "production"}
    if marker not in known:
        raise ValueError("Customer APNs environment marker is missing or invalid")
    if signed not in known:
        raise ValueError("Customer signed aps-environment entitlement is missing or invalid")
    if marker != signed:
        raise ValueError("Customer APNs environment marker does not match its signed entitlement")
    if expected is not None and signed != expected:
        raise ValueError(f"This distribution lane requires {expected} APNs signing")


def check_app(app, expected=None):
    with (app / "Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    result = subprocess.run(
        ["/usr/bin/codesign", "-d", "--entitlements", ":-", str(app)],
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        raise ValueError("Cannot read the customer artifact's signed entitlements")
    try:
        entitlements = plistlib.loads(result.stdout)
    except (plistlib.InvalidFileException, ValueError) as error:
        raise ValueError("Cannot decode the customer artifact's signed entitlements") from error
    validate_environment(info.get("CleansiaAPNSEnvironment"), entitlements.get("aps-environment"), expected)


def check_ipa(ipa, expected=None):
    with tempfile.TemporaryDirectory(prefix="cleansia-apns-artifact-") as directory:
        root = Path(directory).resolve()  # macOS temp folders sit under /var, a symlink to /private/var
        with zipfile.ZipFile(ipa) as archive:
            for entry in archive.infolist():
                target = (root / entry.filename).resolve()
                if not target.is_relative_to(root):
                    raise ValueError("The IPA contains an invalid artifact path")
            archive.extractall(root)
        apps = list((root / "Payload").glob("*.app"))
        if len(apps) != 1:
            raise ValueError("The IPA must contain exactly one customer application")
        check_app(apps[0], expected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    artifact = parser.add_mutually_exclusive_group(required=True)
    artifact.add_argument("--ipa", type=Path)
    artifact.add_argument("--app", type=Path)
    parser.add_argument("--expected", choices=["development", "production"])
    args = parser.parse_args()
    try:
        if args.ipa:
            check_ipa(args.ipa, args.expected)
        else:
            check_app(args.app, args.expected)
    except (ValueError, OSError, zipfile.BadZipFile) as error:
        parser.exit(1, f"Live Activity artifact check failed: {error}\n")
    print("Live Activity APNs marker matches the signed artifact")


if __name__ == "__main__":
    main()
