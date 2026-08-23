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

app="${1:-all}"
override_url="${2:-}"

skipped=0

refresh_one() {
  local name="$1"
  local url="${override_url:-$(default_url_for "$name")}"
  local out="${SPEC_DIR}/${name}-mobile-api.json"

  echo "Fetching ${name} spec from ${url} ..."
  if curl -fsS "$url" -o "$out"; then
    echo "Wrote $(wc -c < "$out" | tr -d ' ') bytes to ${out#"${IOS_ROOT}/../"}."
    return 0
  fi

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

if [ "$skipped" -gt 0 ]; then
  echo
  echo "Nothing was refreshed (${skipped} host(s) unreachable). The committed specs still stand."
  echo "If you only meant to rebuild the clients, that is ./scripts/generate-api-clients.sh — it reads"
  echo "the committed specs off disk and needs nothing running."
  exit 0
fi

echo "Spec(s) refreshed. Regenerate the clients with ./scripts/generate-api-clients.sh"
