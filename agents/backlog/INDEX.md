# Backlog — INDEX

**One row per ticket. One status. This table is the only place a status lives.** See
[`README.md`](README.md) for why that sentence is the whole design.

**Status:** `todo` · `in_progress` · `blocked` · `done` · `dropped`
**Size:** S (< half a day) · M · L

> Filed 2026-08-25 from a transcript sweep of the whole session, then ground-truthed against
> master one item at a time. 17 further items were checked and found **already shipped** — they
> are not listed, because a done ticket in a queue is how four lanes got dispatched onto
> already-finished work on 2026-08-11. Everything below was verified still open.

> **Re-ground-truthed 2026-08-27, after #226, #227 and #228 merged.** 24 rows were re-checked
> against master with file-level evidence. Four were already shipped by #228 and are now `done`
> (T-0611, T-0612, T-0614, T-0616); three have a fix on master that nobody has confirmed on a
> device and are `blocked` on the owner rather than on an agent (T-0617, T-0618, T-0621); five
> had a premise the tree contradicted and are retitled (T-0613, T-0615, T-0626, T-0628, T-0631,
> T-0643). **A row filed as open is not evidence that it is open** — that is the whole reason this
> pass exists, and the reason to run one again before dispatching any lane.
> **The blocked rows were re-grounded the same day.** Every question a `blocked` row named was
> researched against the tree. Two rows named a question about something else entirely — `Q-INFRA-01`
> is the custom-domain decision and `Q-INFRA-02` is one-subscription-vs-two, neither of which gates
> the row that cited it. Several rows were not blocked at all: the Azure cost cut was designed,
> measured and committed on 2026-08-11, the docs getting-started page was written on 2026-08-24, and
> the role-name hyphen bug was fixed on 2026-08-25. **A `blocked` tag is a claim about the past too.**

> `blocked` rows name a question in [`questions/open.md`](questions/open.md). A blocked row is
> waiting on the owner, not on an agent.

| ID | Title | Size | Status | Owner | PR |
|---|---|---|---|---|---|
| T-0607 | Onboarding: tappable step dots let a cleaner go back (iOS + Android) | M | `done` | — | #227 |
| T-0608 | Onboarding: the 5 iOS/Android stepper divergences reconciled | M | `done` | — | #227 |
| T-0609 | Partner login: the brand hold no longer replays after sign-in (iOS) | M | `done` | — | #227 |
| T-0610 | Partner web: dead availability module removed, with a backend null-guard | S | `done` | — | #227 |
| T-0611 | Partner web: registration-number label from country config, drop hardcoded IČO | S | `done` | — | #228 |
| T-0612 | Partner web: replace-document UI (mobile has it, web does not) | S | `done` | — | #228 |
| T-0613 | Admin web: UI for document requirements CRUD and the deletion queue — new feature lib, 2 screens, 5 locales | M | `todo` | — | — |
| T-0614 | Documents checklist: iOS shows retry on load failure, Android renders the list | S | `done` | — | #228 |
| T-0615 | Admin web: registration-number label — the admin host has **no field-labels route at all**, so a regen alone yields nothing; needs a backend action first, then `manual_step: nswag-regen` | M | `todo` | — | — |
| T-0616 | Backend: missingFields emits camelCase keys, every locale defines snake_case | S | `done` | — | #228 |
| T-0617 | Customer iOS: swipe-to-return on order detail — third fix shipped, unconfirmed — **Q-DEVICE-01** | M | `blocked` | — | — |
| T-0618 | Customer apps: cancelled-order location — fix shipped, report was ambiguous — **Q-DEVICE-01** | S | `blocked` | — | — |
| T-0619 | Customer apps: cancel-order button unreadable when disabled | S | `todo` | — | — |
| T-0620 | Android core: terms-and-agreement checkbox line is not aligned | S | `todo` | — | — |
| T-0621 | Partner iOS: new-order address placeholder — centring already shipped, may be a stale re-file — **Q-DEVICE-01** | S | `blocked` | — | — |
| T-0622 | Android apps: photo upload disabled — which surface is meant is unrecorded — **Q-AND-02** | S | `blocked` | — | — |
| T-0623 | Partner Android top whitespace — the only matching defect was fixed 2026-08-23, two days before this was filed; needs a yes/no — **Q-AND-01** | S | `blocked` | — | — |
| T-0624 | Logout label not centred — exactly one offender, customer Android profile; what is left is a design call — **Q-UI-01** | S | `blocked` | — | — |
| T-0625 | Partner onboarding mascots: use different ones (iOS) | S | `todo` | — | — |
| T-0626 | Sign out: confirm before logging out — 3 places left (admin unauthorized page, partner splash-unreachable on iOS + Android) | S | `todo` | — | — |
| T-0627 | Customer web: serviceability banner fires on a serviced city | M | `todo` | — | — |
| T-0628 | Customer mobile: serviceability at address selection — iOS booking review pane + saved-address path on both | M | `todo` | — | — |
| T-0629 | All apps: a translation longer than its control must not be truncated — **Q-I18N-01** | L | `blocked` | — | — |
| T-0630 | Frontends behind App Registration — the hyphen bug is fixed and docs is gated; the **scope** is the open call — **Q-SEC-01** | L | `blocked` | — | — |
| T-0631 | iOS/Android parity: audit is **done**; needs a ruling on Android partner having no camera-capture path — **Q-PARITY-01** | S | `blocked` | — | — |
| T-0632 | SendNewJobsDigestTimerFunction never fires — fix merged and deployed 2026-08-23, needs one portal confirmation — **Q-AZURE-01** | M | `blocked` | — | — |
| T-0633 | Bicep audit — blocked on **scope**, not on Q-INFRA-02 (which is one-sub-vs-two and already defaulted) — **Q-AUDIT-01** | M | `blocked` | — | — |
| T-0634 | Log Analytics is **required** by workspace-based App Insights — deleting it by hand is a cost regression — **Q-AZURE-01** | S | `blocked` | — | — |
| T-0635 | Azure cost: the dev cut is already committed (sampling 10%, 500 MB cap) — needs a portal reading, not a decision — **Q-AZURE-01** | S | `blocked` | — | — |
| T-0636 | Partner DEV API returned 500 from /health for 4 minutes after deploy | S | `todo` | — | — |
| T-0637 | Warm-up gate cannot tell 5xx from cold start and discards the body | S | `todo` | — | — |
| T-0638 | RequestLoggingMiddleware ordering — "nothing logged" is false, and today's order is what makes an oversize upload answer 413 — **Q-BE-01** | S | `blocked` | — | — |
| T-0639 | refresh-mobile-spec.sh throws on the owner's machine | S | `todo` | — | — |
| T-0640 | Many .cs files show as modified with no content change | S | `todo` | — | — |
| T-0641 | Move the repo to private without losing Actions minutes — **Q-REPO-01** | M | `blocked` | — | — |
| T-0642 | docs: a getting-started page for running Cleansia locally | S | `done` | — | 7651ee69 |
| T-0643 | Root README: six stale facts, not a rewrite (~180 of 203 lines verified accurate) | S | `todo` | — | — |
| T-0644 | iOS: enum L10n.Profile is at 393 against a 400 type_body_length cap | S | `todo` | — | — |

*Next id: **T-0645**.*
