#!/usr/bin/env bash
# Email-uniqueness census — ADR-0050 §D3. Run this BEFORE regenerating the Initial migration.
#
# WHY: T-0604 arms IX_Users_TenantId_Email with NULLS NOT DISTINCT, which is what finally makes
# "two accounts may not share an email" true at the database level. Postgres REFUSES to create such
# an index if rows already violate it — and until now nothing prevented them, because every TenantId
# is NULL and Postgres treats NULLs as distinct. So the migration cannot be applied blind.
#
# USAGE — the connection string never touches the repo:
#
#   PGPASSWORD='…' psql -h <host> -U <user> -d <db> -f scripts/email-uniqueness-census.sh   # no
#   ./scripts/email-uniqueness-census.sh "postgresql://user:pass@host:5432/dbname"          # yes
#
# or, if you already have DATABASE_URL exported:
#
#   ./scripts/email-uniqueness-census.sh
#
# It prints a verdict, not just rows. Exit 0 = safe to regenerate. Exit 1 = stop, decisions needed.

set -uo pipefail

CONN="${1:-${DATABASE_URL:-}}"
if [ -z "$CONN" ]; then
    echo "usage: $0 <postgres-connection-string>   (or export DATABASE_URL)" >&2
    exit 2
fi

command -v psql >/dev/null 2>&1 || { echo "psql not found on PATH" >&2; exit 2; }

q() { psql "$CONN" -qtAX -c "$1" 2>/dev/null; }

echo "=== Email-uniqueness census (ADR-0050 §D3) ==="
echo

# 1 — the blocking question. Email is citext, so this comparison is already case-insensitive and
#     matches the index exactly; there is no LOWER() to add.
DUP_EMAIL=$(q 'SELECT COUNT(*) FROM (SELECT "Email" FROM "Users" GROUP BY "Email" HAVING COUNT(*) > 1) d;')
# 2 — the same question scoped the way the index is actually declared.
DUP_PAIR=$(q 'SELECT COUNT(*) FROM (SELECT "TenantId", "Email" FROM "Users" GROUP BY "TenantId", "Email" HAVING COUNT(*) > 1) d;')
# 3 — not about duplicates: a non-zero answer means a tenant-stamped account exists in an environment
#     whose entire read path assumes none does. That is its own finding (ADR-0051), not this one's.
TENANTED=$(q 'SELECT COUNT(*) FROM "Users" WHERE "TenantId" IS NOT NULL;')

if [ -z "$DUP_EMAIL" ]; then
    echo "Could not reach the database, or \"Users\" does not exist. Nothing was measured." >&2
    exit 2
fi

printf 'duplicate emails (any tenant) : %s\n' "$DUP_EMAIL"
printf 'duplicate (TenantId, Email)   : %s\n' "$DUP_PAIR"
printf 'rows with a non-null TenantId : %s\n' "$TENANTED"
echo

FAIL=0

if [ "$DUP_EMAIL" != "0" ]; then
    FAIL=1
    echo "STOP — $DUP_EMAIL email(s) are held by more than one account."
    echo "The index cannot be created until each is resolved, and WHICH row survives is your call,"
    echo "not a mechanical one: the losing row may own orders, invoices or a payout destination."
    echo
    echo "The affected addresses and their row counts:"
    psql "$CONN" -c 'SELECT "Email", COUNT(*) AS accounts, MIN("CreatedAt") AS first_seen, MAX("CreatedAt") AS last_seen
                     FROM "Users" GROUP BY "Email" HAVING COUNT(*) > 1 ORDER BY COUNT(*) DESC;'
fi

if [ "$TENANTED" != "0" ]; then
    FAIL=1
    echo "FINDING — $TENANTED account(s) carry a non-null TenantId."
    echo "That is not a blocker for the index, but it contradicts what the whole read path assumes"
    echo "(single-tenant, TenantId always NULL). Worth understanding before the migration, because a"
    echo "tenant-stamped row is invisible to queries that run without an ambient tenant."
fi

if [ "$FAIL" = "0" ]; then
    echo "CLEAR — no duplicates, no tenant-stamped rows."
    echo "Safe to regenerate the Initial migration and apply it."
    exit 0
fi

exit 1
