#!/bin/bash
# DEV only, read-only: lists the fee receipts of the e2e orders (sql-scripts/dev-e2e-read-fee-receipts.sql, PAY-042).
# Usage: read-fee-receipts.sh [order id to expect in the log].
exec bash "$(dirname "$0")/dev-sql.sh" dev-e2e-read-fee-receipts.sql "$@"
