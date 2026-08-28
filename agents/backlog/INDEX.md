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

> **Batches 1 and 2 closed 2026-08-27** (#230, #231). Ten rows above went `done` in one 
> pass because the INDEX was not updated as each merged — the same drift this file's 
> second provenance note was written about. Close a row when its PR merges, not later.

| ID | Title | Size | Status | Owner | PR |
|---|---|---|---|---|---|
| T-0607 | Onboarding: tappable step dots let a cleaner go back (iOS + Android) | M | `done` | — | #227 |
| T-0608 | Onboarding: the 5 iOS/Android stepper divergences reconciled | M | `done` | — | #227 |
| T-0609 | Partner login: the brand hold no longer replays after sign-in (iOS) | M | `done` | — | #227 |
| T-0610 | Partner web: dead availability module removed, with a backend null-guard | S | `done` | — | #227 |
| T-0611 | Partner web: registration-number label from country config, drop hardcoded IČO | S | `done` | — | #228 |
| T-0612 | Partner web: replace-document UI (mobile has it, web does not) | S | `done` | — | #228 |
| T-0613 | Admin web: document-requirements CRUD and the deletion-request queue — new feature lib, two screens, three permissions, five locales | M | `done` | — | #234 |
| T-0614 | Documents checklist: iOS shows retry on load failure, Android renders the list | S | `done` | — | #228 |
| T-0615 | Admin: the registration-number label comes from the country config, not a hardcoded IČO — backend route, regenerated client, and neutral fallbacks in five locales | M | `done` | — | #233 |
| T-0616 | Backend: missingFields emits camelCase keys, every locale defines snake_case | S | `done` | — | #228 |
| T-0617 | Customer iOS: swipe-to-return on order detail | M | `done` | — | 7ad5bdfc |
| T-0618 | Cancelled orders show the real map again — the status guard is out of all four mobile apps | M | `done` | — | #230 |
| T-0619 | Customer apps: the disabled cancel button mutes to a neutral instead of fading a red fill under a white label | S | `done` | — | #232 |
| T-0620 | Android core: the consent checkbox glyph sits at the form's leading edge | S | `done` | — | #232 |
| T-0621 | Partner iOS: new-order address placeholder centring | S | `done` | — | c4acb926 |
| T-0622 | Partner iOS: the profile photo reaches the onboarding Personal step | S | `done` | — | #231 |
| T-0623 | Partner Android: unwanted whitespace at the top | S | `done` | — | 5c70922f |
| T-0624 | Customer apps: the Logout / Delete-account rows follow the partner app | M | `done` | — | #231 |
| T-0625 | Partner iOS onboarding: the two slides use different mascots, matching Android | S | `done` | — | #232 |
| T-0626 | Sign out confirms everywhere it should — admin unauthorized page, and both partner splash escapes | S | `done` | — | #233 |
| T-0627 | Customer web: the serviceability banner uses the server's CityNameMatch rule, not a string compare | S | `done` | — | #233 |
| T-0628 | Customer mobile: serviceability warned at address selection — booking review pane (#233) and the saved-address lists (#235) | M | `done` | — | #235 |
| T-0629 | i18n: a label outgrowing a single-line control truncates — ported to iOS and web, written into conventions.md | M | `done` | — | #232 |
| T-0630 | Admin console gated behind an Azure invitation (`admin_console`) — **MS-9 before the next admin deploy** | M | `done` | — | #233 |
| T-0631 | Partner Android: camera capture for job photos and the avatar, CAMERA restored with its uses-feature | M | `done` | — | #231 |
| T-0632 | `SendNewJobsDigest` fires hourly as scheduled — App Insights `sum(itemCount)` reads 30/20/20/30 across 2026-08-24..27, i.e. ~25 a day. The Invocations blade showed 8 because DEV samples telemetry at 10% and that blade reports raw sampled rows without correcting for it | S | `done` | — | — |
| T-0633 | Bicep audit — three axes read 2026-08-28 against a non-mutating `what-if` (run 33198443746). **Drift**: 23 reported, 21 are what-if artefacts (ARM's site GET omits `siteConfig`, plus service-defaulted Postgres / SWA / blob-container properties) — 2 are real and are filed as T-0655 and T-0656. **Secrets/identity**: RBAC vault, no value in source, managed-identity reads, Secrets *User* for hosts and Officer only for CI — no finding. **Cost**: B2 plan, SWA Free ×3, Postgres B1ms Burstable, Standard_LRS, ACR Basic, private networking off, telemetry sampled to 10% under a 500 MB breaker — no finding. Method limit worth carrying: `what-if` returns NestedDeploymentShortCircuited on the roleAssignment nested deployment, so a clean drift report never covers RBAC | M | `done` | — | — |
| T-0634 | Log Analytics is required by workspace-based App Insights — closed, not a defect | S | `done` | — | d5b020bf |
| T-0635 | Azure cost: the dev cut was designed, measured and committed on 2026-08-11 — owner closed it without resolving further | S | `done` | — | d5b020bf |
| T-0636 | Partner DEV API returned 500 from /health. **Window confirmed 2026-08-28 from App Insights: `api-cleansia-partner-weu-dev`, `GET /health`, resultCode 500, 2026-08-25 18:43:57.643 UTC** — one sampled record (itemCount 10), so at least one and possibly ~10 real 500s. The same 30-day window holds ordinary **503s** (partner 08-23 21:22, admin 08-24 22:27), which confirms the platform answers 503 for a failed `AddDbContextCheck` on `/health` — so this 500 is a different path, not the unhealthy response. `/alive` is the liveness path Azure polls and stayed 200 throughout. Next step is the exception inside 18:38–18:50 on that host | S | `todo` | — | — |
| T-0637 | Warm-up probe: classifies the status and surfaces the body; prod fails fast, dev keeps its budget | S | `done` | — | #232 |
| T-0638 | Backend: `context.TraceIdentifier` joins the request log lines to the exception line, keeping the 413 | S | `done` | — | #230 |
| T-0639 | refresh-mobile-spec.sh: compares CR-normalised content, counts refusals separately, exits non-zero on one | S | `done` | — | #232 |
| T-0640 | Line endings pinned to LF repo-wide — the index was already 100% LF, so the ~5,700-file renormalize the row predicted was a no-op; this is the preventive attribute | S | `done` | — | #234 |
| T-0641 | Repo stays public — automating the flip was refused deliberately: private bills against an already-exhausted Actions quota, so it would stop CI, and the right moment depends on a billing reset nothing in the repo can observe. One owner command: `gh repo edit --visibility private` | M | `done` | — | — |
| T-0642 | docs: a getting-started page for running Cleansia locally | S | `done` | — | 7651ee69 |
| T-0643 | Root README: six stale facts corrected | S | `done` | — | #232 |
| T-0644 | iOS: enum L10n.Profile back to 342 against the 400 cap | S | `done` | — | #232 |
| T-0645 | Partner iOS: the address step checks the city and shows all four service-area verdicts | M | `done` | — | #231 |
| T-0646 | Customer apps: the status-coloured left stripe is off the orders-list card | S | `done` | — | #230 |
| T-0647 | Customer Android: pull-to-refresh on Home and Recurring bookings | S | `done` | — | #232 |
| T-0648 | iOS: a disabled field looks disabled and stays disabled — four editability escapes closed | M | `done` | — | #231 |
| T-0649 | Partner Identification: Self-employed / Legal entity slides on a spring, both platforms | M | `done` | — | #231 |
| T-0650 | CI: cancel superseded iOS runs, ceiling every CI job | S | `done` | — | 00f4f729 |
| T-0651 | Customer iOS: the profile tab reaches recurring bookings, so the Plus upsell is reachable | S | `done` | — | #230 |
| T-0652 | Partner mobile: the splash Logout signs out for real, on both platforms, and confirms first | M | `done` | — | #233 |
| T-0653 | Customer apps: a saved address in an unserved city carries a warning glyph before it is picked — 3 surfaces; the recurring wizard's list cannot, its model lacks the fields | M | `done` | — | #235 |
| T-0654 | iOS: PushTokenForwarderTests.testRefreshedTokenReRegisters is flaky — its helper waits on a REGISTER COUNT while the assertion checks token ORDER, and the startup registration races the first forward. Wait for the token, not the count | S | `todo` | — | — |

| T-0655 | Bicep and the deploy pipeline disagree on the Functions image: `functionApp.bicep` declares `cleansia-functions:latest`, while the live site is pinned to the commit sha CI pushed. A provision run silently rolls the container to whatever `:latest` happens to be — surfaced by the T-0633 what-if | M | `todo` | — | — |
| T-0656 | Portal drift on two dev sites: `api-cleansia-partner-weu-dev` and `func-cleansia-weu-dev` carry a `hidden-link:/app-insights-resource-id` tag no template declares, so a provision strips it. Cosmetic in itself — it counts as evidence that the partner API has been hand-edited in the portal, which is the host T-0636 is about | S | `todo` | — | — |

*Next id: **T-0657**.*
