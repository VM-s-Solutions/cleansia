#!/usr/bin/env bash
#
# Generates the typed Swift business API clients (swift5 + urlsession) from the
# SHARED committed mobile OpenAPI specs that Android also reads:
#
#   src/cleansia_android/openapi/partner-mobile-api.json   -> CleansiaPartnerApi
#   src/cleansia_android/openapi/customer-mobile-api.json  -> CleansiaCustomerApi
#
# Android (openapi-generator kotlin) and iOS (openapi-generator swift5) generate
# from the same two spec files, keeping both platforms aligned to one backend
# contract. Hand-written DTOs drifted silently from the backend on Android; the
# generated client turns every shape mismatch into a compile error instead.
#
# The auth/session/header spine is HAND-WRITTEN and lives in CleansiaCore/Auth;
# it is NOT generated and the spec's Auth/Device endpoints are out of scope for
# the generated client (the body-token transport + single-use refresh + theft
# detection can't be expressed in the generated surface). See README + ADR-0013.
#
# Generated output is NOT committed (see .gitignore). Never hand-edit it — change
# the spec (owner regen) or this script and regenerate.
#
# Usage:
#   ./scripts/generate-api-clients.sh            # both apps
#   ./scripts/generate-api-clients.sh partner    # one app
#   ./scripts/generate-api-clients.sh customer
#
# Requires `openapi-generator` 7.x on PATH (brew install openapi-generator).

set -euo pipefail

IOS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG_DIR="${IOS_ROOT}/openapi"
ANDROID_SPEC_DIR="${IOS_ROOT}/../cleansia_android/openapi"

apps=("partner" "customer")
if [[ $# -gt 0 ]]; then
  apps=("$@")
fi

if ! command -v openapi-generator >/dev/null 2>&1; then
  echo "error: openapi-generator not found on PATH." >&2
  echo "       Install it with: brew install openapi-generator" >&2
  exit 1
fi

for app in "${apps[@]}"; do
  case "$app" in
    partner|customer) ;;
    *)
      echo "error: unknown app '${app}' (expected 'partner' or 'customer')." >&2
      exit 1
      ;;
  esac

  spec="${ANDROID_SPEC_DIR}/${app}-mobile-api.json"
  config="${CONFIG_DIR}/openapi-generator-config.${app}.yaml"

  if [[ ! -f "$spec" ]]; then
    echo "error: spec not found: ${spec}" >&2
    echo "       Run ./scripts/refresh-mobile-spec.sh ${app} against the running mobile API first," >&2
    echo "       or wait for the owner mobile-spec-regen (manual_step: mobile-spec-regen)." >&2
    exit 1
  fi

  # Capitalize the first letter portably (macOS ships Bash 3.2, no ${app^}).
  cap="$(printf '%s' "${app:0:1}" | tr 'a-z' 'A-Z')${app:1}"
  echo "Generating Cleansia${cap}Api from ${app}-mobile-api.json ..."
  ( cd "$CONFIG_DIR" && openapi-generator generate -c "$config" )

  # openapi-generator (swift5) emits encodeToJSON() calls on AnyCodable free-form
  # parameters but omits AnyCodable's own JSONEncodable conformance, so the
  # generated client does not compile. Append it — deterministic, re-applied on
  # every run (the output is gitignored/machine-owned and never hand-edited).
  ext="$(find "${CONFIG_DIR}/../Cleansia${cap}Api" -name Extensions.swift | head -1)"
  if [[ -n "$ext" ]] && ! grep -q "extension AnyCodable: JSONEncodable" "$ext"; then
    cat >> "$ext" <<'SWIFT'

#if canImport(AnyCodable)
extension AnyCodable: JSONEncodable {
    func encodeToJSON() -> Any { value }
}
#endif
SWIFT
    echo "  + added AnyCodable JSONEncodable conformance"
  fi
done

echo "Done. Generated clients live under CleansiaPartnerApi/ and CleansiaCustomerApi/ (gitignored)."
echo "Open Cleansia.xcworkspace and let SPM resolve the local packages."
