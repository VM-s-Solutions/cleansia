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
# (manual_step: mobile-spec-regen) — the specs are stale (pre-T-0272) and the
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

declare -A default_url=(
  [partner]="http://localhost:5002/swagger/v1/swagger.json"
  [customer]="http://localhost:5004/swagger/v1/swagger.json"
)

app="${1:-all}"
override_url="${2:-}"

refresh_one() {
  local name="$1"
  local url="${override_url:-${default_url[$name]}}"
  local out="${SPEC_DIR}/${name}-mobile-api.json"

  echo "Fetching ${name} spec from ${url} ..."
  if ! curl -fsS "$url" -o "$out"; then
    echo "error: could not fetch the ${name} OpenAPI spec from ${url}." >&2
    echo "       Is the ${name}-mobile-api host running? Pass a custom URL as the 2nd arg" >&2
    echo "       for a remote/dev host." >&2
    exit 1
  fi
  echo "Wrote $(wc -c < "$out" | tr -d ' ') bytes to ${out#"${IOS_ROOT}/../"}."
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

echo "Spec(s) refreshed. Regenerate the clients with ./scripts/generate-api-clients.sh"
