---
id: T-0465
title: Avatars are not cached at all — no Cache-Control and a SAS query string that changes on every mint
status: draft
size: S
owner: backend
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0446, T-0464]
blocks: []
stories: []
adrs: []
layers: [backend, architect]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Filed from **QA's T-0446 AC4 run** (DEF-3). **Every profile load re-downloads the full avatar image.**
Gate 5.

**It is over-determined — two independent causes, and fixing one does not fix it:**

1. **No `Cache-Control` on the response.** Same root cause as **T-0464**: `Metadata.CacheMetadata`'s
   value goes to `x-ms-meta-CacheControl` and never reaches a real header.
2. **The SAS query string changes on every mint.** `se` (expiry) and `sig` are recomputed on every
   profile read, so **the HTTP cache key changes every time** even if a perfect `Cache-Control` were
   present. An HTTP cache keys on the full URL including query.

**Cause 2 is inherent to the chosen design** (ADR option A: a fresh short-lived SAS per read). It
should be **stated as accepted, not silently carried** — that is most of what this ticket is for.

**T-0464's SAS response-header override fixes cause 1 only.** QA's executed run of that override
returned `Cache-Control: max-age=3600, private` — correct and condition-compliant, but it buys
nothing while cause 2 changes the key underneath it.

## What "fixed" can realistically mean here

The honest options, and this ticket's job is to **choose and record**, not to assume:

- **A. Accept it, document it, cache in the clients.** The clients are already instructed to cache on
  **`fileName`** (T-0447/0448/0449 conditions) — a stable key that changes only when the user
  actually replaces the photo (T-0446 AC10). Coil and Kingfisher both support an explicit cache key
  decoupled from the URL, so **the client layer can cache correctly even though HTTP cannot.** The web
  is the weak leg: `<img [src]>` with a changing URL will re-fetch, and CORS (T-0447 C2) blocks the
  workarounds.
- **B. Stabilise the URL** — round the SAS expiry to a coarse boundary (e.g. the top of the hour) so
  the same URL is minted for all reads within a window. Makes the URL cacheable at the cost of a
  slightly longer effective credential lifetime and a **security question** that must be answered
  explicitly, not assumed.
- **C. Do nothing.** Legitimate if the measured cost is small — avatars are small images and the
  profile is not a hot loop. **Requires the measurement**, not an assertion.

## Deliberation

**Architect call, short.** B changes a security bound (effective SAS lifetime and its predictability)
and therefore cannot be chosen by an implementer alone. A and C are recordable decisions. If B is
chosen, **the security reviewer must re-gate**, because a predictable, longer-lived and now
*shared-across-reads* URL is a different exposure from a per-read one.

## Acceptance criteria

- [ ] **AC1 (measure before deciding)** — The actual cost is measured and recorded: avatar payload
      size, and re-downloads per typical session across the three clients. **A decision made without
      this number is guesswork** — and option C cannot be justified without it.
- [ ] **AC2** — One of A / B / C is chosen and **written down with its reasoning** in the living
      decision doc (`agents/architecture/decisions/`), including the explicit statement that **cause 2
      is inherent to the per-read-SAS design and is being accepted** (if A or C).
- [ ] **AC3** — If **B**: the security reviewer re-gates the widened/predictable SAS window and the
      verdict is recorded. **Do not implement B without that verdict.**
- [ ] **AC4** — The three client tickets' caching guidance is confirmed **consistent** with the
      choice. They currently say "cache on `fileName`, never on `blobUrl`" — under A that is exactly
      right and should be reinforced; under B it may become unnecessary on some platforms. **Do not
      leave three tickets carrying advice that contradicts the decision.**
- [ ] **AC5** — Whatever is chosen, `Cache-Control` on an avatar is **`private`**, never `public`.
      Carried from the T-0446 security gate and shared with **T-0464 AC5**.

## Out of scope

- The blob `Content-Type` defect and the SAS override mechanism — **T-0464**. This ticket **consumes**
  that fix; it does not build it.
- Client-side cache implementation — **T-0447 / T-0448 / T-0449**.
- CDN / image-resizing service. If the measurement in AC1 is bad enough to suggest one, **stop and
  tell the PM** rather than growing this ticket.

## Implementation notes

- **⚠️ SHARED-FILE LANE — the SAS mint (`BlobContainerClient.cs`): T-0446 → T-0464 → T-0465.**
  This ticket is **last** in that lane and depends on T-0464's override existing.
- If the answer is A or C, the deliverable is mostly **a written decision plus a measurement** — that
  is a legitimate outcome and should not be padded into a code change to look like work.

## Status log
- 2026-07-30 — draft (created by pm from QA's T-0446 AC4 run, DEF-3)
- 2026-07-30 — **not `ready`**: `depends_on: [T-0446, T-0464]`; also wants a short architect call because option B moves a security bound.

## Review
<!-- reviewer verdict here -->
