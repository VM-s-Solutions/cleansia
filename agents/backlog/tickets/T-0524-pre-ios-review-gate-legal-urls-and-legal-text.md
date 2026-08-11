---
id: T-0524
title: PRE-REVIEW GATE — the apps' Terms and Privacy links point at a host that has never been deployed
status: blocked
size: S
owner: ios
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0016]
layers: [ios, android, frontend, docs]
security_touching: false
manual_steps: []
sprint: 15
---

> **This is a GATE, not today's work.** The owner's ruling, 2026-08-02: *"legal text to be added later;
> use DEV URLs for now."* **Recorded as a pre-iOS-review gate. It blocks nothing in the current
> sprint** — and it will fail an App Store submission if it reaches one unresolved.

## Context

**PM-verified first-hand at `master` 2026-08-02.** Both mobile apps route their consent links through a
single origin constant, which is good design and currently points somewhere that does not serve:

| Platform | File | Value |
|---|---|---|
| iOS | `CleansiaCore/Sources/CleansiaCore/Config/CleansiaWeb.swift:8-21` | `domain = "cleansia.cz"`; `termsURL = /terms`; `privacyURL = /privacy` |
| Android | `core/.../config/CleansiaWeb.kt:13-20` | `DOMAIN = "cleansia.cz"`; `TERMS_URL`, `PRIVACY_URL` — same shape |
| Consumers | iOS `ConsentMarkdown.swift:12-13`; Android `ConsentMarkup.kt:17-18` | the signup consent text's tappable links |

The customer web app **does** define the routes (`apps/cleansia.app/src/app/app.routes.ts:140-147`,
lazy-loading `legal-pages`). **The problem is where they are served from: `sprint-15.md` records that
production has never been deployed** — DEV is the only live environment. So the links in a shipped
build resolve to a host with no app behind it.

**Why this is an App Review item and not a nicety.** A reachable privacy policy is a submission
requirement, and a consent checkbox whose links 404 is also a GDPR-transparency problem, not only a
store one — it interacts directly with **T-0507** (consent required on web, never persisted, never
asked on mobile).

**The fix itself is genuinely one line per platform**, because both apps already funnel through a
single constant — the file comment on `CleansiaWeb.swift:3-6` says so explicitly: *"A move off `.cz`
… must be a one-line edit here — never a grep across two apps and five locale catalogs."* **That
design decision is what makes this a gate rather than a project.**

## Acceptance criteria

- [ ] **AC0 — the owner confirms which origin ships in the review build**, and by when real legal text
      exists at it. Tracked as `Q-IOS-LEGAL-01` (`blocking: no`, **resolve-by: `pre-submission`**).
- [ ] **AC1 — the current state is re-established at gate time, not assumed.** Do
      `https://cleansia.cz/terms` and `/privacy` serve content **at the moment of submission**? Do the
      DEV equivalents? Evidence: the two fetches with status codes.
- [ ] **AC2 — the origin used by the review build serves real legal text in at least the review
      language.** Not a placeholder, not a 404, not a redirect to the marketing home page. Evidence:
      the fetched content.
- [ ] **AC3 — the switch is the one-line edit both platforms were designed for.** `CleansiaWeb.swift:8`
      and `CleansiaWeb.kt:13`. **If it turns out not to be one line, that is itself the finding** —
      say so, because the file comment claims otherwise and a false claim in a comment is worse than no
      comment. Evidence: the diff, or the correction.
- [ ] **AC4 — web parity.** The customer web app's own footer/consent links resolve wherever it is
      deployed. Evidence: the check.
- [ ] **AC5 — the App Store Connect privacy-policy URL matches** what the app links to. A mismatch
      between the binary and the listing is its own rejection. Evidence: the ASC field.
- [x] **AC6 — a row is added to `agents/backlog/ios-app-review-checklist.md`** so this cannot be
      forgotten at submission time. **DONE by the PM at filing time — `AR-PRIV-5`** — because a gate
      that lives only in a `blocked` ticket is a gate that gets missed. Evidence: the row.
- [ ] **AC7 — the interaction with T-0507 is stated.** That ticket covers consent capture and
      persistence; **this one covers whether the text the consent points at exists.** They are
      different failures of the same flow and both must be true before a real user consents to
      anything. Evidence: the cross-note on T-0507.

## Out of scope

- **Writing the legal text.** The owner's, or their lawyer's. No agent drafts terms or a privacy
  policy.
- **Deploying production.** A separate matter; **this gate must pass on whatever host actually ships.**
- **Consent capture and persistence** — **T-0507**.
- **The privacy manifest / nutrition label** — `ios-app-review-checklist.md` AR-PRIV-1/2, already
  tracked there.
- **Changing the origin today.** The owner ruled DEV URLs for now; **this ticket does not make that
  change until AC0 confirms which origin ships.**

## Implementation notes

**Do not dispatch this now.** It is filed so the decision is written down and surfaces at the
pre-submission checkpoint rather than during a review rejection.

**Read first:** `CleansiaWeb.swift`, `CleansiaWeb.kt`, `ConsentMarkdown.swift`, `ConsentMarkup.kt`,
`apps/cleansia.app/src/app/app.routes.ts:135-150`, ADR-0016 (Apple review compliance),
`agents/backlog/ios-app-review-checklist.md`, and **T-0507**.

## Status log
- 2026-08-02 — **filed `blocked` as a GATE (created by pm from the owner's housekeeping answers).**
  The owner: *legal text later, DEV URLs for now.* **Recorded as a pre-iOS-review gate, explicitly not
  a blocker today**, per instruction. **PM-verified:** both mobile apps resolve their consent links
  from a single origin constant pointing at `cleansia.cz`, and **production has never been deployed** —
  so the links in a build today go to a host with no app behind it. `Q-IOS-LEGAL-01` filed
  `blocking: no`, `resolve-by: pre-submission`.

## Review
