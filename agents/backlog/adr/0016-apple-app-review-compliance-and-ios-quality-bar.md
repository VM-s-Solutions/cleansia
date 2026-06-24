# ADR-0016 — Apple App Review compliance & the iOS quality bar: STRICT SwiftLint/SwiftFormat as a BLOCKING CI gate, a verifiable App-Store-Review-Guidelines checklist (privacy manifest, purpose strings, in-app account deletion, Sign-in-with-Apple, external-payment confirmation), a standing per-ticket compliance gate, and a pre-submission audit artifact — engineered against the REAL guidelines, not a phantom "AI-code detector"

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-23
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** ios | cross-cutting (process: reviewer + ios charters; a standing gate on every iOS ticket)
- **Extends:** **ADR-0013** (iOS architecture & port strategy — iOS 16 floor via ADR-0014, SwiftUI,
  `ObservableObject`, MapKit, `stripe-ios` PaymentSheet, APNs, hand-written auth, 5 locales) and **ADR-0014**
  (the iOS-16 floor). This ADR adds the **quality/compliance bar** those apps must clear to pass Apple App
  Review; it does not re-open any ADR-0013/0014 architecture decision.
- **Ticket:** IOS-COMPLIANCE-ADR (this ADR) · **Consumers:** new tickets appended to the Wave-10 iOS plan
  (`status/sprint-12.md`) continuing the T-0296… numbering, plus a **standing Gate** the reviewer/ios charters
  run on **every** iOS ticket. Living companion `architecture/decisions/ios-app-architecture.md` (compliance
  section) + the pre-submission audit artifact `agents/backlog/ios-app-review-checklist.md`.

> This ADR freezes the **iOS quality + Apple-App-Review-compliance bar**: the blocking lint/format CI gate, the
> concrete and *verifiable* App Store Review Guidelines checklist the apps must satisfy, how each checklist
> item maps to a ticket or a standing per-ticket gate, and the pre-submission audit artifact the team runs
> before the first TestFlight/App Store submission. It ships **no Swift code** — every concrete artifact is a
> consumer ticket. Once `accepted` it is immutable — supersede, never edit.

> **The framing this ADR exists to correct (stated plainly so the myth does not persist):**
> There is **NO "AI-written-code detector"** in Apple App Review, and **App Review cannot brick, disable, or
> damage hardware.** Both beliefs are **FALSE.** App Review is a **guideline-conformance review** of the
> *submitted app + its metadata* against the **published App Store Review Guidelines**. The **real** risk is
> **rejection** against those (knowable, enumerable) guidelines on submission, plus **account-level
> consequences for repeat/deliberate abuse** (e.g. concealing functionality, spam resubmission). So this ADR
> engineers for the **actual checklist** — clean, standards-compliant, polished, crash-free code + the concrete
> submission requirements — **NOT** for a phantom detector. Every item below is something a reviewer (Apple's
> *or* ours) can verify.

---

## Context

ADR-0013/0014 set the iOS apps as **parity ports** of the Android customer + partner apps (iOS 16, SwiftUI,
`ObservableObject`, MapKit, `stripe-ios` PaymentSheet, APNs, hand-written auth, 5 locales). The owner wants the
iOS apps held to a **higher bar than the rest of the platform** — they **must pass Apple App Review on
submission**. The rest of the platform's lint is *non-blocking baseline-debt* (frontend); iOS lint will
**block** (D1). The decision is *what concretely that bar is* and *how it is enforced and verified*, grounded
in the published guidelines and the real facts of the two apps.

### The real, knowable obligations (traced to the apps' actual behavior)

**The customer app has Google Sign-In → Sign in with Apple is a real obligation (Guideline 4.8).** The Android
customer app ships Google Sign-In (`customer-app/.../auth/AuthModule.kt`, `SignUpScreen.kt`, the backend
`googleauth` anon endpoint in the allow-list per ADR-0013 D4.4). Guideline **4.8 ("Login Services")** requires
that an app using a **third-party or social login service** (Google) **also offer** an equivalent
privacy-focused login option — **Sign in with Apple** is the canonical one that satisfies 4.8 for an app that
already offers Google. So the iOS customer app, by porting Google Sign-In, **incurs a real SIWA obligation.**
The **partner** app has **no** social login (hand-written email/password only) and therefore **no** 4.8
obligation. This is flagged as a genuine decision/obligation, not a nicety (D2/D3).

**The customer app has an in-app GDPR/account-deletion flow → it must be reachable in-app (Guideline
5.1.1(v)).** The platform has a `GdprDeletionService` (backend) and the customer app exposes a
DeleteAccount/GDPR flow (sprint-12 T-0314: "Profile/Settings incl. **DeleteAccount/GDPR**"). Guideline
**5.1.1(v)** requires that any app offering account *creation* also offer **in-app account deletion** (not
merely a web link or an email request) that initiates deletion of the account **and** its data. The flow
exists; the obligation is that it is **reachable in-app from Settings** and actually triggers the deletion (not
just a deactivation). This is a verifiable reachability + behavior item (D2/D3).

**Payments are real-world SERVICES → external payment (Stripe) is ALLOWED and must NOT use IAP (Guideline
3.1.3(e)/3.1.5(a)).** Cleansia sells **physical cleaning services** performed in the real world. Apple's IAP
mandate (3.1.1) applies to **digital** goods/services consumed *within* the app; **physical, real-world
services** are explicitly **outside** IAP and **must** use a payment method **other than** IAP — Stripe
PaymentSheet (ADR-0013 D7) is correct and compliant. **This must be documented** so a reviewer (ours or Apple's
during a back-and-forth) does not **wrongly** demand IAP, and so the app's metadata/description frames the
purchase as a real-world service. (The `SubscribePlus` membership is the one to watch — if it ever becomes a
purely-digital benefit it could be argued into IAP territory; as a discount/benefit tied to real-world cleaning
services it stays external. Recorded as a watch-item, D2.) This is a *defensive documentation* item, not a code
change.

**Privacy is the densest cluster of concrete, mechanical requirements.** Each is a verifiable artifact:
- **`PrivacyInfo.xcprivacy` privacy manifest** (required since 2024): declares **required-reason API** usage
  (e.g. `UserDefaults`/file-timestamp/disk-space/system-boot-time reason codes — the generated client + auth +
  Keychain code may touch these), the **data collection types** the app collects (account data, location,
  photos, payment info, device identifiers), and **tracking** (the app does **not** track across apps/websites
  — see ATT below). Required per app target.
- **ATT (AppTrackingTransparency):** required **only if** the app tracks the user across other companies' apps
  /websites or shares data with data brokers for advertising. Cleansia's apps **do not** — they collect data to
  operate the service (booking, auth, push), not to track for ads. So the apps **declare "no tracking"** in the
  privacy manifest + App Privacy nutrition label and **do NOT** present the ATT prompt (presenting ATT without
  tracking is itself a confusing/rejectable pattern). **Confirmed and declared accordingly** (D2). (If any
  analytics SDK that tracks is ever added, this flips — recorded as a watch-item.)
- **Info.plist purpose strings** (the app crashes/rejects without them when the API is touched):
  `NSLocationWhenInUseUsageDescription` (the MapKit address pickers / service-area — ADR-0013 D6),
  `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` / `NSPhotoLibraryAddUsageDescription` (partner
  order photos — ADR-0013 T-0308 — and customer dispute evidence — T-0314). **Push/APNs does NOT use a
  purpose-string Info.plist key** — it uses the `aps-environment` entitlement + the runtime
  `UNUserNotificationCenter.requestAuthorization` permission prompt (whose copy is the request, not an
  Info.plist string). (Recorded precisely so the dev does not invent a non-existent
  "NSUserNotificationsUsageDescription" key — the push permission is an entitlement + a runtime request, D2.)
- **App Privacy "nutrition label"** in App Store Connect: the data-collection declaration that must **match**
  the privacy manifest + the app's actual behavior (account, location, photos, payments, identifiers; linked to
  identity where the account links them; **not** used for tracking).
- **The standard rejection-bait the guidelines enumerate:** **no private APIs** (2.5.1), **no
  hidden/undocumented/disabled features** (2.3.1 — the "concealed functionality" that *is* the real
  account-level risk, the thing the AI-detector myth is a distorted echo of), **complete and accurate
  metadata** (2.3 — screenshots/description match the app), **functional on submission** (2.1 — no broken
  flows, no demo-account gaps; the reviewer must be able to exercise the app — a **demo account** for the
  authed flows is provided), **crash-free** (2.1), **no placeholder/lorem content** (2.3.x).
- **HIG + accessibility as quality items** (not hard rejections in most cases, but the "polished" bar the owner
  wants and a 4.0-design expectation): **VoiceOver labels** on actionable controls, **Dynamic Type** support,
  **contrast**, hit-target sizes, no truncated/clipped layouts. These ride the per-ticket gate as quality
  checks (D3).

**Why this is ONE decision.** The lint gate, the guideline checklist, the per-ticket gate, and the
pre-submission audit are **inseparable** as a *compliance posture*: the lint gate is the mechanical floor; the
checklist is the human-verifiable floor; the per-ticket gate is how the checklist is *enforced continuously*
(so compliance is not a single end-of-project scramble); and the pre-submission audit is the final
consolidated proof. Splitting them would let the lint gate exist without the guideline checklist it serves, or
the checklist exist without a place it is verified. The *implementation* (the specific tickets) is split below.

---

## Decision

> **Contract principle.** The iOS apps are held to a **submission-passing bar**: (1) **STRICT SwiftLint +
> SwiftFormat as a BLOCKING iOS CI gate** (a lint/format failure fails the build — unlike the platform's
> non-blocking FE lint); (2) a **concrete, verifiable App Store Review Guidelines checklist** every item of
> which a reviewer can check — the **privacy manifest**, **App Privacy nutrition label**, **"no tracking" / no
> ATT prompt**, the **Info.plist purpose strings**, **in-app account deletion (5.1.1(v))**, **Sign in with
> Apple on the customer app (4.8, because it offers Google Sign-In)**, **external Stripe payment for real-world
> services documented as non-IAP (3.1.3/3.1.5)**, and the standard **no-private-API / no-hidden-feature /
> complete-metadata / functional / crash-free / no-placeholder** floor plus **HIG/accessibility** quality
> items; (3) a **standing "App Review Compliance" gate (Gate-AR)** the reviewer + ios charters run on **every**
> iOS ticket; and (4) a **pre-submission audit artifact** (`agents/backlog/ios-app-review-checklist.md`) the
> team runs before the first TestFlight/App Store submission. **The bar is engineered against the published
> guidelines — there is no AI-code detector and App Review cannot damage hardware; the risk is rejection +
> account-level consequences for concealment/abuse, both of which this checklist directly defends.**

### D1 — Code-quality gate: STRICT SwiftLint + SwiftFormat, BLOCKING in iOS CI

- **SwiftLint** (lint rules) **+ SwiftFormat** (deterministic formatting) run as a **required iOS CI job** — a
  violation **fails the build and blocks merge.** This is the explicit *delta* from the rest of the platform:
  the **frontend** lint is **non-blocking baseline-debt** (a known carried debt that does not block); the **iOS
  lint is a hard gate** from day one (greenfield — there is no baseline debt to grandfather, so strict is free).
- **Config location:** `src/cleansia_ios/.swiftlint.yml` + `src/cleansia_ios/.swiftformat`, checked in, applied
  to **both** app targets + `CleansiaCore`. The **generated client directory is excluded** (the codegen output
  is never hand-edited per ADR-0013 D5 — linting it is noise; its quality is the generator's). Auth/headers and
  all hand-written code are **in** scope.
- **Rule severity = STRICT:** the analyzer/opt-in rules are enabled (e.g. `force_unwrapping`,
  `force_cast`, `force_try` as **errors** — a force-unwrap is a crash risk and crashes are a 2.1 rejection;
  `empty_count`, `closure_spacing`, `unused_import`, `redundant_*`, file/type/function length limits, cyclomatic
  complexity). `force_unwrapping`/`force_try`/`force_cast` being **errors** directly serves the **crash-free**
  guideline (D2) — the lint gate is not cosmetic, it removes the most common iOS crash class before review.
  SwiftFormat runs in `--lint` mode in CI (fails on unformatted code) and developers run it in write mode
  locally.
- **Why strict + blocking (and why it differs from FE):** the owner's bar is "much higher"; greenfield means no
  debt to grandfather (the FE lint is non-blocking *because* it carries pre-existing debt — that justification
  does not exist for iOS); and a blocking lint gate is the mechanical mirror of the reviewer discipline the
  platform applies by hand. It is the cheapest, most objective enforcement of "clean, standards-compliant code."

### D2 — The App Store Review Guidelines checklist (each item VERIFIABLE)

Every item is phrased so a reviewer can confirm it. Grouped; each cites its guideline and its evidence.

**Privacy (the mechanical cluster):**
- **AR-PRIV-1 — Privacy manifest.** Each app target ships a **`PrivacyInfo.xcprivacy`** declaring (a) the
  **required-reason API** reason codes for every such API the app + `CleansiaCore` use (audit the Keychain/auth
  /generated-client/UserDefaults usage), (b) the **collected data types** (account info, coarse/precise
  location, photos, payment info, device id), and (c) **tracking = false**. *Verify:* the file exists per
  target, builds, and the declared types match actual behavior.
- **AR-PRIV-2 — App Privacy nutrition label** in App Store Connect **matches** AR-PRIV-1 and the app's behavior
  (account/location/photos/payments/identifiers, linked-to-identity where the account links them, **not used
  for tracking**). *Verify:* the ASC declaration equals the manifest + the data-flow reality.
- **AR-PRIV-3 — No tracking, no ATT prompt.** The apps do **not** track across other companies'
  apps/sites and present **no `ATTrackingManager` prompt**. *Verify:* no `AppTrackingTransparency` import / no
  ATT request anywhere; tracking declared false. (Watch-item: adding a tracking SDK flips this — then ATT +
  manifest change become mandatory.)
- **AR-PRIV-4 — Info.plist purpose strings present + accurate (localized in all 5 locales):**
  `NSLocationWhenInUseUsageDescription` (address pickers / service-area), `NSCameraUsageDescription` +
  `NSPhotoLibraryUsageDescription` (+ `NSPhotoLibraryAddUsageDescription` if saving) for partner photos +
  dispute evidence. The strings **describe the real use** (a generic/empty string is a rejection). **Push uses
  the `aps-environment` entitlement + the runtime `UNUserNotificationCenter` permission request — NOT an
  Info.plist purpose key.** *Verify:* each capability the app actually exercises has its purpose string; no
  purpose string for a capability the app does **not** use (an unused permission is also a rejection); push has
  the entitlement, not a phantom Info.plist key.

**Account & login:**
- **AR-ACCT-1 — In-app account deletion (5.1.1(v)).** The **customer** app's Settings reaches a working
  **Delete Account** flow that initiates account **+ data** deletion via the existing `GdprDeletionService`
  path (not a web link, not an email request, not a mere deactivation). *Verify:* a path Settings → Delete
  Account → confirmation → backend deletion exists and is reachable without leaving the app.
- **AR-ACCT-2 — Sign in with Apple on the customer app (4.8).** Because the customer app offers **Google
  Sign-In**, it **must** also offer **Sign in with Apple** (the 4.8-satisfying privacy-focused option). The
  **partner** app has no social login → **no SIWA obligation.** *Verify:* the customer sign-in screen presents
  SIWA alongside Google + email; SIWA produces a working authenticated session through the
  backend (the integration shape — a backend `appleauth` analogue to `googleauth`, or SIWA-issued-token
  exchange — is a **decision flagged to the owner**, Q-IOS-04, because it may need a backend endpoint; the
  *obligation* is real and recorded here, the *integration mechanism* is the open input).

**Payments:**
- **AR-PAY-1 — External payment is correct; IAP is NOT required (3.1.3(e)/3.1.5(a)).** Cleaning is a
  **real-world service**, so Stripe PaymentSheet (ADR-0013 D7) is compliant and IAP must **not** be used. This
  item is **documentation + framing**: the app/metadata presents purchases as real-world services; the
  pre-submission audit carries the guideline citation so a reviewer back-and-forth does not wrongly demand IAP.
  *Verify:* no IAP/StoreKit purchase for cleaning services; the metadata frames real-world services; the
  citation is on file. (Watch-item: `SubscribePlus` must remain a benefit tied to real-world services, not a
  purely-digital good.)

**The standard rejection floor + quality:**
- **AR-STD-1 — No private APIs** (2.5.1); **no hidden/disabled/undocumented features** (2.3.1 — the real
  account-level-risk item: nothing in the build is concealed from the reviewer or remotely toggled to change
  behavior post-review).
- **AR-STD-2 — Complete + accurate metadata** (2.3): screenshots, description, keywords match the app; **a demo
  account** is provided for the authed flows so the reviewer can exercise partner + customer paths (2.1).
- **AR-STD-3 — Functional + crash-free** (2.1): no broken/dead-end flows, no placeholder/lorem content
  (2.3.x); the lint gate's `force_unwrapping`-as-error (D1) removes the top crash class.
- **AR-QUAL-1 — HIG + accessibility** (the polish bar): VoiceOver labels on actionable controls, Dynamic Type,
  contrast, hit targets, no clipped layouts, the 5-locale completeness (ADR-0013 D11 — no hardcoded strings).

### D3 — Mapping to tickets + a standing per-ticket gate (Gate-AR)

Two mechanisms — a **standing gate** (continuous) + **specific tickets** (the artifacts):

- **Gate-AR — a standing "App Review Compliance" gate on EVERY iOS ticket** (the reviewer + ios charters run it,
  the way the platform runs Gate 0 / the ADR-0013/0014 reviewer checks #1–#13). On every iOS ticket: (i) the
  **D1 lint/format gate is green** (mechanical); (ii) any **capability the ticket introduces carries its
  purpose string + privacy-manifest entry + locale strings** (e.g. the photo ticket T-0308 must add the camera
  /photo purpose strings + manifest types **in the same ticket** — compliance is not deferred); (iii) **no
  hidden feature / no private API / no placeholder** in the ticket's surface; (iv) **VoiceOver/Dynamic Type**
  on new controls. This makes compliance **continuous** — caught per-ticket, not in an end-of-project scramble.
- **Specific tickets** (the consolidated artifacts the standing gate cannot produce piecemeal):
  - the **SwiftLint/SwiftFormat gate** ticket (D1 — configs + the blocking CI job),
  - the **privacy-manifest** ticket (the `PrivacyInfo.xcprivacy` per target + the required-reason-API audit),
  - the **Sign-in-with-Apple** ticket (customer app, 4.8 — **gated on Q-IOS-04**, the integration-mechanism
    owner input),
  - the **purpose-strings / Info.plist + entitlements** ticket (the central capability declaration + the push
    entitlement),
  - the **account-deletion-reachability** verification ticket (5.1.1(v) — confirm Settings → Delete reaches the
    GDPR path in-app),
  - the **pre-submission audit** ticket (runs the D4 artifact end-to-end).

### D4 — The pre-submission audit artifact

A checklist artifact lives at **`agents/backlog/ios-app-review-checklist.md`** — the single document the team
runs **before the first TestFlight/App Store submission** (and re-runs before any subsequent submission). It
enumerates **every AR-* item from D2** as a checkbox with its guideline citation, the evidence/where-to-look,
and a pass/fail line, **per app** (partner + customer differ: SIWA + account-deletion + Google are
customer-only; both have the privacy/purpose-string/lint/standard items). It also carries the **App Store
Connect submission prerequisites** (demo account, App Privacy answers, export-compliance/encryption declaration
— the apps use standard HTTPS/Keychain crypto, which is the standard "uses exempt encryption" answer; screenshot
set; age rating; the SIWA + external-payment notes for the reviewer). It is **owned by the reviewer/ios
charters** and is a **release-gate**, not a feature doc — the first submission does not proceed until it is
fully green.

### D5 — Scope guard

This ADR decides the **compliance/quality bar + its enforcement**. It does **not**: write Swift code or the
lint configs (consumer tickets); re-open any ADR-0013/0014 architecture decision (it *adds* the bar those apps
clear); design the SIWA **backend integration** (the obligation is recorded; the mechanism is Q-IOS-04 to the
owner — it may need a backend `appleauth` endpoint, which would be a separate backend ticket + spec-regen);
change the Android apps (the obligations are iOS App-Store-specific — Android's Play requirements are a separate
concern). A future App-Review guideline change, or adding a tracking SDK (flipping AR-PRIV-3/ATT), is revisited
against this ADR.

---

## Alternatives considered

- **Engineer against a presumed "AI-code detector" / fear of hardware bricking.** Rejected — **both are myths
  this ADR explicitly corrects.** Apple App Review reviews the *submitted app + metadata* against the
  *published guidelines*; there is no AI-authorship detector and review cannot damage hardware. Building for a
  phantom wastes effort and misses the **real** rejection causes (privacy manifest, missing purpose strings,
  missing SIWA, missing in-app deletion, concealed functionality). This ADR engineers for the knowable
  checklist instead.
- **Non-blocking iOS lint (mirror the frontend's baseline-debt posture).** Rejected (D1). The FE lint is
  non-blocking *because* it grandfathers pre-existing debt; iOS is **greenfield** with **no debt to
  grandfather**, and the owner's bar is "much higher." A blocking strict gate is free at greenfield and is the
  mechanical enforcement of the clean-code bar. (FE's non-blocking choice is correct *for FE* — it is not a
  precedent for greenfield iOS.)
- **Skip Sign in with Apple (treat 4.8 as optional).** Rejected (D2/AR-ACCT-2). 4.8 is **triggered** by the
  customer app offering **Google Sign-In** — it is a real obligation, not optional. Shipping Google without SIWA
  is a near-certain rejection. The **partner** app (no social login) correctly has **no** SIWA work — the ADR
  scopes the obligation precisely (customer-only), which avoids over-building it on partner.
- **Treat in-app account deletion as satisfiable by a web link / email request.** Rejected (AR-ACCT-1).
  5.1.1(v) requires **in-app** deletion that initiates account **+ data** removal; a link/email is
  non-compliant. The flow already exists (`GdprDeletionService` + the customer delete flow) — the obligation is
  *reachability + real deletion in-app*, which the audit verifies.
- **Use IAP for the booking/membership payments to be "safe."** Rejected (AR-PAY-1). Cleaning is a **real-world
  service** — IAP is for **digital** goods consumed in-app; using IAP for a real-world service is itself wrong
  (and Apple does not permit IAP for physical services). Stripe external payment is **compliant**; the ADR
  documents the citation so a reviewer back-and-forth does not wrongly push IAP. (The watch-item keeps
  `SubscribePlus` framed as a real-world-service benefit.)
- **A single end-of-project compliance pass instead of a per-ticket gate.** Rejected (D3). A late single pass
  discovers missing purpose strings / manifest entries / locale strings *after* the features are built — the
  expensive time to fix them. Gate-AR makes each ticket carry its own compliance (the photo ticket adds its
  camera purpose string in-ticket), so the pre-submission audit (D4) confirms an already-compliant app rather
  than retrofitting one.
- **Invent an `NSUserNotificationsUsageDescription` Info.plist key for push.** Rejected (AR-PRIV-4) — **that
  key does not exist.** Push authorization is the `aps-environment` **entitlement** + the **runtime**
  `UNUserNotificationCenter.requestAuthorization` prompt. Recorded so the dev does not add a non-existent key
  (which would be confusing, not rejecting, but it signals a misunderstanding the ADR pre-empts).

---

## Consequences

**Cheaper / safer:**
- **Rejection risk is engineered down to the knowable set.** Every common rejection cause (missing privacy
  manifest, missing/empty purpose strings, missing SIWA when Google is offered, missing in-app deletion,
  concealed functionality, crashes, placeholder content) has a checklist item + an enforcement point — the
  first submission is a *confirmation*, not a gamble.
- **The blocking lint gate removes the top crash class for free at greenfield** (`force_unwrapping`/`force_try`/
  `force_cast` as errors → fewer 2.1 crash rejections) and enforces the clean-code bar mechanically.
- **Compliance is continuous, not a scramble** — Gate-AR makes each ticket pay its own compliance cost in-ticket
  (purpose string + manifest + locales with the feature), so the pre-submission audit verifies an
  already-compliant app.
- **The myth is killed in the record** — future agents/owners read that there is no AI-detector and review
  cannot brick hardware, so effort goes to the real guidelines.

**More expensive (new obligations):**
- **A SIWA build on the customer app** (AR-ACCT-2) — net-new vs Android (which has no SIWA), and it may require
  a **backend `appleauth` endpoint + a spec-regen** (Q-IOS-04). Real work, flagged.
- **The privacy manifest + purpose-string discipline** is a per-app + per-capability-introducing-ticket
  obligation (Gate-AR) the reviewer must enforce continuously.
- **A blocking iOS lint/format CI job** must be stood up early and kept green (a red lint blocks merge — by
  design).
- **Owner App-Store-Connect steps** (the pre-submission audit's ASC half): App Privacy answers, demo account,
  export-compliance declaration, screenshots, age rating, the Apple Developer account/signing (the ADR-0013
  owner step). These are owner/portal steps, flagged.
- **The pre-submission audit is a release gate** — the first submission waits on it being fully green per app.

**Rollout (consumer tickets — appended to `status/sprint-12.md`, continuing T-0296…):**
- The SwiftLint/SwiftFormat blocking-gate ticket (early — it gates everything after).
- The privacy-manifest ticket; the purpose-strings/Info.plist+entitlements ticket; the SIWA ticket (gated on
  Q-IOS-04); the account-deletion-reachability verification ticket; the pre-submission audit ticket.
- Gate-AR added to the iOS reviewer-check list (every ticket).

---

## How a reviewer verifies compliance

**Mechanical / structural (the gate):**
1. **Blocking lint/format gate exists + is green.** `src/cleansia_ios/.swiftlint.yml` + `.swiftformat` are
   checked in; the iOS CI has a **required** SwiftLint + SwiftFormat-`--lint` job that **fails the build** on a
   violation; `force_unwrapping`/`force_try`/`force_cast` are **errors**; the generated-client dir is excluded,
   all hand-written code is in scope. (A non-blocking lint job is a finding — the iOS gate must block.)
2. **Privacy manifest per target.** Each app target has `PrivacyInfo.xcprivacy` declaring required-reason-API
   reason codes, the collected data types, and **tracking = false**; the declared types match the app's actual
   data flow.
3. **No ATT / no tracking.** No `AppTrackingTransparency` import, no ATT prompt; tracking declared false in the
   manifest + the ASC nutrition label.
4. **Purpose strings present + accurate, no orphans.** Every capability the app **exercises**
   (location/camera/photos) has a localized, descriptive Info.plist purpose string (all 5 locales); **no**
   purpose string for an unused capability; push uses the `aps-environment` entitlement + the runtime
   `UNUserNotificationCenter` request, **not** an Info.plist key.
5. **In-app account deletion reachable (customer).** Settings → Delete Account reaches a working in-app flow
   that triggers `GdprDeletionService` account+data deletion (not a web link / email / deactivation).
6. **Sign in with Apple on the customer app.** The customer sign-in surface presents SIWA alongside Google +
   email and yields a working session; the **partner** app has no SIWA (and correctly no Google).
7. **No IAP for cleaning services.** No StoreKit/IAP purchase path for bookings/membership; Stripe PaymentSheet
   is the payment; the metadata frames real-world services; the 3.1.3/3.1.5 citation is on file.
8. **Standard floor.** No private API, no hidden/remote-toggled feature, no placeholder/lorem content; a demo
   account is provided; the build is functional + crash-free on the supported devices.
9. **Accessibility/HIG.** Actionable controls have VoiceOver labels; Dynamic Type + contrast respected; no
   clipped layouts; 5-locale completeness, no hardcoded strings (ADR-0013 D11).
10. **Gate-AR applied per ticket.** Each merged iOS ticket that introduced a capability carries its purpose
    string + manifest entry + locale strings **in that ticket** (compliance was not deferred).
11. **The pre-submission audit is green.** `agents/backlog/ios-app-review-checklist.md` is fully checked per app
    before the first submission, including the ASC prerequisites (App Privacy, demo account, export compliance,
    screenshots, age rating).

**Test/verification contract:**
- **TC-AR-LINT:** a deliberate `force_unwrap` / unformatted file fails the iOS CI (the gate blocks).
- **TC-AR-DELETE:** an automated/manual path Settings → Delete Account → confirm reaches the deletion call
  in-app (customer).
- **TC-AR-SIWA:** the customer sign-in screen renders SIWA and a SIWA sign-in produces an authenticated session
  (once Q-IOS-04's mechanism is decided).
- **TC-AR-PURPOSE:** launching a flow that uses location/camera/photos shows the correct localized purpose
  prompt (string present + accurate); no prompt appears for an unused capability.

---

## Roles affected

No new domain aggregates. Process/role updates (same change as this ADR, per the pattern-evolution loop):
- **`agents/knowledge/patterns-mobile.md`** iOS section gains an **App Review compliance** subsection
  cross-referencing ADR-0016 (the blocking lint gate, the AR-* checklist, Gate-AR, the privacy
  manifest/purpose-string/SIWA/in-app-deletion/external-payment items).
- **`agents/knowledge/consistency.md`** notes **Gate-AR** as a standing iOS gate (the per-ticket compliance
  check) alongside the ADR-0013/0014 reviewer checks.
- The **reviewer** and **ios** charters run **Gate-AR** on every iOS ticket (recorded in their check lists).
- The living companion `agents/architecture/decisions/ios-app-architecture.md` gains a compliance section; the
  pre-submission audit artifact `agents/backlog/ios-app-review-checklist.md` is created (D4).

---

## Open questions raised (owner)

Filed in `agents/backlog/questions/open.md`:
- **Q-IOS-04 (`pre-submission` — gates the SIWA ticket only, not the iOS plan; owner + architect)** — the
  **Sign in with Apple integration mechanism.** The 4.8 **obligation is confirmed** (the customer app offers
  Google Sign-In, so SIWA is required). The open input is **how** SIWA authenticates against the backend: a new
  backend **`appleauth`** anon endpoint (analogous to `googleauth`, validating the Apple identity token →
  issuing the mobile JWT — a backend ticket + a spec-regen), **or** an existing token-exchange path. **Default
  taken (to keep planning moving):** assume a **backend `appleauth` endpoint is needed** (the safe assumption —
  it mirrors `googleauth`), sized as a backend + iOS pair, **gated on the owner confirming the backend
  appetite** (it touches the auth contract, so it is owner-ratified). The obligation does not block the rest of
  the iOS plan — only the SIWA ticket waits on this.

This does **not** block the iOS plan. The **owner App-Store-Connect steps** (Apple Developer account/signing —
already an ADR-0013 step; App Privacy answers; demo account; export-compliance declaration; screenshots; age
rating) are **submission prerequisites** on the pre-submission audit, not architecture questions.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted (grounded in the published App Store Review Guidelines + the real app behavior: Google Sign-In +
GDPR-delete in the customer app, photos in both, Stripe for real-world services, the iOS-16/SwiftUI stack);
challengers (myth/over-engineering, obligation-reality, scope-creep) attacked; the Lead verified the guideline
citations + the app-behavior facts and adjudicated. **Verdict: all challenges RESOLVED; zero blocking (one
owner question — the SIWA integration mechanism — escalated with a default that does not block the plan);
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (framing) | The whole premise (pass App Review, held to a higher bar) smells like the "AI-detector / it'll brick the device" myth — is this ADR chasing a phantom? (MAJOR — it would misdirect all the effort) | REBUT + CORRECT | The framing block + Alternatives: **stated plainly that there is NO AI-code detector and review cannot brick hardware** — both FALSE. The ADR engineers for the **real** risk: rejection vs the **published guidelines** + account-level consequences for **concealed functionality** (the real thing the myth distorts). Every AR-* item is reviewer-verifiable. |
| CH-2 (obligation) | Is **Sign in with Apple** actually required, or a nice-to-have you're over-scoping? (MAJOR — it's net-new work + maybe a backend endpoint) | REBUT (with the guideline + the app fact) | D2/AR-ACCT-2: Guideline **4.8** is **triggered** because the customer app offers **Google Sign-In** (`customer-app/.../auth/AuthModule.kt`, `SignUpScreen.kt`). SIWA is then **required**, not optional. Scoped **precisely**: customer-only (partner has no social login → no SIWA). The *mechanism* (backend `appleauth`?) is the only open input → Q-IOS-04, which gates only the SIWA ticket. |
| CH-3 (payments) | If we're being maximally safe for Apple, shouldn't payments use **IAP**? (MODERATE — a wrong "safe" instinct that would be expensive + wrong) | REBUT | D2/AR-PAY-1 + Alternatives: cleaning is a **real-world service** → IAP is for **digital** goods consumed in-app; using IAP here is **wrong** and not permitted for physical services. Stripe external payment (ADR-0013 D7) is **compliant**. The ADR documents the **3.1.3/3.1.5 citation** so a reviewer back-and-forth does not wrongly demand IAP, and watch-items `SubscribePlus` to stay a real-world-service benefit. |
| CH-4 (lint posture) | The platform's FE lint is **non-blocking**; making iOS lint **blocking** is inconsistent — why the special case? (MODERATE — consistency) | DEFEND | D1 + Alternatives: the FE lint is non-blocking *because it grandfathers pre-existing debt*; iOS is **greenfield** (no debt to grandfather) and the owner's bar is **higher**. Strict+blocking is **free** at greenfield and is the mechanical mirror of the reviewer discipline; `force_unwrapping`-as-error directly removes the top **crash** (2.1) rejection class. The inconsistency is *intended* and justified by the debt difference + the owner bar. |
| CH-5 (scramble vs gate) | A pre-submission checklist at the end is enough — a per-ticket gate is process overhead. (MODERATE) | DEFEND | D3 + Alternatives: an end-only pass discovers missing purpose strings/manifest entries/locale strings **after** features are built — the expensive fix time. **Gate-AR** makes each ticket carry its own compliance (the photo ticket adds its camera purpose string in-ticket), so the pre-submission audit **confirms** an already-compliant app instead of retrofitting one. The standing gate is the same shape as the existing per-ticket reviewer checks — not new overhead, the same discipline. |
| CH-6 (precision) | Push needs a usage-description Info.plist key like the camera/location ones — is the checklist even accurate? (MODERATE — an inaccurate checklist erodes trust) | REBUT (corrected) | AR-PRIV-4 + Alternatives: **there is no `NSUserNotificationsUsageDescription` key** — push authorization is the **`aps-environment` entitlement** + the **runtime** `UNUserNotificationCenter.requestAuthorization` prompt. The ADR records this precisely so the dev does not add a non-existent key. (The checklist's accuracy is part of its credibility — this is exactly the kind of precision the bar requires.) |

**Affirmed unchallenged:** the privacy-manifest requirement + the App-Privacy-nutrition-label match; the
no-tracking / no-ATT declaration (the apps operate the service, they do not track for ads); the in-app
account-deletion obligation (5.1.1(v), the flow already exists); the standard floor (no private API / no hidden
feature / complete metadata / functional / crash-free / no placeholder); HIG/accessibility as the polish bar;
the pre-submission audit as a release gate; the per-app difference (SIWA + Google + account-deletion are
customer-only).

**Lead verification (guideline + app-behavior facts, 2026-06-23):** Google Sign-In present in the **customer**
Android app (`customer-app/.../auth/AuthModule.kt`, `SignUpScreen.kt`) + the backend `googleauth` anon endpoint
(ADR-0013 D4.4 allow-list) → **4.8 SIWA obligation is real, customer-only**; the GDPR/`GdprDeletionService` +
the customer DeleteAccount flow (sprint-12 T-0314) → **5.1.1(v) in-app-deletion obligation is real,
customer-only**; partner + customer both upload photos (T-0308 partner base64, T-0314 customer dispute
multipart) → **camera/photo purpose strings + manifest types required**; MapKit address pickers (ADR-0013 D6) →
**location purpose string**; cleaning = real-world service → **Stripe external payment compliant, IAP not
required (3.1.3/3.1.5)**; push = `aps-environment` entitlement + runtime `UNUserNotificationCenter` request (no
Info.plist purpose key); greenfield iOS (no lint debt to grandfather) → **strict blocking lint is free**. The
"AI-code detector" and "review bricks hardware" beliefs are **not** in the App Store Review Guidelines and are
factually false — the ADR engineers for the published guidelines.

**Escalations to the owner:** one — **Q-IOS-04** (the SIWA backend integration mechanism), which gates only the
SIWA ticket and proceeds on the safe `appleauth`-endpoint default. The App-Store-Connect submission
prerequisites (App Privacy answers, demo account, export-compliance, screenshots, age rating, signing) are
owner steps on the pre-submission audit, not blocking questions.
