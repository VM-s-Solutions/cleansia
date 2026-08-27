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
| T-0617 | Customer iOS: swipe-to-return on order detail | M | `done` | — | 7ad5bdfc |
| T-0618 | Cancelled orders: show the real map — the status guard is in **all four** mobile apps, plus 6 test blocks that assert the suppression by name | M | `todo` | — | — |
| T-0619 | Customer apps: cancel-order button unreadable when disabled | S | `todo` | — | — |
| T-0620 | Android core: terms-and-agreement checkbox line is not aligned | S | `todo` | — | — |
| T-0621 | Partner iOS: new-order address placeholder centring | S | `done` | — | c4acb926 |
| T-0622 | Partner iOS: wire the existing avatar control into the onboarding Personal step — **owner overturned the ruling 2026-08-27: match Android** | S | `todo` | — | — |
| T-0623 | Partner Android: unwanted whitespace at the top | S | `done` | — | 5c70922f |
| T-0624 | Customer apps: adopt partner's Logout / Delete-account rows — **iOS order is already correct**, Android has them inverted and left-aligned | M | `todo` | — | — |
| T-0625 | Partner onboarding mascots: use different ones (iOS) | S | `todo` | — | — |
| T-0626 | Sign out: confirm before logging out — 3 places left (admin unauthorized page, partner splash-unreachable on iOS + Android) | S | `todo` | — | — |
| T-0627 | Customer web: serviceability banner fires on a serviced city | M | `todo` | — | — |
| T-0628 | Customer mobile: serviceability at address selection — iOS booking review pane + saved-address path on both | M | `todo` | — | — |
| T-0629 | i18n policy: **ellipsis is allowed** on a single-line control — ratify Android's shipped position and port it to iOS and web | M | `todo` | — | — |
| T-0630 | Gate the **admin console** behind the same invitation block the docs site uses (docs + admin, not all four) | M | `todo` | — | — |
| T-0631 | Android partner: **add a camera-capture path** for job photos and avatar (restore CAMERA + uses-feature required=false) | M | `todo` | — | — |
| T-0632 | SendNewJobsDigest fires, but **irregularly — 8 invocations in 30 days, not hourly**; the host looks to be sleeping | M | `todo` | — | — |
| T-0633 | Bicep audit: one-day three-axis read — secrets/identity, drift, cost — needs one non-mutating `what-if` dispatch | M | `todo` | — | — |
| T-0634 | Log Analytics is required by workspace-based App Insights — closed, not a defect | S | `done` | — | d5b020bf |
| T-0635 | Azure cost: the dev cut was designed, measured and committed on 2026-08-11 — owner closed it without resolving further | S | `done` | — | d5b020bf |
| T-0636 | Partner DEV API returned 500 from /health for 4 minutes after deploy | S | `todo` | — | — |
| T-0637 | Warm-up gate cannot tell 5xx from cold start and discards the body | S | `todo` | — | — |
| T-0638 | RequestLoggingMiddleware: use `context.TraceIdentifier` as the request id in all 5 hosts so the log lines join — keeps the 413 | S | `todo` | — | — |
| T-0639 | refresh-mobile-spec.sh throws on the owner's machine | S | `todo` | — | — |
| T-0640 | Many .cs files show as modified with no content change | S | `todo` | — | — |
| T-0641 | Repo stays **public for now** — revisit when the owner's Actions limit resets | M | `todo` | — | — |
| T-0642 | docs: a getting-started page for running Cleansia locally | S | `done` | — | 7651ee69 |
| T-0643 | Root README: six stale facts, not a rewrite (~180 of 203 lines verified accurate) | S | `todo` | — | — |
| T-0644 | iOS: enum L10n.Profile is at 393 against a 400 type_body_length cap | S | `todo` | — | — |

| T-0645 | Partner iOS: the address section has **no city-level serviceability at all** and renders 1 of its states — Android has 4 — **needs a call on how many to port** | M | `todo` | — | — |
| T-0646 | Customer orders-list card: remove the status-coloured left stripe — **iOS and Android both have it**, nothing tests it | S | `todo` | — | — |
| T-0647 | Customer Android: pull-to-refresh on **HomeTab and RecurringBookings only** — the other 8 screens already have it — needs a call on what drives isRefreshing on Home | S | `todo` | — | — |
| T-0648 | iOS: render a disabled field as **greyed, matching Android** — fix lands in `CleansiaTextField`, used by nearly every form in both apps | M | `todo` | — | — |
| T-0649 | Partner Identification: give Self-employed / Legal entity the Plus page's **animated sliding indicator** (neither page actually swipes) — only `legalEntityName` differs between the two | M | `todo` | — | — |
| T-0650 | CI: cancel superseded iOS runs, ceiling every CI job | S | `done` | — | 00f4f729 |
| T-0651 | Customer iOS: add the always-visible **Recurring bookings** row to the profile tab — the destination already renders the Plus upsell, only the entry point is missing | S | `todo` | — | — |

*Next id: **T-0652**.*
