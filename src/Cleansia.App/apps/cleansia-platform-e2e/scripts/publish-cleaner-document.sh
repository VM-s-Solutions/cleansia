#!/bin/bash
# DEV only: publishes a new version of the CZ cleaner documents (sql-scripts/dev-e2e-publish-cleaner-document.sql,
# TAKE-025). IT GATES EVERY DEV CLEANER until they accept again: run it only as the last step of Day 2 of an E2E run
# (e2e/README.md, "Running"), never on a scratch run. The script publishes only when the dispatch's confirm names it,
# which this wrapper alone passes.
if [ "${E2E_SCRATCH:-}" = "1" ]; then echo "E2E_SCRATCH=1: the cleaner documents are never published on a scratch run" >&2; exit 1; fi
E2E_SQL_CONFIRM=dev-e2e-publish-cleaner-document.sql exec bash "$(dirname "$0")/dev-sql.sh" dev-e2e-publish-cleaner-document.sql "$@"
