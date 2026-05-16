# INFRA-001 + ARCH-001 — Decision Doc

Both items in `post-android-followups.md` are tagged `[TODO — DECIDE]`. Below is the tradeoff for each and a recommendation. Implementation is a separate engagement once you sign off.

---

## INFRA-001 — Bicep IaC for Azure

### Status
Pending decision. Spec recommends "yes, once you deploy to Azure for real." 3–5 days of work for a clean first cut.

### What's true today
- Staging URLs: `api-cleansia-partner-dev.azurewebsites.net`. Implies an existing Azure App Service deploy exists already (hand-clicked or scripted somewhere outside the repo).
- The repo has zero `.bicep` files. Anything in Azure right now exists by hand.
- Secrets (FCM service-account JSON, SendGrid API key, Stripe webhook secret, Mapbox token, Sentry DSN, Google client IDs) currently live in user-secrets locally and presumably in Azure App Configuration or App Service settings in prod — also hand-managed.
- 4 web hosts (Partner/Admin/Mobile/Customer APIs) + Functions + PostgreSQL + Storage queues + Blob containers — that's at least 7 resource categories needing reproducible templates.

### Tradeoff

| Path | Pros | Cons |
|---|---|---|
| **Add Bicep now** | Reproducible envs; dev/staging/prod become identical (modulo param files); audit trail of every infra change in git; new team members can spin up a clean env from scratch | 3–5 days of focused work; need to reverse-engineer current Azure config to template-match it; ongoing maintenance burden whenever a service is added |
| **Defer until breakage** | Zero short-term effort | First time you need to recreate an env (Azure outage / new region / on-the-fly staging clone) you'll be reconstructing it from screenshots. Secrets drift. Hand-clicked infra rots. |
| **ARM exports as middle ground** | Cheap: Azure portal exports current state to ARM templates with one click. Get reproducibility cheaply | ARM templates are verbose, harder to maintain than Bicep, and the exported version captures the snapshot only — you still need to keep them in sync manually |

### Recommendation: **Defer until you hit the second environment**

Concrete trigger: the first time you need to provision a real staging or prod env from scratch (not just iterate on the existing one), do it via Bicep instead of by clicking. Don't pre-write Bicep for envs you don't have. The 3–5 day cost amortizes over the second environment you spin up — before that it's optimization debt.

If you DO want it now anyway: scope is `deploy/bicep/` with `main.bicep` + `params.dev.bicepparam` + `params.prod.bicepparam`. Resources to template:
- 4 App Service apps + 1 Functions app on a shared Linux App Service Plan
- PostgreSQL Flexible Server (single dev, replicated prod)
- Storage Account (queue + 3 blob containers: receipts, invoices, photos)
- Key Vault (rotate secrets out of appsettings)
- App Insights (already wired) + Sentry (third-party, no IaC needed)

### Decision needed from you
- [ ] Proceed now → spec out the Bicep work as its own session.
- [ ] Defer with trigger above → leave INFRA-001 in `[TODO — DECIDE]` until then.

---

## ARCH-001 — Monorepo for the two Android apps

### Status
**SHIPPED 2026-05-15** — see `planning/done/arch-001-android-monorepo.md` for the
full execution log. Both apps now live under `src/cleansia_android/` as
`:partner-app` and `:customer-app` Gradle subprojects with a shared `:core`
library. Customer-side files (auth, network, snackbar, UI primitives, theme
tokens, formatters, Sentry tracker) all migrated. Partner adopts `:core` as a
build dependency but keeps its bespoke `TokenManager` + `AuthInterceptor` and
its own UI primitives (`CleansiaButton(style=…)`, etc.) — tracked as
**Phase 3b/4b** in the execution doc, can land when convenient.

### What's true today
- Two separate Android Studio projects: `src/cleansia_android/` (partner) + `src/cleansia_customer_android/` (customer).
- Both ship to the Play Store under different package IDs (`cz.cleansia.partner` + `cz.cleansia.customer`).
- High duplication: Theme tokens, `CleansiaButton`/`CleansiaTextField`/`CleansiaTextLink` etc., Retrofit/OkHttp setup, `TokenStore` + refresh-token plumbing, `SnackbarController`, FCM dispatch, Sentry init, Mapbox helpers.
- Low duplication: the feature trees themselves (booking flow on customer side has no parallel on partner; partner's order-management has no parallel on customer side).

### Tradeoff

| Path | Pros | Cons |
|---|---|---|
| **Status quo (two repos)** | Zero refactor cost; each app evolves independently; no risk of accidentally cross-coupling features | Theme/auth/networking changes need to be made twice — and have been. Each new shared primitive lands in only one app until someone manually copies it. Drift is invisible until someone notices. |
| **Shared `:core` module (recommended in spec)** | Theme + components + network plumbing land once, both apps consume the same artifact; Gradle's :core dependency makes drift impossible. Each app stays a separate Android Studio project; minimal feature-coupling risk | One-time refactor: move ~30 files (CleansiaTheme + Cleansia* components + Retrofit/OkHttp/Token plumbing) into a new Gradle module. Need to introduce a parent `settings.gradle.kts` that includes both apps + the `:core` module. Build times grow slightly. |
| **Full multi-flavor monorepo** | Strongest deduplication; one APK build pipeline with `partner`/`customer` flavors | Highest refactor cost (every file moves); risk of accidental cross-feature coupling; flavors share `BuildConfig` namespace which can conflict; each Play Store deploy now hits the same flavor pipeline so a broken build blocks both apps |

### Recommendation: **Shared `:core` module — but later**

The win is real (theme drift is a real cost we're paying every time both apps need a new component) but you have higher-leverage work in flight (booking-extras, frontend cleanup waves). Shared-`:core` is a 2–3 day pure-refactor session — best executed when (a) both apps are stable AND (b) no in-flight feature touches both apps' shared layers.

Concrete trigger: next time you need to add the same primitive to both apps. That's the moment to extract instead of copy.

### Scope when triggered
- New Gradle parent: `src/cleansia_android/settings.gradle.kts` includes `:core`, `:customer-app`, `:partner-app` (or the two existing apps as included builds).
- New `:core` module containing:
  - `ui/theme/` — Color tokens, Typography, CleansiaTheme + variants, gradients, Shape tokens.
  - `ui/components/` — Cleansia* primitives (Button, TextField, TextLink, OutlinedButton, Snackbar host, etc.).
  - `core/network/` — Retrofit + OkHttp setup, Auth + NoAuth qualifiers, JSON config, NetworkErrorInterceptor.
  - `core/auth/` — TokenStore, AuthAuthenticator, AuthInterceptor (NOT AuthApi or AuthRepository — those are app-specific because partner/customer have different endpoints).
  - `core/snackbar/` — SnackbarController.
  - `core/sentry/`, `core/datetime/` etc.
- Each app retains: feature trees (`features/`), AuthApi/AuthRepository (each has its own endpoint set), navigation, AppSettings, app-specific theme overrides.
- Hilt: `:core` declares a `CoreModule`; each app's `AppModule` declares the bindings :core needs but doesn't provide itself (e.g. `BuildConfig.API_BASE_URL`).

### Decision needed from you
- [ ] Trigger-based extraction (recommended): wait for next shared-primitive add.
- [ ] Schedule a 2–3 day refactor session now → leaves the spec, fine.
- [ ] Leave the two repos drifting (do nothing) → fine if drift cost is below your pain threshold.

---

## Combined recommendation

Both items: **defer with concrete triggers** documented above. Neither is a blocker; both are quality-of-life infra investments that earn back their cost over multiple environments / multiple shared-primitive adds. The trigger language above means they won't get forgotten — the next time the relevant pain hits, the decision is "use the spec we wrote" instead of "should we do this?".
