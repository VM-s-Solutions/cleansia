#!/bin/bash
# DEV only: runs sql-scripts/dev-e2e-confirm-accounts.sql through the Execute SQL Script workflow and waits for it.
# Usage: confirm-accounts.sh [e-mail to expect in the log]. E2E_CONFIRM_REF names the pushed ref that holds the
# script (default: the current branch). Prints the run id, its conclusion and the confirmed rows; exits 0 only on success.
set -euo pipefail
REPO=VM-s-Solutions/cleansia
REF=${E2E_CONFIRM_REF:-$(git rev-parse --abbrev-ref HEAD)}
EXPECT=${1:-}
if [ -z "${GH_TOKEN:-}" ]; then
  GH_TOKEN=$(printf 'protocol=https\nhost=github.com\n\n' | git credential fill 2>/dev/null | sed -n 's/^password=//p')
  export GH_TOKEN
fi
T0=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
gh workflow run execute-sql.yml --repo "$REPO" --ref "$REF" -f environment=DEV -f script_path=dev-e2e-confirm-accounts.sql
id=
for _ in $(seq 1 30); do
  id=$(gh run list --repo "$REPO" --workflow execute-sql.yml --limit 10 --json databaseId,createdAt \
    --jq "[.[] | select(.createdAt >= \"$T0\")] | sort_by(.createdAt) | first | .databaseId // empty")
  [ -n "$id" ] && break
  sleep 5
done
[ -n "$id" ] || { echo "no execute-sql run appeared after $T0" >&2; exit 1; }
echo "execute-sql run $id (ref $REF)"
state=
for _ in $(seq 1 60); do
  state=$(gh run view "$id" --repo "$REPO" --json status,conclusion --jq '.status + " " + (.conclusion // "")')
  case "$state" in completed*) break ;; esac
  sleep 10
done
echo "execute-sql run $id: $state"
log=$(gh run view "$id" --repo "$REPO" --log 2>/dev/null | grep 'Execute SQL script' | sed 's/^.*Z //' || true)
echo "$log" | grep -E 'e2e\.|UPDATE|rows?\)|ERROR' || true
[ "$state" = "completed success" ] || exit 1
if [ -n "$EXPECT" ]; then echo "$log" | grep -qi "$EXPECT" || { echo "the run did not confirm $EXPECT" >&2; exit 1; }; fi
