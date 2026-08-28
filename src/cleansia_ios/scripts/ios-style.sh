#!/usr/bin/env bash
# Run the iOS style gates HERE, under WSL, instead of paying a CI round for a missing blank line.
#
# WHY THIS EXISTS. Neither tool runs on Windows, so every style violation in this repo has been found
# by macOS CI — roughly sixteen minutes a round, and style rounds arrive in batches because one fix
# uncovers the next. Both ship Linux binaries and WSL is already here, so the two gates that fail
# most often can now be answered in seconds.
#
# THE PINS ARE THE WHOLE POINT. .github/workflows/ios-ci.yml pins SwiftFormat 0.60.1 and SwiftLint
# 0.65.0 and asserts both at run time, because a drifted version turns on default rules and fails
# code that was clean locally — the CI comment records 0.62.1 flagging 94 files that pass under
# 0.60.1. This installs and asserts the SAME versions from the SAME releases. A local tool that
# disagrees with CI is worse than no local tool: it spends your trust, then fails the push anyway.
# Change a pin in ios-ci.yml and you must change it here.
#
# TWO THINGS THIS HAD TO SOLVE, both found by running it rather than by reading the docs:
#
#   1. The Windows working tree is CRLF. `core.autocrlf` is true and .gitattributes says `text=auto`,
#      so the checkout is CRLF while the index — and every CI checkout — is LF. SwiftFormat's
#      `linebreaks` rule then fails 812 of 831 files, none of them real. So the gates do not read the
#      working tree directly: an LF snapshot is staged on the Linux filesystem and linted there. That
#      also takes SwiftFormat from ~50s over /mnt/c to a few seconds on ext4.
#
#   2. SwiftLint's Linux zip contains TWO binaries. `swiftlint` is dynamically linked and dies with
#      "Loading libsourcekitdInProc.so failed" unless a full Swift toolchain is installed;
#      `swiftlint-static` is self-contained and is what this uses. Preferring the static build is the
#      difference between a 5-second check and a 1.5 GB toolchain install.
#
# WHAT THIS IS NOT. It is not the iOS gate. xcodegen, the build and the test suite still run only on
# macOS. This covers the two cheap gates, which is where the round-trips actually go.
#
# No sudo, no apt: archives are unpacked with python3, which Ubuntu ships, and binaries land in
# ~/.local/bin.
#
#   wsl bash src/cleansia_ios/scripts/ios-style.sh          # lint, exactly as CI does
#   wsl bash src/cleansia_ios/scripts/ios-style.sh --fix     # let SwiftFormat rewrite, then lint
set -euo pipefail

SWIFTFORMAT_VERSION="0.60.1"
SWIFTLINT_VERSION="0.65.0"

BIN_DIR="${IOS_STYLE_BIN_DIR:-$HOME/.local/bin}"
CACHE_DIR="${IOS_STYLE_CACHE_DIR:-$HOME/.cache/cleansia-ios-style}"
IOS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAGE="$CACHE_DIR/tree"

mkdir -p "$BIN_DIR" "$CACHE_DIR"

case "$(uname -m)" in
    x86_64)  SWIFTFORMAT_ASSET="swiftformat_linux.zip";         SWIFTLINT_ASSET="swiftlint_linux_amd64.zip" ;;
    aarch64) SWIFTFORMAT_ASSET="swiftformat_linux_aarch64.zip"; SWIFTLINT_ASSET="swiftlint_linux_arm64.zip" ;;
    *)
        echo "ios-style: no published Linux binary for $(uname -m) — run the gates on macOS or in CI." >&2
        exit 2
        ;;
esac

# Install only when the pinned version is not already the one sitting there. `--version` is the test
# rather than a marker file, because what must be true is what the binary reports.
install_tool() {
    local name="$1" want="$2" url="$3" version_flag="$4" have=""
    if [ -x "$BIN_DIR/$name" ]; then
        have="$("$BIN_DIR/$name" "$version_flag" 2>/dev/null | tr -d '\r' | head -1 || true)"
        [ "$have" = "$want" ] && return 0
        echo "ios-style: $name reports '${have:-nothing}', want $want — reinstalling"
    fi
    echo "ios-style: installing $name $want"
    curl -fsSL -o "$CACHE_DIR/$name.zip" "$url"
    rm -rf "$CACHE_DIR/$name-unpacked"
    python3 -m zipfile -e "$CACHE_DIR/$name.zip" "$CACHE_DIR/$name-unpacked"

    # Prefer a `-static` build: SwiftLint ships both, and only the static one runs without a Swift
    # toolchain. Then the exact name, then the extension-less `<name>_*` sibling — SwiftFormat's
    # Linux zip calls its binary `swiftformat_linux`. Never a `.md`/`.txt` sharing the prefix.
    local bin=""
    for pattern in "$name-static" "$name" "${name}_*"; do
        bin="$(find "$CACHE_DIR/$name-unpacked" -type f -name "$pattern" ! -name '*.*' | head -1)"
        [ -n "$bin" ] && break
    done
    if [ -z "$bin" ]; then
        echo "ios-style: no '$name' binary inside $url" >&2
        exit 1
    fi
    install -m 0755 "$bin" "$BIN_DIR/$name"
}

install_tool swiftformat "$SWIFTFORMAT_VERSION" \
    "https://github.com/nicklockwood/SwiftFormat/releases/download/$SWIFTFORMAT_VERSION/$SWIFTFORMAT_ASSET" \
    "--version"
install_tool swiftlint "$SWIFTLINT_VERSION" \
    "https://github.com/realm/SwiftLint/releases/download/$SWIFTLINT_VERSION/$SWIFTLINT_ASSET" \
    "version"

# Assert the pins the way CI does, and for the same reason: a tool earlier in PATH once shadowed the
# pinned one and linted the tree with a drifted version.
SF_VERSION="$("$BIN_DIR/swiftformat" --version | tr -d '\r')"
SL_VERSION="$("$BIN_DIR/swiftlint" version | tr -d '\r')"
[ "$SF_VERSION" = "$SWIFTFORMAT_VERSION" ] || { echo "ios-style: swiftformat is $SF_VERSION, CI pins $SWIFTFORMAT_VERSION" >&2; exit 1; }
[ "$SL_VERSION" = "$SWIFTLINT_VERSION" ]   || { echo "ios-style: swiftlint is $SL_VERSION, CI pins $SWIFTLINT_VERSION" >&2; exit 1; }

# Stage an LF copy of everything the two tools read. See note 1 in the header.
rm -rf "$STAGE"
python3 - "$IOS_ROOT" "$STAGE" <<'PY'
import os, sys
src, dst = sys.argv[1], sys.argv[2]
SKIP = {".build", "DerivedData", "CleansiaPartnerApi", "CleansiaCustomerApi", ".git", "Generated"}
KEEP = (".swiftformat", ".swiftlint.yml")
count = 0
for root, dirs, files in os.walk(src):
    dirs[:] = [d for d in dirs if d not in SKIP]
    for name in files:
        if not (name.endswith(".swift") or name in KEEP):
            continue
        source = os.path.join(root, name)
        target = os.path.join(dst, os.path.relpath(source, src))
        os.makedirs(os.path.dirname(target), exist_ok=True)
        with open(source, "rb") as fh:
            data = fh.read()
        with open(target, "wb") as fh:
            fh.write(data.replace(b"\r\n", b"\n"))
        count += 1
print(f"ios-style: staged {count} file(s) as LF")
PY

echo "ios-style: swiftformat $SF_VERSION · swiftlint $SL_VERSION · $IOS_ROOT"
cd "$STAGE"
status=0

# Report paths in the REAL tree so the messages stay clickable. PIPESTATUS because the tool's exit
# code is what matters, not sed's.
gate() {
    local label="$1"; shift
    echo "--- $label"
    set +e
    "$@" 2>&1 | sed "s|$STAGE|$IOS_ROOT|g"
    local rc=${PIPESTATUS[0]}
    set -e
    return "$rc"
}

if [ "${1:-}" = "--fix" ]; then
    gate "swiftformat (rewriting)" "$BIN_DIR/swiftformat" . || status=$?
    # Copy back only what changed. Writing LF into a CRLF working tree is safe and produces no
    # spurious diff: core.autocrlf normalises on read, so a LF file whose content matches the index
    # shows as unmodified.
    python3 - "$STAGE" "$IOS_ROOT" <<'PY'
import filecmp, os, shutil, sys
stage, real = sys.argv[1], sys.argv[2]
changed = 0
for root, _, files in os.walk(stage):
    for name in files:
        if not name.endswith(".swift"):
            continue
        s = os.path.join(root, name)
        r = os.path.join(real, os.path.relpath(s, stage))
        if not os.path.exists(r):
            continue
        with open(s, "rb") as fh:
            new = fh.read()
        with open(r, "rb") as fh:
            old = fh.read().replace(b"\r\n", b"\n")
        if new != old:
            with open(r, "wb") as fh:
                fh.write(new)
            changed += 1
print(f"ios-style: wrote back {changed} reformatted file(s)")
PY
    status=0
fi

gate "swiftformat --lint ." "$BIN_DIR/swiftformat" --lint . || status=$?
gate "swiftlint lint --strict" "$BIN_DIR/swiftlint" lint --strict || status=$?

if [ "$status" -eq 0 ]; then
    echo "ios-style: clean — both gates pass at CI's pinned versions"
else
    echo "ios-style: the violations above would fail ios-ci.yml" >&2
fi
exit "$status"
