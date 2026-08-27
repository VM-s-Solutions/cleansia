# Backlog — INDEX

**One row per ticket. One status. This table is the only place a status lives.** See
[`README.md`](README.md) for why that sentence is the whole design.

**Status:** `todo` · `in_progress` · `blocked` · `done` · `dropped`
**Size:** S (< half a day) · M · L

> Filed 2026-08-25 from a transcript sweep of the whole session, then ground-truthed against
> master one item at a time. 17 further items were checked and found **already shipped** — they
> are not listed, because a done ticket in a queue is how four lanes got dispatched onto
> already-finished work on 2026-08-11. Everything below was verified still open.

> `blocked` rows name a question in [`questions/open.md`](questions/open.md). A blocked row is
> waiting on the owner, not on an agent.

| ID | Title | Size | Status | Owner | PR |
|---|---|---|---|---|---|
| T-0607 | Onboarding: tappable step dots let a cleaner go back (iOS + Android) | M | `done` | — | #227 |
| T-0608 | Onboarding: the 5 iOS/Android stepper divergences reconciled | M | `done` | — | #227 |
| T-0609 | Partner login: the brand hold no longer replays after sign-in (iOS) | M | `done` | — | #227 |
| T-0610 | Partner web: dead availability module removed, with a backend null-guard | S | `done` | — | #227 |
| T-0611 | Partner web: registration-number label from country config, drop hardcoded IČO | S | `todo` | — | — |
| T-0612 | Partner web: replace-document UI (mobile has it, web does not) | S | `todo` | — | — |
| T-0613 | Admin web: UI for document requirements CRUD and the deletion queue | M | `todo` | — | — |
| T-0614 | Documents checklist: iOS shows retry on load failure, Android renders the list | S | `todo` | — | — |
| T-0615 | Admin web: registration-number label hardcodes IČO in three places — **needs admin NSwag regen** | S | `blocked` | — | — |
| T-0616 | Backend: missingFields emits camelCase keys, every locale defines snake_case | S | `todo` | — | — |
| T-0617 | Customer iOS: swipe-to-return still does not work on order detail | M | `todo` | — | — |
| T-0618 | Customer apps: cancelled-order location renders wrong | S | `todo` | — | — |
| T-0619 | Customer apps: cancel-order button unreadable when disabled | S | `todo` | — | — |
| T-0620 | Android core: terms-and-agreement checkbox line is not aligned | S | `todo` | — | — |
| T-0621 | Partner iOS: new-order address placeholder is not centred | S | `todo` | — | — |
| T-0622 | Android apps: photo upload is disabled and should be enabled | S | `todo` | — | — |
| T-0623 | Partner Android: unwanted whitespace at the top — **Q-AND-01** | S | `blocked` | — | — |
| T-0624 | Logout button text is not horizontally centred — **Q-UI-01** | S | `blocked` | — | — |
| T-0625 | Partner onboarding mascots: use different ones (iOS) | S | `todo` | — | — |
| T-0626 | Sign out: confirm before logging out, everywhere it is still missing | S | `todo` | — | — |
| T-0627 | Customer web: serviceability banner fires on a serviced city | M | `todo` | — | — |
| T-0628 | Customer mobile: warn about serviceability at address selection, not after | M | `todo` | — | — |
| T-0629 | All apps: a translation longer than its control must not be truncated — **Q-I18N-01** | L | `blocked` | — | — |
| T-0630 | Every frontend gated by App Registration with additional roles — **Q-SEC-01** | L | `blocked` | — | — |
| T-0631 | Standing rule: iOS and Android ship in step — audit the gaps | S | `todo` | — | — |
| T-0632 | SendNewJobsDigestTimerFunction never fires — **Q-INFRA-01** | M | `blocked` | — | — |
| T-0633 | Bicep: full A-to-Z audit — **Q-INFRA-02** | M | `blocked` | — | — |
| T-0634 | Log Analytics keeps reappearing — it is declared in Bicep and App Insights needs it — **Q-COST-01** | S | `blocked` | — | — |
| T-0635 | Cut Azure cost without losing App Insights — measure first — **Q-COST-01** | S | `blocked` | — | — |
| T-0636 | Partner DEV API returned 500 from /health for 4 minutes after deploy | S | `todo` | — | — |
| T-0637 | Warm-up gate cannot tell 5xx from cold start and discards the body | S | `todo` | — | — |
| T-0638 | RequestLoggingMiddleware sits outside the exception handler on all 5 hosts — **Q-BE-01** | S | `blocked` | — | — |
| T-0639 | refresh-mobile-spec.sh throws on the owner's machine | S | `todo` | — | — |
| T-0640 | Many .cs files show as modified with no content change | S | `todo` | — | — |
| T-0641 | Move the repo to private without losing Actions minutes — **Q-REPO-01** | M | `blocked` | — | — |
| T-0642 | docs: a getting-started page for running Cleansia locally — **Q-DOCS-01** | S | `blocked` | — | — |
| T-0643 | Root README rewrite | S | `todo` | — | — |
| T-0644 | iOS: enum L10n.Profile is at 393 against a 400 type_body_length cap | S | `todo` | — | — |

*Next id: **T-0645**.*
