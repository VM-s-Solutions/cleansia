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
| T-0633 | Bicep audit — three axes read 2026-08-28 against a non-mutating `what-if` (run 33198443746). **Drift**: 23 reported, 21 are what-if artefacts (ARM's site GET omits `siteConfig`, plus service-defaulted Postgres / SWA / blob-container properties) — 2 are real and are filed as T-0655 and T-0656. **Secrets/identity**: RBAC vault, no value in source, managed-identity reads, Secrets *User* for hosts and Officer only for CI — no finding. **Cost**: B2 plan, SWA Free ×3, Postgres B1ms Burstable, Standard_LRS, ACR Basic, private networking off, telemetry sampled to 10% under a 500 MB breaker — no finding. Method limit worth carrying: `what-if` returns NestedDeploymentShortCircuited on the roleAssignment nested deployment, so a clean drift report never covers RBAC. One dismissed entry was spot-checked in the portal 2026-08-28 — health check is **Enabled** with `/alive` on the API sites, confirming the `+ healthCheckPath` line was a what-if artefact and not drift | M | `done` | — | — |
| T-0634 | Log Analytics is required by workspace-based App Insights — closed, not a defect | S | `done` | — | d5b020bf |
| T-0635 | Azure cost: the dev cut was designed, measured and committed on 2026-08-11 — owner closed it without resolving further | S | `done` | — | d5b020bf |
| T-0636 | Partner DEV API 500 from /health — **root cause found, closed 2026-08-28 with no code change**. `System.BadImageFormatException`: *"…The format of the file '/home/site/wwwroot/Cleansia.Infra.Database.dll' is invalid."*, from `Microsoft.Extensions.DependencyInjection` → `ResolveService`, 2026-08-25 18:43:57 UTC. A deploy race, not a defect: `azure/webapps-deploy@v3` extracts the zip file-by-file over the live `wwwroot` and dev has no staging slot (a B-series plan cannot host one), so the restart raced extraction and DI loaded a half-written assembly — clearing when extraction finished, which is the four minutes the report measured. **Deliberately not fixed.** Prod is immune by construction (slot → warm → prove → swap). Dev is already mitigated: `warm-dev-sites` retries 30×10s and only warns, and its comment already names this ticket. `WEBSITE_RUN_FROM_PACKAGE=1` does not apply — these are `kind: 'app,linux'` hosts and the value `1` is a Windows mechanism; the Linux form takes a blob URL, i.e. a SAS-rotating upload across five deploy jobs to remove a warning from a dev log | S | `done` | — | — |
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
| T-0654 | iOS: `testRefreshedTokenReRegisters` waits for the TOKENS it asserts on, not a register count — the session-start token-less register races the first forwarded token, so "two have arrived" never meant "these two have arrived". New `waitForTokens` helper; the assertion drops to a set plus a call count, which is what the test is about | S | `done` | — | #236 |

| T-0655 | `az acr build` pushes `:latest` alongside the commit sha, so the tag `functionApp.bicep` declares actually exists — a provision was pointing the Functions host at an image nothing had ever pushed. The param description is corrected in the same commit: it claimed "CI overrides per deploy with the commit sha", and no such override exists | M | `done` | — | #236 |
| T-0656 | Portal drift, the `hidden-link:/app-insights-resource-id` tag on two dev sites — **closed 2026-08-28 without declaring it**. The tag does nothing: all six hosts report telemetry and only two carry it, so `APPLICATIONINSIGHTS_CONNECTION_STRING` is what wires it and the tag is an artefact of a manual portal enable. A provision stripping it is IaC correcting drift, which is the system working. Declaring a portal-managed hidden tag in Bicep to preserve an artefact with no function is machinery for nothing. The row's value was as evidence that the partner API gets hand-edited in the portal, and that is now recorded | S | `done` | — | — |
| T-0657 | `check-backlog-consistency` could not read the backlog it guards — the row reader required a bold id and parsed 0 of 50 rows, the anti-vacuity rule keyed on an optional `tickets/` directory so the blindness looked like an empty backlog, and the summary line printed FAILED unconditionally. Reader accepts both forms; reach check is self-referential; verdict computed. Verified clean at exit 0 and exit 1 with one row made unparseable | S | `done` | — | #236 |
| T-0658 | `check-consistency` E1 and conv narrowed to what they mean — 19 violations of which 10 were the rules being wrong. E1 fired on every `data class *UiState` (9 hits, 1 defensible) and now exempts single-signal states, two-or-more concurrent in-flight signals, and per-field form validation; conv checked only the current line for a disable directive and reported a documented, ESLint-sanctioned exception. E1 9→1, conv 2→1, both survivors genuine. Seven new self-tests, 40 passing | M | `done` | — | #236 |
| T-0659 | Partner Android: the splash screen collects its two ViewModel flows with `collectAsStateWithLifecycle` — 30 files in the module already did, the dependency was already there, these two were stragglers (check-consistency E6) | S | `done` | — | #236 |
| T-0660 | iOS style gates run locally under WSL — `src/cleansia_ios/scripts/ios-style.sh` installs and asserts CI's exact pins (SwiftFormat 0.60.1, SwiftLint 0.65.0), stages an LF snapshot on ext4 because the Windows checkout is CRLF and faked 812 `linebreaks` violations, and prefers `swiftlint-static` because the dynamic build needs a 1.5 GB Swift toolchain. 36s vs a ~16min CI round; verified clean and verified catching an injected violation | M | `done` | — | #237 |
| T-0661 | Dependabot triage of 165 open alerts — **all npm**, 157 in `Cleansia.App`, 8 in `docs`. **127 of 165 are devDependencies** that never reach a browser, including 3 of the 4 criticals (handlebars, shell-quote, websocket-driver — build tooling, supply-chain not runtime). The one critical runtime alert (`@angular/ssr`) is FIXED here by a lockfile bump to 19.2.27 | M | `done` | — | #237 |
| T-0662 | Angular framework moved to 19.2.25, clearing **18 of the 26 runtime alerts** on `@angular/*` — lockfile only, since `~19.2.0` already admitted it. `npm update` could not get there: the packages peer-pin each other exactly, so npm kept reporting the installed 19.2.14 as fixed and refused every coordinated install, and `ng update` needs an angular.json this Nx workspace does not have. Dropping the `@angular` entries from the lockfile let npm re-resolve that family alone. Verified with `nx build cleansia-partner.app` | M | `done` | — | #238 |
| T-0663 | The eight unpatched advisories assessed against **this** codebase — and three of the seven distinct ones do not apply at all. Angular i18n XSS (GHSA-jj27) needs `$localize` / `i18n=`; this app uses ngx-translate and has neither. The two-way-binding sanitization bypass (GHSA-58w9) needs a sanitized sink two-way bound; there is no `innerHTML`, no `bypassSecurityTrust`, no such binding. The formatDate DoS (GHSA-48r7) needs Angular's `formatDate` on hostile input; the app's own `formatDate` is `toLocaleDateString` over server-supplied dates. **Four do apply**, all through SSR + hydration — and two of those are mitigated here by turning the HTTP transfer cache off. The remaining two need Angular 20 (T-0667) | M | `done` | — | #238 |
| T-0664 | The select option's `value: any` is recorded as deliberate rather than refactored — polymorphic by nature across 25+ call sites, PrimeNG types SelectItem the same way, and generics would be a design-system change rather than a lint cleanup | S | `done` | — | #237 |
| T-0665 | Four commands stop returning a bool that was always `true`, and `HandlePaymentNotification` stops returning a Stripe id nothing read — all five are bare `ICommand`, the form B1's own comment sanctions. No test read `.Value`; the one that did asserted `true` was `true`. 5 commands, 8 controllers, 5 test files. Solution builds, 4077 unit tests pass, check-consistency 19 → 2. **Clients not regenerated — MS-11** | M | `done` | — | #238 |
| T-0666 | `.github/dependabot.yml` batches security fixes — version updates off via `open-pull-requests-limit: 0`, security updates grouped three ways, turning 165 alerts into roughly four PRs instead of 165 × five CI workflows. The owner enabled `automated-security-fixes` after the config reached master, which is the order that matters: grouping only applies once the file is there | S | `done` | — | #238 |
| T-0667 | Angular **20.3.30** — the only fix for the advisories 19.x will never get, all of which read *affected ≤ 19.2.25, patched none*. Peer chain moved as one set (CLI 20.3.35, CDK 20.2.14, PrimeNG 20.4.0, NgRx 20.1.0, TypeScript 5.9.3, every `@nx/*` aligned to 21.6.11); `jest-preset-angular` deliberately held at 14.6 because its peer already admits Angular <21, which kept a Jest 30 migration out of scope. 17 breakages in four mechanical shapes — PrimeNG module renames, a nullable `DialogService.open()`, `[severity]` narrowed to a union, and `p-tabs` `valueChange` emitting `undefined`. `withNoHttpTransferCache()` removed: the advisories it worked around are fixed here. Typecheck clean, 68 test projects pass, three builds, SSR renders | L | `done` | — | #239 |
| T-0668 | Nx **23.1.2** and Jest **30**, landed by repairing what #246 left behind rather than by a clean migration — that PR was merged with red CI and master could not `npm ci` at all: it moved `nx` and eight `@nx/*` to 23.1.2 while leaving `@nx/devkit` on 21.6.11, whose peer is `nx ">= 20 <= 22"`. Four families were inconsistent, not one (nx, swc, the jest 30 set, and `angular-eslint` still on ^19 — missed by the T-0667 pass because eslint is not in the build path). Seven lib `test-setup.ts` files moved to `setupZoneTestEnv`, which jest-preset-angular 17 requires. Two overrides for optional peers nothing invokes. Dependabot gains an `nx` group so the family cannot split again | L | `done` | — | #250 |

*Next id: **T-0669**.*
