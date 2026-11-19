#!/bin/bash
# DEV only: confirms the e2e test accounts (sql-scripts/dev-e2e-confirm-accounts.sql). Usage: confirm-accounts.sh [text to expect in the log].
# The log lists only the accounts this dispatch confirmed, not one another dispatch confirmed first: confirmAccounts()
# in src/support/flows.ts proves an account by signing it in instead (F-087).
exec bash "$(dirname "$0")/dev-sql.sh" dev-e2e-confirm-accounts.sql "$@"
