# iOS pre-submission App Review checklist (the release gate)

> The single document the team runs **before the first TestFlight / App Store submission** (and re-runs before
> any subsequent submission). Owned by the **reviewer + ios** charters. Source of the bar: **ADR-0016**
> (`agents/backlog/adr/0016-apple-app-review-compliance-and-ios-quality-bar.md`). Consumer ticket that runs this
> end-to-end: **T-0329** (`status/sprint-12.md §10`). **The first submission does not proceed until every
> applicable box per app is green.**

**Framing (so the myth never re-enters the record):** there is **NO "AI-written-code detector"** in App Review,
and App Review **cannot brick/disable hardware** — both are **FALSE**. This checklist defends against the
**real** risk: **rejection vs the published App Store Review Guidelines** + account-level consequences for
**concealed functionality / abuse**. Every item below is something a reviewer (Apple's or ours) can verify.

**Per-app difference:** SIWA (4.8), in-app account deletion (5.1.1(v)), and Google Sign-In are **customer-only**.
The **partner** app has no social login and no account-creation-tied delete obligation beyond the standard
floor. Columns: ☐ Partner · ☐ Customer (mark N/A where an item is one-app-only).

---

## A. Code quality (the mechanical floor — ADR-0016 D1)

- [ ] **AR-LINT-1** — SwiftLint + SwiftFormat run as a **BLOCKING** iOS CI job (a violation fails the build).
      Configs at `src/cleansia_ios/.swiftlint.yml` + `.swiftformat`; STRICT (`force_unwrapping`/`force_try`/
      `force_cast` = **error**); generated-client dir excluded; all hand-written code in scope. *(both apps)*
- [ ] **AR-LINT-2** — the latest `main` build is lint-clean (no suppressions hiding violations in submitted code).

## B. Privacy (the densest cluster — ADR-0016 D2)

- [ ] **AR-PRIV-1 — Privacy manifest.** `PrivacyInfo.xcprivacy` present per app target; declares the
      **required-reason API** codes for every such API the app + `CleansiaCore` touch (Keychain/auth/generated
      client/UserDefaults audited), the **collected data types** (account, location, photos, payment info,
      device id), and **tracking = false**. The declared types **match** actual behavior. *(both apps)*
- [ ] **AR-PRIV-2 — App Privacy nutrition label** in App Store Connect **matches** AR-PRIV-1 and the data flow
      (account/location/photos/payments/identifiers; linked-to-identity where the account links them; **not**
      used for tracking). *(both apps — owner fills the ASC answers)*
- [ ] **AR-PRIV-3 — No tracking, no ATT prompt.** No `AppTrackingTransparency` import / no ATT request anywhere;
      tracking declared false. *(both apps)* — *watch-item: adding any tracking SDK flips this → ATT + manifest
      become mandatory.*
- [ ] **AR-PRIV-4 — Purpose strings present, accurate, localized ×5.** `NSLocationWhenInUseUsageDescription`
      (MapKit address pickers / service-area), `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription`
      (+ `NSPhotoLibraryAddUsageDescription` if saving) for partner photos (T-0308) + customer dispute evidence
      (T-0314). Strings describe the **real** use (no generic/empty string). **No purpose string for a
      capability the app does NOT use.** **Push uses the `aps-environment` entitlement + the runtime
      `UNUserNotificationCenter` request — NOT an Info.plist key** (no `NSUserNotificationsUsageDescription`).
      *(both apps — location/camera/photos per what each app exercises)*

## C. Account & login (customer-only — ADR-0016 D2)

- [ ] **AR-ACCT-1 — In-app account deletion (5.1.1(v)).** Customer Settings → Delete Account reaches a working
      **in-app** flow that triggers `GdprDeletionService` account **+ data** deletion (not a web link, not an
      email request, not a deactivation). *(customer)*
- [ ] **AR-ACCT-2 — Sign in with Apple (4.8).** Because the customer app offers **Google Sign-In**, the customer
      sign-in surface **also** presents **Sign in with Apple** and it yields a working authenticated session.
      *(customer)* — *depends on Q-IOS-04 (the SIWA backend mechanism — likely a backend `appleauth` endpoint).*
      *(partner: N/A — no social login.)*

## D. Payments (ADR-0016 D2)

- [ ] **AR-PAY-1 — External payment correct; no IAP.** No StoreKit/IAP purchase path for bookings/membership;
      Stripe PaymentSheet is the payment. Cleaning = a **real-world service**, so external payment is **compliant
      (3.1.3(e)/3.1.5(a))** and IAP must **not** be used. The metadata frames real-world services; the guideline
      citation is on file for any reviewer back-and-forth. *(customer)* — *watch-item: `SubscribePlus` must stay
      a real-world-service benefit, not a purely-digital good.*

## E. The standard rejection floor + quality (ADR-0016 D2)

- [ ] **AR-STD-1 — No private APIs** (2.5.1); **no hidden / disabled / remotely-toggled features** (2.3.1 — the
      real account-level-risk item). *(both apps)*
- [ ] **AR-STD-2 — Complete + accurate metadata** (2.3): screenshots/description/keywords match the app; **a demo
      account** is provided for the authed partner + customer flows (2.1). *(both apps — owner provides the demo
      account + the store assets)*
- [ ] **AR-STD-3 — Functional + crash-free** (2.1): no broken/dead-end flows, no placeholder/lorem content
      (2.3.x). The lint gate's `force_unwrapping`-as-error removes the top crash class. *(both apps)*
- [ ] **AR-QUAL-1 — HIG + accessibility:** VoiceOver labels on actionable controls; Dynamic Type; contrast;
      hit-target sizes; no clipped layouts; 5-locale completeness, no hardcoded strings (ADR-0013 D11).
      *(both apps)*

## G. Design parity — Gate-DP (the standing per-screen design gate — ADR-0018)

> Runs on **every iOS screen/feature ticket** (reviewer + ios charters), beside **Gate-AR** (§A–E, ADR-0016)
> and the **SwiftLint/SwiftFormat** gate (AR-LINT-1). The principle (ADR-0018): **same layout/flow/branding as
> the Android Compose apps, built with NATIVE SwiftUI components, and iOS convention WINS on a genuine
> component conflict.** Pure-infra tickets (codegen, auth layer, DI root) are **N/A**.

- [ ] **AR-DP-1 — Layout/flow/branding parity (Android cited).** The screen's **layout, flow, and branding
      match the corresponding Android Compose screen** — and the ticket **cites it** (`<path/Screen.kt>`). Same
      region arrangement, same flow position, same field set + order, same brand (colors/logo/type/spacing/icon
      meaning). A moved flow / dropped/added/merged screen or field / re-brand is a **blocking** finding. *(both apps)*
- [ ] **AR-DP-2 — Native SwiftUI components, standard iOS pattern.** Every control is a **native SwiftUI
      component** — **no Material re-implementation** (no faux Material text field/sheet/ripple). The standard
      iOS pattern is used for nav (`NavigationStack`/`TabView`), pickers (`DatePicker`/`Picker`/`Menu`), sheets
      (`.sheet`/`.presentationDetents`/`.confirmationDialog`/`.alert`), lists (`List`/`Form`/`swipeActions`),
      back (swipe-back + nav-bar back), images (`AsyncImage`). Platform affordances present where applicable:
      **SF Symbols** mapping the Android icon's meaning, **haptics**, **pull-to-refresh**, swipe-back. A
      faux-Material control or a missing standard affordance is a finding. *(both apps)*
- [ ] **AR-DP-3 — Conflicts resolved iOS-native + noted.** Where an Android and an iOS convention genuinely
      conflicted, the **iOS-native** pattern was chosen, the divergence is **noted in the ticket** (one line:
      "Android X → iOS-native Y, iOS convention"), and the divergence touches **only the component** — never
      layout/flow/branding. Canonical mappings (ADR-0018 D3): Compose bottom-nav → `TabView`; `ModalBottomSheet`
      → `.sheet`+`.presentationDetents`; Material `DatePicker` → native `DatePicker`; Material `TextField` →
      native `TextField`/`SecureField` (same labels + error strings ×5); Android system-back → swipe-back +
      `NavigationStack` back; Coil `AsyncImage` → SwiftUI `AsyncImage`/Kingfisher; Material `Snackbar` → native
      toast on the same `SnackbarController` bus; Material `AlertDialog` → `.alert`/`.confirmationDialog`. An
      **undocumented** divergence, or one that **moves layout/flow**, is a **blocking** finding. *(both apps)*

## F. App Store Connect submission prerequisites (owner — the ASC half)

- [ ] **App Privacy answers** completed in ASC, matching AR-PRIV-1/2.
- [ ] **Demo account** (partner + customer) provided in the review notes so the reviewer can exercise the authed
      flows.
- [ ] **Export-compliance / encryption declaration** — the apps use **standard HTTPS + Keychain crypto** → the
      standard "uses exempt encryption" answer. (Confirm — no custom/proprietary crypto added.)
- [ ] **Screenshots** for the required device sizes; **age rating** completed.
- [ ] **Reviewer notes** carry: the **SIWA** option location (customer), the **external-payment / real-world
      service** framing (no IAP, with the 3.1.3/3.1.5 citation), and the **in-app account-deletion** path
      (customer).
- [ ] **Apple Developer account + signing + bundle ids** (`cz.cleansia.partner` / `cz.cleansia.customer`) — the
      ADR-0013 owner provisioning step; the **APNs auth key/cert** for push (T-0311).

---

## Sign-off

| App | All applicable A–E green? | ASC prerequisites (F) green? | Signed off by | Date |
|---|---|---|---|---|
| Partner (`cz.cleansia.partner`) | ☐ | ☐ | | |
| Customer (`cz.cleansia.customer`) | ☐ | ☐ | | |

**The first submission proceeds only when both rows are fully green.** Re-run this checklist before every
subsequent submission (a metadata or capability change can re-open an item).
