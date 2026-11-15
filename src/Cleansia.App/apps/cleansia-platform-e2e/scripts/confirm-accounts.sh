#!/bin/bash
# DEV only: confirms the e2e test accounts (sql-scripts/dev-e2e-confirm-accounts.sql). Usage: confirm-accounts.sh [e-mail to expect in the log].
exec bash "$(dirname "$0")/dev-sql.sh" dev-e2e-confirm-accounts.sql "$@"
