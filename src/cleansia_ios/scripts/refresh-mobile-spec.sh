#!/usr/bin/env bash
#
# Fetches the latest OpenAPI spec from a running mobile API host and overwrites
# the SHARED committed copy that both Android and iOS codegen read:
#
#   partner  -> http://localhost:5002/swagger/v1/swagger.json
#   customer -> http://localhost:5004/swagger/v1/swagger.json
#
# This mirrors Android's `./gradlew :{partner,customer}-app:dumpOpenApiSpec`.
# The two specs live under src/cleansia_android/openapi/ so both platforms stay
# on a single source of truth.
#
# The CANONICAL regen of these committed specs is an owner step
# (manual_step: mobile-spec-regen) — a re-dump is owner-run because it needs the mobile API hosts
# running. It is NOT a blocker: the committed specs are current as of 2026-08-14 and both clients are
# generated and wired. Formerly the specs were stale (pre-T-0272) and the
# first real client generation is held until the owner refreshes them. This
# script is the same plumbing a developer uses locally against a dev host.
#
# Usage:
#   ./scripts/refresh-mobile-spec.sh                       # both apps, localhost
#   ./scripts/refresh-mobile-spec.sh partner               # one app
#   ./scripts/refresh-mobile-spec.sh customer http://192.168.1.20:5004/swagger/v1/swagger.json

set -euo pipefail

IOS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SPEC_DIR="${IOS_ROOT}/../cleansia_android/openapi"

# macOS ships Bash 3.2, which has no associative arrays (declare -A) — resolve
# the per-app default URL with a function instead.
default_url_for() {
  case "$1" in
    partner) echo "http://localhost:5002/swagger/v1/swagger.json" ;;
    customer) echo "http://localhost:5004/swagger/v1/swagger.json" ;;
  esac
}

# Compare NORMALISED content, not raw bytes. git's eol filter (core.autocrlf, and the root
# .gitattributes pins *.sh but says nothing about *.json) hands a Windows checkout a CRLF
# working tree while the host serves LF. Measured 2026-08-27: the committed partner spec is
# 313,178 bytes on disk and the BYTE-IDENTICAL live fetch is 301,765 - so a raw-byte guard
# refuses an unchanged spec, and by the same margin would wave through a real 11 KB downgrade.
#
# A bare CR here can only be a line terminator: inside a JSON string it must be escaped as
# backslash-r, so stripping it never touches payload.
normalised_size() {
  tr -d '\r' < "$1" | wc -c | tr -d ' '
}

app="${1:-all}"
override_url="${2:-}"

# Three outcomes, three counters. These used to share one, so a refused downgrade was reported
# as "host unreachable" - the wrong cause, and the one message that would have made this bug
# obvious the first time it happened.
refreshed=0
skipped=0
refused=0

refresh_one() {
  local name="$1"
  local url="${override_url:-$(default_url_for "$name")}"
  local out="${SPEC_DIR}/${name}-mobile-api.json"

  echo "Fetching ${name} spec from ${url} ..."

  # Fetch to a temp file first. Writing straight over the committed spec means a host running an OLDER
  # build silently downgrades the contract every client is generated from — and the symptom lands much
  # later as "the generator will not emit a type the spec clearly defines", with nothing pointing here.
  local before=0
  [[ -f "$out" ]] && before="$(normalised_size "$out")"
  local tmp="${out}.fetched"

  if curl -fsS "$url" -o "$tmp"; then
    local after
    after="$(normalised_size "$tmp")"

    if [[ "$before" -gt 0 && "$after" -lt "$before" ]]; then
      echo "  refusing: the fetched ${name} spec is SMALLER than the committed one" >&2
      echo "            (${after} vs ${before} bytes, CR-normalised). The host you fetched from is behind" >&2
      echo "            the contract in git, and overwriting would delete types the apps already use." >&2
      echo "            Rebuild and restart the ${name} mobile API, or keep the committed spec." >&2
      echo "            Kept: ${out#"${IOS_ROOT}/../"} (unchanged). Fetched copy left at ${tmp##*/}." >&2
      refused=$((refused + 1))
      return 0
    fi

    mv "$tmp" "$out"
    refreshed=$((refreshed + 1))
    # Raw, not normalised: this is what was actually written to disk.
    echo "Wrote $(wc -c < "$out" | tr -d ' ') bytes to ${out#"${IOS_ROOT}/../"}."
    return 0
  fi
  rm -f "$tmp"

  # An explicitly named host is a deliberate request, so failing to reach it IS an error.
  if [ -n "$override_url" ]; then
    echo "error: could not fetch the ${name} OpenAPI spec from ${url}." >&2
    echo "       That URL was passed explicitly, so nothing was assumed." >&2
    exit 1
  fi

  # The default host is not running. That is not a failure: this script exists to pull a spec that
  # has CHANGED, and if the API is not up there is nothing to pull. The committed spec stays exactly
  # as it was, and `generate-api-clients.sh` reads it from disk and needs no host at all — so the
  # common case (regenerating clients from the spec already in git) must not be blocked by this.
  echo "  skipped: no ${name}-mobile-api host on ${url}." >&2
  echo "           The committed spec is untouched, which is correct unless you changed the backend" >&2
  echo "           contract. Start the host (dotnet run in Cleansia.Web.Mobile.${name^}) only if you" >&2
  echo "           did. To regenerate the CLIENTS you do not need this script at all." >&2
  skipped=$((skipped + 1))
}

case "$app" in
  all)
    refresh_one partner
    refresh_one customer
    ;;
  partner|customer)
    refresh_one "$app"
    ;;
  *)
    echo "error: unknown app '${app}' (expected 'partner', 'customer', or omit for both)." >&2
    exit 1
    ;;
esac

if [ "$refused" -gt 0 ]; then
  echo
  echo "${refused} spec(s) refused as a downgrade — see the reason(s) above. The committed specs stand."
  # Non-zero, unlike an unreachable host: a refusal means the running API and the contract in
  # git genuinely disagree, and that is a result worth failing on rather than a no-op.
  exit 1
fi

if [ "$refreshed" -eq 0 ]; then
  echo
  echo "Nothing was refreshed (${skipped} host(s) unreachable). The committed specs still stand."
  echo "If you only meant to rebuild the clients, that is ./scripts/generate-api-clients.sh — it reads"
  echo "the committed specs off disk and needs nothing running."
  exit 0
fi

echo "Spec(s) refreshed. Regenerate the clients with ./scripts/generate-api-clients.sh"
