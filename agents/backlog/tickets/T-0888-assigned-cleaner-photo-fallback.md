---
id: T-0888
title: Show assigned cleaner photos with initials fallback
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [backend, android, ios]
security_touching: true
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — Authorized order details expose the existing profile-photo blob contract and render it with a stable cache key.
- [x] **AC2** — Missing or failed images show initials from the visible masked name, preserving guest/browse/past-job redaction and contact masking.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS maps authorized profilePhoto: BlobFileDto? through existing ProfileAvatar/RemoteImageCache; visible first/last name components supply initials, fully masked/unknown names use ?. Native synthetic photo/initial captures in both themes and presentation tests pass. No private image URL is logged. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Optional generated photo contract and authorized detail/redaction/logging boundaries pass backend suites. Native captures cover loaded synthetic images and missing/error initials from disclosed names. Android successful-image evidence uses numeric local resource IDs; no live signed URL fetch is claimed.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
