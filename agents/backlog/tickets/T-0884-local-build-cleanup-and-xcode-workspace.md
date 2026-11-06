---
id: T-0884
title: Clean obsolete local builds and document fresh Xcode runs
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [docs]
security_touching: false
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — Remove only inventoried generated products and superseded Cleansia archives, preserving build18 artifacts, evidence and signing inputs.
- [x] **AC2** — Explain workspace/source selection separately from local version labels and TestFlight numbering; document regeneration from project.yml.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

## Local verification review — 29 October 2026

Removed 38 inventoried generated targets / 7,859,773,440 allocated bytes, preserving current build18 release artifacts and the open owner checkout/cache. README explains the correct workspace, Run, project.yml regeneration and archive-only build numbering. Exact receipts are in the local mobile-refinements-20261029 report.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
