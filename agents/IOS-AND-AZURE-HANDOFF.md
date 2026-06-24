# iOS Build + Azure Deployment — Session Handoff

> **Read this first.** This is a cold-start handoff for a new session (on the Mac) that will build the
> iOS apps. It consolidates every decision already made, what's done, what to build, and in what order.
> Everything referenced here is committed on branch **`feature/wave8-pre-ios-cleanup`**.
>
> **Last commit at handoff:** `8946c67c` (ADR-0018 iOS design parity).
> **Date of handoff:** 2026-06-23.

---

## 0. TL;DR — what this session does

Build the **iOS apps** (Swift/SwiftUI) for Cleansia: a **partner app** and a **customer app**, as
**parity ports** of the existing Kotlin/Compose Android apps, sharing the backend Mobile API contract.

**Start with iOS Phase 0 (the foundation) — it needs NO backend and NO spec regen.** Phase 1+ (feature
screens) need the Azure dev API live + the owner's mobile-spec regen.

The iOS code lives at **`src/cleansia_ios/`** (to be created — it does not exist yet).

---

## 1. The decisions already made (read the ADRs, don't re-litigate)

All accepted and committed under `agents/backlog/adr/`:

| ADR | Decision |
|---|---|
| **0013** | iOS architecture & port strategy. One Xcode workspace, a shared **`CleansiaCore`** SPM package, two app targets (`CleansiaPartner`, `CleansiaCustomer`). **Partner app is the LEAD** (its first screen — Dashboard — is read-only, so it proves the architecture without Mapbox/Stripe/Google). Hand-written auth (NOT generated). Generated business client via openapi-generator. |
| **0014** | **Minimum deployment target = iOS 16** (old-device reach: iPhone 8/X, 2017+). State = **`ObservableObject` + `@Published`** (NOT `@Observable`, which is iOS-17-only). Sealed `UiState`/`ActionState` enums unchanged. iOS-16 MapKit API variant (no iOS-17 `Map {...}`/`Marker`). |
| **0015** | Azure dev deployment (Bicep + GitHub Environments) — see §4. |
| **0016** | **Apple App Review compliance & quality bar.** SwiftLint + SwiftFormat STRICT, BLOCKING the iOS CI. Standing **Gate-AR** on every iOS ticket. Real obligations: Sign in with Apple (customer app, Guideline 4.8), in-app account deletion (5.1.1(v)), external Stripe allowed/no-IAP (3.1.3), `PrivacyInfo.xcprivacy`, no tracking→no ATT, purpose strings ×5 locales, APNs entitlement. NOTE: "Apple detects AI code / bricks devices" is a MYTH — the real risk is App Review *rejection*; engineer for the guideline checklist. |
| **0017** | Multi-region seam. **Tenancy = APP** (row-scoped `TenantId`, unchanged); **region = INFRA**. One shared region + DB now; region is a Bicep param (`weu` token in names); the connection-string resolver seam (`RegionConnectionStringResolver`) already shipped. iOS is unaffected (multi-tenancy is a JWT claim — no header for iOS to manage). |
| **0018** | **iOS design parity (Gate-DP).** The iOS apps **look the same as Android** (same layout/flow/branding) but are built with **native SwiftUI components**; where Android & iOS conventions genuinely conflict, **iOS wins** (the "iOS component improvements") — touching only the component, never the layout/flow/branding. |

**Supporting docs:**
- iOS plan + ticket table: `agents/backlog/status/sprint-12.md`
- iOS App Review checklist (Gate-AR + Gate-DP): `agents/backlog/ios-app-review-checklist.md`
- Living companion (evolving iOS design notes): `agents/architecture/decisions/ios-app-architecture.md`
- The Android reference apps to mirror: `src/cleansia_android/` (`:core`, `:partner-app`, `:customer-app`)

---

## 2. The design philosophy (ADR-0018 — bake this into EVERY screen)

**Same app as Android, native iOS components, iOS wins on conflict.**

Three checks (Gate-DP) on every screen:
1. **AR-DP-1** — layout, flow, and branding match the cited Android Compose screen (`src/cleansia_android/.../<Screen>.kt`).
2. **AR-DP-2** — native SwiftUI only (no Material re-implementation); standard iOS affordances (swipe-back, SF Symbols, haptics, detents, pull-to-refresh).
3. **AR-DP-3** — Android↔iOS conflicts resolved iOS-native, noted in the ticket, **component-only** (never moving the layout/flow).

**Canonical Android → iOS component mappings:**

| Android (Compose) | iOS (SwiftUI native) | Keep the same |
|---|---|---|
| bottom `NavigationBar` | `TabView` | same tabs, same order |
| `ModalBottomSheet` / booking AnchoredDraggable sheet | `.sheet` + `.presentationDetents` | same 3 steps, content, snap intent |
| Material `DatePicker`/`TimePicker` | native `DatePicker` | same field, label, placement |
| Material `TextField` | `TextField`/`SecureField` | same labels + error strings (×5 locales) |
| system-back | swipe-back + `NavigationStack` nav-bar back | same back-stack |
| Coil `AsyncImage` | `AsyncImage` (or Kingfisher) | same frame/aspect/placeholder |
| Material `Snackbar` | toast on the `SnackbarController` bus | same message bus |
| Material `AlertDialog` | `.alert`/`.confirmationDialog` | same actions |

---

## 3. The iOS build plan — phases & tickets (from sprint-12.md)

> **Quality gates on EVERY iOS ticket:** SwiftLint + SwiftFormat strict (blocking) · Gate-AR (App Review,
> ADR-0016) · Gate-DP (design parity, ADR-0018, on screen tickets). Reviewer-per-developer. Security gate
> on T-0300 (auth spine), T-0313/T-0314 (booking+payment + GDPR-delete).

### PHASE 0 — foundation (START HERE; needs NO backend, NO regen)

| Ticket | What | Size |
|---|---|---|
| **T-0296** | Xcode workspace + `CleansiaCore` SPM package skeleton + 2 app targets (`CleansiaPartner`/`CleansiaCustomer`), bundle ids, signing placeholders. **iOS-16 target** + `Package.swift platforms: [.iOS(.v16)]`. **FIRST/ALONE.** | M |
| **T-0297** | Design tokens (colors/spacing/shape/type) + `Cleansia*` SwiftUI component parity (Button/TextField/Dropdown/Dialog/Checkbox/CodeInput) in `CleansiaCore`. VM pattern = `ObservableObject`/`@Published`; sealed `UiState`/`ActionState` enums. | M |
| **T-0298** | DI composition root (`AppContainer` per app, initializer injection; the lazy no-auth refresh-session boundary). | S |
| **T-0299** | Global snackbar bus + error center (`SnackbarController` parity + `ApiError→String` localizer). | S |
| **T-0300** | **The auth/session/header middleware (hand-written, LOAD-BEARING).** Keychain `TokenStore`, hand-written `AuthClient` + no-auth refresh session, `actor SessionRefresher` single-flight 401-refresh, `DeviceIdProvider` (one source), `HeaderAdapter` (X-Device-Id / X-Device-Label / X-Time-Zone + the no-Bearer-on-anon allow-list), `SessionManager`/ForcedSignOut + session-scoped cache registry. **L → split. Security gate.** | L |
| **T-0301** | Header-parity spec document — the invisible out-of-band contract (X-Device-Id == `/Device/Register` id invariant, the anon allow-list incl. customer host, X-Time-Zone, replace-refresh-on-refresh, empty-token gate). | S |
| **T-0302** | Swift codegen toolchain — openapi-generator **swift5 + urlsession**, wired into the build, reading the shared mobile spec. Wiring is runnable now; **first real generation is BLOCKED on the owner mobile-spec regen.** | M |

**Phase 0 dependency order:** T-0296 first/alone → then T-0297, T-0298, T-0299, T-0301, T-0302 fan out;
T-0298 → T-0300 (the auth spine).

### PHASE 1 — partner lead vertical (HELD on the mobile-spec regen)

| Ticket | What | Size |
|---|---|---|
| **T-0303** | Partner login (hand-written auth, empty-token gate) → **read-only Dashboard** (generated partner client + `UiState`) — proves auth/session/headers/codegen/state end-to-end. Needs T-0300 + T-0302. | M |

### PHASE 2+ — parity feature waves (after Phase 1 proves the architecture)

Partner: **T-0304** (shell+RegistrationLock) → **T-0305** (auth-rest), **T-0307** (order work-loop, L→split,
**hard area #3**) → **T-0308** (photos), **T-0309** (earnings/invoices/PeriodPay), **T-0310** (profile/devices),
**T-0306** (map seam + MapKit, **hard area #2**).
Cross-app: **T-0311** (APNs push — needs owner APNs auth key).
Customer: **T-0312** (shell+auth+Google Sign-In) → **T-0313** (booking wizard + Stripe, L→split, **hard area #1, hardest**) → **T-0314** (customer tail: orders/rewards/membership/recurring/disputes/addresses/profile incl. **DeleteAccount/GDPR**, L→split).

**The 3 effort-dominating areas:** (#1) customer booking wizard + Stripe (T-0313), (#2) maps across both
apps (T-0306, reused by 0307/0310/0314), (#3) partner order work-loop + photos + codegen (T-0302/0307/0308).

---

## 4. Azure dev environment — THE FULL PICTURE

> You asked for "the full instruction." There are **two layers**: (A) the **decisions** (ADR-0015 +
> ADR-0017) and (B) the **owner steps** (the runbook). Both are below.

### 4A. What was decided & built (ADR-0015 / ADR-0017 — already committed)

The Azure infrastructure is **authored as Bicep** (`deploy/bicep/`) and the CI is rewired — but **not yet
provisioned** (the resources are torn down; this is a clean-slate re-provision the OWNER runs).

**Topology** (one subscription, RG `rg-cleansia-weu-dev`, West Europe):
- App Service Plan **B2 Linux** (`plan-cleansia-weu-dev`)
- **5 API App Services** (`api-cleansia-{partner,admin,customer,partner-mobile,customer-mobile}-weu-dev`)
  — note the **customer-mobile** host: the old pipeline omitted it; the **iOS customer app needs it**.
- Customer **SSR** App Service (`web-cleansia-customer-weu-dev`, Node 20)
- 2 **Static Web Apps** (`swa-cleansia-{partner,admin}-weu-dev`)
- **Functions** container via ACR (`func-cleansia-weu-dev` + `acrcleansiaweudev`)
- **PostgreSQL Flexible** B1ms (`pg-cleansia-weu-dev`)
- **Storage** (`stcleansiaweudev` — blob + queues + Functions store)
- **Key Vault** RBAC (`kv-cleansia-weu-dev` — values owner-populated)
- **App Insights** + **Log Analytics** (`appi-`/`log-cleansia-weu-dev`)

**Key design points:**
- **No secrets in code** — every secret is a Key Vault reference resolved by each host's managed identity;
  the Postgres password is a `@secure()` param the CI passes from a GitHub Environment secret.
- **OIDC federation** — CI logs into Azure with no stored password (federated to the `dev-weu` Environment).
- **EF migrations** applied by CI via the migration bundle, before app deploys.
- **GitHub Environments** — `dev-weu` (auto-deploy on merge) + `prod-weu` (protected, manual approval).
- **Region seam (ADR-0017)** — `weu` token in every name + a `region` param, so a 2nd region is a param
  value, not a rewrite. Prod Bicep is authored (`weu.prod.bicepparam`) but **NOT deployed**.
- The iOS apps point at the two `api-cleansia-{partner,customer}-mobile-weu-dev.azurewebsites.net` hosts.

### 4B. The owner steps to make it live — THE RUNBOOK

**The complete step-by-step is at [`deploy/AZURE-DEV-RUNBOOK.md`](../deploy/AZURE-DEV-RUNBOOK.md).** Summary
of the 10 stages (all OWNER-only — agents never run `az`, create Environments, or set secret values):

1. **Prereqs** — Azure subscription (Owner), `az` CLI + bicep, GitHub admin, your public IP, a Postgres password.
2. **OIDC federation** — create the app registration, federate it to the `dev-weu` Environment, grant
   it **Contributor + User Access Administrator** (the second is needed so the deployment can create the
   managed-identity role assignments).
3. **Resource group** — `az group create rg-cleansia-weu-dev --location westeurope`.
4. **GitHub Environments + secrets** — create `dev-weu` (open) + `prod-weu` (protected); add the secrets
   (the 3 OIDC ids, `POSTGRES_ADMIN_PASSWORD`, `ADMIN_IP_ADDRESS`, `ACR_NAME`, the 2 SWA tokens).
5. **First provision** — `az deployment group create` (with a `what-if` preview first). Creates all 11
   resources; the Key Vault is created **empty**.
6. **Key Vault values** — grant yourself Secrets Officer, then `az keyvault secret set` each value
   (DB conn, `Jwt--Key`, **Stripe TEST keys**, SendGrid, Sentry, Storage conn, **Mapbox** — rotate the
   exposed token first). Restart the API hosts.
7. **Migrations** — applied automatically by CI on the first deploy (the EF bundle); manual command provided.
8. **Functions container + first deploy** — fill the 2 SWA deploy tokens, then merge to `master` (or run
   the workflow) to build the Functions image + deploy all 5 APIs + SSR + 2 SPAs.
9. **Smoke** — confirm all 5 APIs healthy, **both mobile hosts issue a token**, SSR renders, SPAs load,
   the Functions PDF pipeline works.
10. **Green → the iOS apps point at the stable dev API.** That's the whole reason for this wave.

**Why this matters for iOS:** the iOS apps need a stable backend to talk to (you said your Mac can't run
the full local stack). Once dev is live, the iOS apps use the dev URLs as their base URL. **iOS Phase 0
does NOT need this** — only Phase 1+ (the feature screens) do.

---

## 5. What's done vs what's owner-gated

### Done & committed (nothing blocked on the builder)
- All 6 ADRs (0013–0018) accepted.
- The full Azure Bicep IaC + rewired CI + the region resolver (commit `38a10375`).
- The Azure runbook (`b85441a8`).
- The iOS plan, App Review checklist, design-parity gate.

### OWNER manual steps (in priority order)
1. **Provision the Azure dev environment** (follow `deploy/AZURE-DEV-RUNBOOK.md`) → unblocks iOS Phase 1+.
2. **Rotate the exposed Mapbox token** before putting it in Key Vault.
3. **Mobile-spec regen** — regenerate the committed mobile OpenAPI specs
   (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`). They are stale (pre-T-0272). This
   unblocks the iOS codegen (T-0302 first gen) + every iOS feature screen (T-0303+).
4. **APNs auth key** (Apple Developer) — needed for T-0311 (push).
5. **Apple Developer signing** — provisioning profiles/certs for the two app targets (needed to run on a
   device / submit to TestFlight).
6. Unrelated carry-overs: admin client regen (unblocks T-0295 employee-page audit drill-in), IMP-3 admin
   regen (unblocks T-0279 pay-config client swap).

---

## 6. How to start (recommended order for the Mac session)

1. **Read** the ADRs 0013/0014/0016/0018 + `sprint-12.md` + `ios-app-review-checklist.md`, and skim the
   Android reference apps under `src/cleansia_android/` (especially `:core` and the partner app's nav graph
   + Dashboard — that's the lead vertical).
2. **Build Phase 0** (T-0296 → T-0297/0298/0299/0301 → T-0300 → T-0302 wiring). This is all runnable now,
   needs no backend, no regen. Use the agent team (the `ios` charter) with reviewer + Gate-AR + Gate-DP +
   SwiftLint/SwiftFormat on every ticket. T-0300 (the auth spine) and T-0302 (codegen) are the load-bearing
   ones — get them right; they're what every feature screen rides on.
3. **In parallel, the owner provisions Azure** (the runbook) and **does the mobile-spec regen**.
4. **When the regen + dev API are ready**, run **Phase 1** (T-0303 partner login → Dashboard) against the
   dev API with the freshly-generated client. That's the proof the architecture works end-to-end.
5. **Then Phase 2+** by complexity — partner waves first (the order work-loop, T-0307, is the meaty one),
   then customer (booking + Stripe, T-0313, is the hardest). Each screen ticket satisfies Gate-DP (looks
   like its Android counterpart, native components).

**Conventions to honor** (same as the rest of the repo): no ticket IDs in source comments; never run an
owner-only step (mobile-spec regen, EF migration, the `az deployment`); commit per batch on the feature
branch + push, no PR; the team operating system is in `agents/` (charters in `.claude/agents/`, process in
`agents/process/`, the catalog in `agents/knowledge/`, especially `patterns-mobile.md`).

---

## 7. One-paragraph status (for the very top of the new session)

> Cleansia is a cleaning-services platform (.NET 10 backend, Angular web, Kotlin/Compose Android) going to
> iOS. All planning is done and committed (ADRs 0013–0018 on branch `feature/wave8-pre-ios-cleanup`). The
> Azure dev infra is authored as Bicep (`deploy/bicep/`, not yet provisioned — the owner runs
> `deploy/AZURE-DEV-RUNBOOK.md`). The iOS apps are Swift/SwiftUI parity ports of the Android apps
> (`src/cleansia_android/`), partner-app-first, iOS-16 target, `ObservableObject` state, native components
> that look like Android (ADR-0018), MapKit-default, hand-written auth, openapi-generated business client.
> **Start by building iOS Phase 0** (foundation — `src/cleansia_ios/`, no backend needed) per
> `agents/backlog/status/sprint-12.md`, holding Phase 1 until the owner provisions Azure dev + regenerates
> the mobile OpenAPI specs.
