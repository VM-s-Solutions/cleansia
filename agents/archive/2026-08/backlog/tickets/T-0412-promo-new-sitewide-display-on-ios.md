---
id: T-0412
title: "iOS — promo.new_sitewide display: event-scoped literal ApsAlert pass-through; apns-priority 5, interruption-level passive; Promo opt-in stays server-side default-OFF"
status: done
size: S
owner: architect
created: 2026-07-17
updated: 2026-07-19
depends_on: [T-0403, T-0404]
blocks: []
stories: []
adrs: [0025]
layers: [architect, backend, ios]
security_touching: true
priority: medium
manual_steps: []
sprint: 12
source: ADR-0025 Verdict CH-1 — the ratified "promo.new_sitewide display on iOS (literal ApsAlert from on-wire title/body)" follow-up (PM action)
---

> **The acknowledged customer parity gap from ADR-0025 (CH-1):** Android customers see sitewide promo
> pushes; iOS customers see nothing. `promo.new_sitewide` is *structurally* excluded from the ADR-0025
> loc-key map — it has no fixed template anywhere; the admin authors title/body per campaign in all 5
> locales and the fan-out ships the recipient's-language strings in the FCM **data** payload, which
> Android renders verbatim and iOS cannot display (data-only). ADR-0025 named the additive fix (a
> literal `ApsAlert.Title`/`Body` from the on-wire values) and deferred the three product decisions to
> this ticket: apns-priority, interruption-level, and the Promo opt-in default.

## Evidence (verified against source, 2026-07-17)

- **Fan-out is strictly opt-in server-side.** `SendSitewidePromoFanoutHandler.cs:115-123` pages only
  `UserNotificationPreferences` rows `Where(p => p.Promo)`; the domain default is `Promo = false`
  (`UserNotificationPreferences.cs:36` — "Marketing — opt-in. Default false", per spec). A user with
  no prefs row is never enqueued. The opt-in gate therefore lives at fan-out, per user, platform-blind.
- **All 5 locales are always authored.** `SendSitewidePromo.cs` validator: title ≤120 / body ≤500
  chars, `NotEmpty` for en/cs/sk/uk/ru. The fan-out picks per stored `User.PreferredLanguageCode`
  with `en` fallback and ships exactly `Args = { title, body }` (`SendSitewidePromoFanoutHandler.cs:160-179`).
- **Android renders it verbatim and quietly.** `CleansiaFirebaseMessagingService.kt:79-82` special-
  cases the key, drops on blank title/body (`takeIf { isNotBlank() } ?: return`); the Promo channel is
  **`IMPORTANCE_LOW`** (`NotificationChannels.kt:74-77`) — no sound, no heads-up peek. Promo tap
  deliberately lands on Home (`NotificationDeepLink.kt:66`).
- **Both clients already surface the Promo toggle.** Android `NotificationsScreen.kt:79`
  (`preferences?.promo ?: false`); iOS customer `NotificationsView.swift` +
  `NotificationPreferences.swift:91` (`promo: promo ?? false`). Nothing to build on the prefs surface.
- **The factory currently drops promo for iOS.** `FcmMessageFactory.cs:65-70` returns `Apns = null`
  for any key outside the 12-event loc-key map; ADR-0025's TC-PUSH-APNS-2 **pins** `promo.new_sitewide
  → Apns == null`. This ticket changes that pinned behavior → requires a supersede-in-part ADR (below).

## Decision (panel)

*(Architect defense panel — AUTHOR → CHALLENGER → LEAD, 2026-07-17. Protocol:
`agents/process/deliberation.md`.)*

### D1 — Pass-through mechanism: an **event-scoped literal branch**, NOT a generic unmapped-event fallback

`FcmMessageFactory.BuildApns` gains one explicit branch, keyed on
`NotificationEventCatalog.PromoNewSitewide` **only**:

```csharp
// Sketch — the wire contract, not final code.
if (eventKey == NotificationEventCatalog.PromoNewSitewide)
{
    var title = data.GetValueOrDefault("title");
    var body  = data.GetValueOrDefault("body");
    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        return null;                                  // Android takeIf{isNotBlank} ?: return parity
    return new ApnsConfig
    {
        Headers = new Dictionary<string, string> { ["apns-priority"] = "5" },        // D2
        Aps = new Aps
        {
            Alert = new ApsAlert { Title = title, Body = body },                     // literal fields
            // FirebaseAdmin .NET has no first-class interruption-level property:
            CustomData = new Dictionary<string, object> { ["interruption-level"] = "passive" }, // D2
            // NO Sound (passive suppresses it anyway; keep the payload honest).
            ThreadId = eventKey,                       // all campaigns group into one stack
        },
    };
}
```

- The 12-event **loc-key map stays untouched** (`promo.new_sitewide` remains absent — ADR-0025
  verification #2 still holds to the letter). The literal branch is a *second, deliberately narrow*
  gate: an explicit event-key allowlist for server-authored text, with exactly one member. A second
  literal event requires an ADR — pinned by comment **and** by tripwire test (AC3).
- `Data` stays byte-unchanged (title/body already ride there for Android); the alert duplicates
  ≤620 chars — worst-case Cyrillic ≈ 2.5 KB total, under the 4 KB APNs cap (budget test in AC3).

### D2 — apns-priority **5**, interruption-level **passive**, no sound

The mirror is Android's `IMPORTANCE_LOW` Promo channel: silent, no heads-up, visible in the shade.
The iOS analog is `interruption-level: passive` — no sound, no screen wake, still listed in
Notification Center and on the lock screen. `apns-priority: 5` is Apple's "deliver at an opportune
time" value; marketing is Apple's canonical not-priority-10 content, and priority 10 on promos is
exactly the posture App Review scrutinizes (ADR-0016 surface; Guideline 4.5.4 additionally requires
marketing pushes be explicit opt-in with an in-app opt-out — both already satisfied, see D3). Being
quiet is simultaneously the honest Android mirror *and* the review-safe choice. If the product later
wants louder promos, that is a both-platforms product decision (note: Android cannot raise an existing
channel's importance retroactively — new channel id; iOS is a header change).

### D3 — Promo opt-in default: **OFF (opt-in)** — already enforced server-side; nothing to invent on iOS

There is no per-platform default to decide: the gate is per-user at fan-out (`Where(p => p.Promo)`),
domain default `false`, and **both** apps already render the toggle default-off from the same API.
The decision is to *confirm the mirror*, not create one: no iOS code change, no new opt-in surface,
no dispatch-side change. (Noted for the record: the dispatch-side prefs check at
`SendPushNotificationHandler.cs:112` would pass a no-row user, but no-row users are never enqueued by
the fan-out, so opt-in holds end-to-end.)

### D4 — Localization: stored-preference text is an **accepted limitation**, identical on both platforms

The alert text is pre-localized per stored `PreferredLanguageCode` (en fallback) — not device locale.
This is *milder* than "single-language": the admin must author all 5 locales, so every user gets
their stored-preference language; the residual is stored-pref ≠ device-locale, and it is **identical
on Android by construction** (Android renders the same server-picked string), so zero cross-platform
divergence. **Eventual fix direction:** ship all 5 locales in the payload and select device-locale
client-side — on iOS that is the `mutable-content` + NSE seam ADR-0025 deliberately left open (the
first genuine on-device-transformation use case); on Android a data-payload pick. Not now: ×5 payload
bloat plus two app-extension targets for a marginal correction.

### D5 — Governance: supersede-in-part ADR required

ADR-0025 is `accepted`/immutable and pins TC-PUSH-APNS-2 (`promo.new_sitewide → Apns == null`).
Implementation must land with **ADR-0028** (supersedes ADR-0025 *only* in the promo drop-parity pin;
records D1–D4 verbatim from this panel) and update the living doc
`agents/architecture/decisions/push-notifications.md` in the same change.

## Challenge

*(Challenger mode — attacked against source, not the author's citations.)*

- **CH-A (the generic-fallback temptation).** "Skip the branch: any unmapped event carrying
  `title`/`body` args gets a literal alert — no factory edit next time." **Attacked as a seam
  break:** ADR-0025 D2 makes the display map the forward-compatibility gate (client-first rule, S6
  lock-screen allowlist). A generic literal fallback **silently un-gates every future unmapped
  event** whose producer happens to name args `title`/`body` — display with no client-first review,
  no S6 review, and a standing literal-text channel that grows by accident. The convenience is one
  saved factory edit per new literal event; the cost is an ungoverned lock-screen surface. The
  author's scoped branch must also answer the inverse: what stops event #2 from being quietly added
  to the *branch*?
- **CH-B (too quiet?).** Users **explicitly opted in** to promos — 4.5.4 is satisfied either way, so
  `active` (default level, sound) is permissible. Is `passive` + priority 5 burying the campaign the
  admin paid to write? Marketing wants impressions.
- **CH-C (payload budget).** Title+body now ride the wire **twice** (Data + alert). 120+500 chars of
  Cyrillic ≈ 1.24 KB ×2 plus envelope — asserted "under 4 KB" but not pinned by any test.
- **CH-D (foreground leak).** Both delegates' `willPresent → [.banner, .sound]` fires for *any*
  alert-type payload — a passive promo arriving in-foreground still banners. Does that break the
  quiet posture?
- **CH-E (tap parity).** Android promo tap deliberately lands on Home. iOS
  `CustomerNotificationDeepLink` returns nil for unknown keys → app just opens. Same outcome, but
  unpinned — nothing stops a future resolver edit from routing promo somewhere Android doesn't.
- **Checked and found sound (named, not silent):** the opt-in chain end-to-end (fan-out
  `Where(p => p.Promo)` → default-false row → both toggles default-off); the Android blank-drop
  parity in D1; `ThreadId = eventKey` grouping; ADR-0025 verification #2 surviving to the letter
  (map still 12, promo absent); no xcstrings keys needed (literal text needs no catalog, so the
  D5 day-one catalog gate is not implicated); partner Android drops promo → partner iOS parity
  holds automatically (customer-only fan-out).

## Defense

- **CH-A — REBUT the generic option, CONCEDE the tripwire.** Generic fallback rejected for exactly
  the stated reason — it converts the display map from a gate into a suggestion. The scoped branch
  is kept, and the "event #2" hole is closed mechanically: a new tripwire test (AC3) asserts the
  literal branch fires for **exactly** `{promo.new_sitewide}` across the whole
  `NotificationEventCatalog` — adding a second literal event fails the suite until an ADR ships and
  the test's pinned set is deliberately edited.
- **CH-B — REBUT.** Opt-in consent makes `active` *permissible*, not *right*. The parity rule
  (mirror, don't invent — ADR-0018's principle applied to behavior) says mirror Android's
  IMPORTANCE_LOW, which is silent and peek-less. A promo that buzzes on iOS but sits silent on
  Android is invented product behavior inside an infra ticket. Passive promos still appear on the
  lock screen and in Notification Center — impressions survive; interruptions don't. Louder-promos
  is a legitimate future product decision, named in D2 with its Android asymmetry priced.
- **CH-C — CONCEDE + REVISE.** A serialized-size budget test is added to AC3: max-length ru (Cyrillic)
  title+body through the factory must yield an APNs payload < 4096 bytes. No design change.
- **CH-D — REBUT (with a recorded nuance).** `willPresent` returning `.sound` plays *the
  notification's* sound — the promo payload carries none, so nothing plays; the foreground banner is
  the same treatment Android gives an in-foreground promo notification (posted to the shade). No
  code change; nuance recorded here so QA doesn't file it as a defect.
- **CH-E — CONCEDE + REVISE.** AC4 adds a resolver test pinning `promo.new_sitewide → nil`
  (open-app-only), mirroring Android's Home landing by test rather than by luck.

## Verdict

*(Lead mode — 2026-07-17.)* **CONSENSUS — decision finalized as amended; ticket `proposed` for the
PM. No owner escalation** (all three defaults are mirrors of existing, owner-ratified behavior — the
opt-in spec, the Android LOW channel — not new business policy).

| Challenge | Ruling |
|---|---|
| CH-A (generic fallback) | **Generic REJECTED; scoped branch stands** — tripwire test added (resolved) |
| CH-B (passive too quiet) | **DEFENDED** — parity + review posture beat impression-maximizing (resolved) |
| CH-C (4 KB budget) | **CONCEDED** — budget test added to AC3 (resolved) |
| CH-D (foreground banner) | **DEFENDED** — no sound in payload; parity with Android shade (resolved) |
| CH-E (tap parity unpinned) | **CONCEDED** — resolver test added to AC4 (resolved) |

**The three defaults (the panel's answer to ADR-0025's deferred product decisions):**
1. **apns-priority = `5`** (opportune delivery — marketing is never priority 10)
2. **interruption-level = `passive`** (no sound, no screen wake — the iOS analog of Android's IMPORTANCE_LOW Promo channel)
3. **Promo opt-in default = OFF** (opt-in; enforced server-side at fan-out, default-false row, toggle already default-off in both apps — confirmed mirror, no iOS change)

## Acceptance criteria

- [ ] **AC1 (architect)** — ADR-0028 filed: supersedes ADR-0025 **in part** (only the
  `promo.new_sitewide → Apns == null` drop pin / TC-PUSH-APNS-2), recording D1–D4;
  `agents/architecture/decisions/push-notifications.md` updated in the same change.
- [ ] **AC2 (backend)** — `FcmMessageFactory` gains the event-scoped literal branch per D1: literal
  `ApsAlert.Title`/`Body` from `data["title"]`/`data["body"]`; blank/missing either → `Apns == null`
  (Android drop parity); headers `apns-priority: "5"`; `aps` carries `interruption-level: "passive"`
  (via `Aps.CustomData` — no first-class SDK property); **no** `Sound`; `ThreadId = eventKey`. The
  loc-key map is untouched (still exactly 12 events); `Data` and `AndroidConfig` remain byte-unchanged
  (TC-PUSH-APNS-1 still green).
- [ ] **AC3 (backend tests, red-first)** — TC-PUSH-APNS-2 rewritten to pin the *unknown*-key drop
  only; **TC-PUSH-APNS-6** promo alert shape (literal title/body, priority 5, passive, no sound, no
  loc-keys); **TC-PUSH-APNS-7** blank/missing title or body → `Apns == null`; **TC-PUSH-APNS-8**
  tripwire — the literal branch fires for exactly `{promo.new_sitewide}` across every
  `NotificationEventCatalog` key; **TC-PUSH-APNS-9** budget — max-length Cyrillic title+body
  serializes to an APNs payload < 4096 bytes.
- [ ] **AC4 (iOS)** — no display code (the OS renders the literal alert; `willPresent` covers
  foreground). One test added: `CustomerNotificationDeepLink` resolves `promo.new_sitewide` →
  `nil` (open-app-only — Android Home-landing parity). No xcstrings changes.
- [ ] **AC5 (QA, device)** — an opted-in iOS customer receives a real campaign **silently** (no
  sound, no screen wake) and finds it on the lock screen / Notification Center, in the stored-
  preference language, background AND terminated; an opted-out (or no-prefs-row) user receives
  nothing; Android behavior unchanged. Console-test caveat from ADR-0025 applies — evidence is a
  real campaign send.
- [ ] **AC6** — `dotnet test` green; no change to `IPushDispatcher`, `SendPushNotificationHandler`,
  the fan-out handler, producers, or the queue contract.

## Status log
- 2026-07-17 — filed `proposed` from the architect defense panel (ADR-0025 Verdict CH-1 follow-up).
  Panel consensus recorded above; awaiting PM scheduling. Implementation gated on T-0403/T-0404's
  release train only insofar as iOS delivery must work at all for AC5 to be observable.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped on feature/i18n-cluster-3 (merged): event-scoped literal aps.alert for promo.new_sitewide only.
