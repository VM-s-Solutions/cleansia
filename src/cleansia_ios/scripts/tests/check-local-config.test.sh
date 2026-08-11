#!/usr/bin/env bash
#
# Tests for scripts/check-local-config.sh — the build-phase gate that turns a
# missing Config/Local.xcconfig into a named diagnostic instead of a silent
# build with a dead Stripe path.
#
# Run: ./scripts/tests/check-local-config.test.sh

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../check-local-config.sh
CHECK_LOCAL_CONFIG_SOURCED=1 source "$SCRIPT_DIR/../check-local-config.sh"

passed=0
failed=0

expect_severity() {
  local expected="$1" setting="$2" value="$3" configuration="$4" platform="$5"
  local actual
  actual="$(config_severity "$setting" "$value" "$configuration" "$platform")"
  if [[ "$actual" == "$expected" ]]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    echo "FAIL: config_severity($setting, '$value', $configuration, $platform) = $actual, expected $expected"
  fi
}

expect_run() {
  local label="$1" expected_exit="$2" expected_needle="$3"
  shift 3
  local output actual_exit
  output="$("$@" 2>&1)"
  actual_exit=$?
  if [[ "$actual_exit" != "$expected_exit" ]]; then
    failed=$((failed + 1))
    echo "FAIL: $label exited $actual_exit, expected $expected_exit"
    return
  fi
  if [[ -n "$expected_needle" && "$output" != *"$expected_needle"* ]]; then
    failed=$((failed + 1))
    echo "FAIL: $label output missing '$expected_needle'; got: $output"
    return
  fi
  if [[ -z "$expected_needle" && -n "$output" ]]; then
    failed=$((failed + 1))
    echo "FAIL: $label expected no output; got: $output"
    return
  fi
  passed=$((passed + 1))
}

run_main() {
  CONFIGURATION="$1" PLATFORM_NAME="$2" STRIPE_PUBLISHABLE_KEY="$3" DEVELOPMENT_TEAM="$4" \
    LOCAL_XCCONFIG="/repo/src/cleansia_ios/Config/Local.xcconfig" \
    bash "$SCRIPT_DIR/../check-local-config.sh" STRIPE_PUBLISHABLE_KEY DEVELOPMENT_TEAM
}

expect_severity ok      STRIPE_PUBLISHABLE_KEY "pk_test_123"              Debug   iphonesimulator
expect_severity ok      STRIPE_PUBLISHABLE_KEY "pk_live_123"              Release iphoneos
expect_severity warning STRIPE_PUBLISHABLE_KEY ""                         Debug   iphonesimulator
expect_severity error   STRIPE_PUBLISHABLE_KEY ""                         Release iphoneos
expect_severity warning STRIPE_PUBLISHABLE_KEY '$(STRIPE_PUBLISHABLE_KEY)' Debug  iphonesimulator
expect_severity warning STRIPE_PUBLISHABLE_KEY "   "                       Debug  iphonesimulator
expect_severity error   STRIPE_PUBLISHABLE_KEY "sk_live_123"               Debug  iphonesimulator
expect_severity error   STRIPE_PUBLISHABLE_KEY "rk_live_123"               Release iphoneos

expect_severity ok      DEVELOPMENT_TEAM ""           Debug   iphonesimulator
expect_severity ok      DEVELOPMENT_TEAM ""           Release iphonesimulator
expect_severity error   DEVELOPMENT_TEAM ""           Debug   iphoneos
expect_severity error   DEVELOPMENT_TEAM ""           Release iphoneos
expect_severity ok      DEVELOPMENT_TEAM "ABCDE12345" Release iphoneos

expect_severity warning SOME_OTHER_SETTING ""      Debug iphonesimulator
expect_severity ok      SOME_OTHER_SETTING "value" Debug iphonesimulator

expect_run "fresh clone, simulator debug" 0 \
  "warning: STRIPE_PUBLISHABLE_KEY" \
  run_main Debug iphonesimulator "" ""

expect_run "fresh clone names the file to create" 0 \
  "/repo/src/cleansia_ios/Config/Local.xcconfig" \
  run_main Debug iphonesimulator "" ""

expect_run "release archive without a key fails the build" 1 \
  "error: STRIPE_PUBLISHABLE_KEY" \
  run_main Release iphoneos "" "ABCDE12345"

expect_run "device build without a team fails the build" 1 \
  "error: DEVELOPMENT_TEAM" \
  run_main Debug iphoneos "pk_test_123" ""

expect_run "secret key in a client build fails the build" 1 \
  "error: STRIPE_PUBLISHABLE_KEY" \
  run_main Debug iphonesimulator "sk_live_123" ""

expect_run "fully configured build is silent" 0 "" \
  run_main Release iphoneos "pk_live_123" "ABCDE12345"

echo "check-local-config: $passed passed, $failed failed"
[[ "$failed" -eq 0 ]]
