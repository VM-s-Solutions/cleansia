#!/usr/bin/env bash
#
# Build-phase gate for the owner-local settings that live in the gitignored
# Config/Local.xcconfig (Stripe publishable key, Apple DEVELOPMENT_TEAM).
#
# It reads the RESOLVED build settings out of the Xcode environment rather than
# looking for the file, so an override injected another way (fastlane passes
# DEVELOPMENT_TEAM as an xcarg) counts as configured. Severity is graded so a
# fresh clone still builds and tests on the simulator — the empty-key path is
# fail-closed at runtime (card payment hidden, cash only) — while an unsigned
# device build or a keyless Release archive is a hard stop.
#
# Usage (from an Xcode Run Script phase):
#   scripts/check-local-config.sh STRIPE_PUBLISHABLE_KEY DEVELOPMENT_TEAM

set -uo pipefail

config_severity() {
  local setting="$1" value="$2" configuration="$3" platform="$4"
  local trimmed="${value#"${value%%[![:space:]]*}"}"
  trimmed="${trimmed%"${trimmed##*[![:space:]]}"}"
  [[ "$trimmed" == '$('* ]] && trimmed=""

  if [[ "$setting" == "STRIPE_PUBLISHABLE_KEY" && -n "$trimmed" && "$trimmed" != pk_* ]]; then
    echo error
    return
  fi
  if [[ -n "$trimmed" ]]; then
    echo ok
    return
  fi

  case "$setting" in
    DEVELOPMENT_TEAM)
      if [[ "$platform" == iphonesimulator ]]; then echo ok; else echo error; fi
      ;;
    STRIPE_PUBLISHABLE_KEY)
      if [[ "$configuration" == Release ]]; then echo error; else echo warning; fi
      ;;
    *)
      echo warning
      ;;
  esac
}

config_message() {
  local setting="$1" value="$2" local_xcconfig="$3"
  case "$setting" in
    STRIPE_PUBLISHABLE_KEY)
      if [[ -n "$value" ]]; then
        echo "STRIPE_PUBLISHABLE_KEY must be a Stripe PUBLISHABLE key (pk_...). A secret key must never be built into a client app. Fix it in $local_xcconfig"
      else
        echo "STRIPE_PUBLISHABLE_KEY is not set, so card payment is hidden and only cash is offered. Copy Config/Local.xcconfig.example to $local_xcconfig and set STRIPE_PUBLISHABLE_KEY to your pk_ key."
      fi
      ;;
    DEVELOPMENT_TEAM)
      echo "DEVELOPMENT_TEAM is not set, so this build cannot be signed for a device. Copy Config/Local.xcconfig.example to $local_xcconfig and set DEVELOPMENT_TEAM to your 10-character Apple Developer Team ID."
      ;;
    *)
      echo "$setting is not set. Copy Config/Local.xcconfig.example to $local_xcconfig and set it."
      ;;
  esac
}

main() {
  local script_dir ios_root local_xcconfig configuration platform status=0
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  ios_root="$(dirname "$script_dir")"
  local_xcconfig="${LOCAL_XCCONFIG:-$ios_root/Config/Local.xcconfig}"
  configuration="${CONFIGURATION:-Debug}"
  platform="${PLATFORM_NAME:-iphonesimulator}"

  local setting value severity
  for setting in "$@"; do
    value="${!setting:-}"
    severity="$(config_severity "$setting" "$value" "$configuration" "$platform")"
    [[ "$severity" == ok ]] && continue
    echo "$severity: $(config_message "$setting" "$value" "$local_xcconfig")"
    [[ "$severity" == error ]] && status=1
  done
  return "$status"
}

if [[ -z "${CHECK_LOCAL_CONFIG_SOURCED:-}" ]]; then
  main "$@"
fi
