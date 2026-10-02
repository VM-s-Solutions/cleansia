#!/usr/bin/env bash
# Warm one deployed site and prove it answers: GET the URL until it returns HTTP 200, 30 attempts
# 10 s apart (5 minutes). Exits 1 when that budget runs out, so a site that never goes healthy is a
# failed deploy, not a green one found out about from a white screen.
#
#   warm-site.sh <url> [marker]
#
# marker  an extended regex the 200 body must also match. Given one, the request carries a cookie:
#         the customer SSR's server.ts skips its landing-page micro-cache when any cookie is present,
#         so a cached render cannot pass for a fresh one.
#
# Callers: each DEV leg of deploy-api (its own /health) and warm-dev-sites (the customer SSR, with
# Angular's server-render marker), both in .github/workflows/deploy-azure.yml.
set -u

url="${1:?usage: warm-site.sh <url> [marker]}"
marker="${2:-}"
name="${url#*://}"
name="${name%%/*}"
body="${RUNNER_TEMP:-/tmp}/warm-${name}.txt"
curl_args=()
if [ -n "$marker" ]; then
  curl_args+=(--cookie 'cleansia_ssr_probe=1')
fi

for attempt in $(seq 1 30); do
  code="$(curl -sS "${curl_args[@]}" -o "$body" -w '%{http_code}' --max-time 15 "$url" || true)"
  if [ "$code" = "200" ]; then
    if [ -z "$marker" ] || grep -Eq "$marker" "$body"; then
      echo "::notice::$name warm after ${attempt} attempt(s)"
      exit 0
    fi
    echo "::warning::$name returned HTTP 200 without a server-rendered app-root at $url"
  fi
  case "$code" in
    000|408|429|502|503|504)
      # No answer, or the platform answering for a worker that is not up. 503 covers
      # both an Unhealthy report and Azure's own cold-container page, so it retries.
      echo "  $name not up yet (HTTP $code, attempt $attempt/30) — retrying in 10s…"
      ;;
    *)
      # Up and answering wrongly. Dev does NOT fail fast on this: T-0636 records a
      # self-healing 500 that lasted four minutes, well inside this 5-minute budget,
      # and failing early would turn that run red while fixing nothing. Surfacing the
      # body is the point — it is what made T-0636 undiagnosable.
      echo "::warning::$name ANSWERED HTTP $code — up, but not healthy (attempt $attempt/30)"
      echo "  response body:"
      if [ -s "$body" ]; then
        awk 'NR<=20 { print "  | " substr($0, 1, 300) } NR==21 { print "  | (truncated)"; exit }' "$body"
      else
        echo "  | (empty)"
      fi
      ;;
  esac
  sleep 10
done
echo "::error::$name never went healthy at $url"
exit 1
