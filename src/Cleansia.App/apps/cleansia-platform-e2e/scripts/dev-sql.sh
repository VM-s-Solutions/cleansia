#!/bin/bash
# DEV only: runs one sql-scripts/ file through the Execute SQL Script workflow on DEV and waits for it.
# Usage: dev-sql.sh <script.sql> [text to expect in the log]. E2E_CONFIRM_REF names the pushed ref that holds the
# script (default: the current branch); E2E_SQL_CONFIRM is the dispatch's confirm, which the script reads as
# cleansia.confirm (default: none). Prints the run id, its conclusion and the script's output rows; exits 0 only
# on success (and, given a text, only when the log holds it).
set -euo pipefail
REPO=VM-s-Solutions/cleansia
REF=${E2E_CONFIRM_REF:-$(git rev-parse --abbrev-ref HEAD)}
SCRIPT=${1:?usage: dev-sql.sh <script.sql> [text to expect]}
EXPECT=${2:-}
if [ -z "${GH_TOKEN:-}" ]; then
  GH_TOKEN=$(printf 'protocol=https\nhost=github.com\n\n' | git credential fill 2>/dev/null | sed -n 's/^password=//p')
  export GH_TOKEN
fi
T0=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
out=$(gh workflow run execute-sql.yml --repo "$REPO" --ref "$REF" -f environment=DEV -f script_path="$SCRIPT" -f confirm="${E2E_SQL_CONFIRM:-}")
echo "$out"
# The dispatch prints its own run's URL, so a second dispatch seconds later cannot be taken for this one.
id=$(printf '%s\n' "$out" | sed -n 's#.*/actions/runs/\([0-9][0-9]*\).*#\1#p' | head -1)
# shortcut: without that URL, the first run created after T0 is taken, which another dispatch in the same seconds can cross.
for _ in $(seq 1 30); do
  [ -n "$id" ] && break
  id=$(gh run list --repo "$REPO" --workflow execute-sql.yml --limit 10 --json databaseId,createdAt \
    --jq "[.[] | select(.createdAt >= \"$T0\")] | sort_by(.createdAt) | first | .databaseId // empty")
  [ -n "$id" ] && break
  sleep 5
done
[ -n "$id" ] || { echo "no execute-sql run appeared after $T0" >&2; exit 1; }
echo "execute-sql run $id (ref $REF)"
state=
for _ in $(seq 1 60); do
  # A run just dispatched can answer 404 for a few seconds: keep polling rather than stop under set -e.
  state=$(gh run view "$id" --repo "$REPO" --json status,conclusion --jq '.status + " " + (.conclusion // "")' 2>/dev/null || true)
  case "$state" in completed*) break ;; esac
  sleep 10
done
echo "execute-sql run $id: $state"
log=$(gh run view "$id" --repo "$REPO" --log 2>/dev/null | grep 'Execute SQL script' | sed 's/^.*Z //' || true)
echo "$log" | grep -E 'e2e\.|UPDATE|INSERT|rows?\)|ERROR|\|' || true
[ "$state" = "completed success" ] || exit 1
if [ -n "$EXPECT" ]; then echo "$log" | grep -qi "$EXPECT" || { echo "the $SCRIPT log does not hold $EXPECT" >&2; exit 1; }; fi
