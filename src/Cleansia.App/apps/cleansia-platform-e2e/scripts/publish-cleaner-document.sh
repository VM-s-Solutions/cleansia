#!/bin/bash
# DEV only: publishes a new version of the CZ cleaner documents (sql-scripts/dev-e2e-publish-cleaner-document.sql,
# TAKE-025). IT GATES EVERY DEV CLEANER until they accept again: run it only as the last step of Day 2 of the phase-2
# run (e2e/README.md, "Running"), never on a scratch run. The script itself refuses any other day.
if [ "${E2E_SCRATCH:-}" = "1" ]; then echo "E2E_SCRATCH=1: the cleaner documents are never published on a scratch run" >&2; exit 1; fi
exec bash "$(dirname "$0")/dev-sql.sh" dev-e2e-publish-cleaner-document.sql "$@"
