# ADR-0025 — iOS push display: per-platform APNs alert block using `loc-key`/`loc-args` (localization stays client-side); no literal text on the wire, no Notification Service Extension

- **Status:** accepted (panel consensus 2026-07-15 — see Verdict)   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-07-15
- **Supersedes:** — (extends the ADR-0002 dispatch contract without touching it: the queue message,
  the D2.1 keys, the consumer's Mode-A guard ordering (ADR-0023 boundary), and the **data payload
  Android consumes are all byte-unchanged**; this ADR only adds an APNs-scoped display block)
- **Superseded by:** —
- **Backs / extends:** ADR-0002 (outbox/dispatch — the push consumer + `IPushDispatcher` seam),
  ADR-0016 (iOS quality bar — push display is user-visible Apple-review surface), ADR-0023 (push
  consumer stays Mode A / byte-untouched — nothing here changes claim ordering)
- **Applies to:** backend (`Cleansia.Infra.Clients.Fcm`) | ios (both apps) — Android is a
  **regression-pinned bystander**
- **Ticket:** T-0404 (owner: architect → backend + ios) · **depends_on T-0403** (iOS FCM-token
  integration — display work is moot until FCM registration tokens flow from the iOS apps)

> **One decision:** *what the FCM wire shape is per platform so transactional pushes surface on iOS.*
> The dispatcher attaches an **APNs-only alert block** whose title/body are **localization keys +
> allowlisted args** (`title-loc-key` / `loc-key` / `loc-args`), resolved by the iOS app from its own
> bundled string catalog at display time — exactly Android's "client renders from `event_key`" model,
> expressed in the only vocabulary iOS offers for background/terminated display. **No literal
> user-facing text and no PII crosses FCM/APNs; the Android data-only contract is byte-unchanged.**
> Once `accepted` this is immutable — supersede, never edit.

---

## Context

### The defect (verified against source)

The five customer order-status events (`order.confirmed`, `order.on_the_way`, `order.in_progress`,
`order.completed`, `order.refunded` — plus `order.cancelled` from the expired-checkout and
stale-recurring auto-cancel paths) ride the `notifications-dispatch` queue and fan out through the
**only** `IPushDispatcher`, `FcmPushDispatcher` (`src/Cleansia.Infra.Clients/Fcm/FcmPushDispatcher.cs`).
The message it builds (`:62–75`) is **data-only**:

```csharp
var payload = new Dictionary<string, string>(data) { ["event_key"] = eventKey };
var message = new MulticastMessage
{
    Tokens = deviceTokens.ToArray(),
    Data = payload,
    Android = new AndroidConfig { Priority = Priority.High },
};
```

No `Notification`, no `Apns`, no `content-available`. That is a **deliberate Android design**: the
Android apps render locally from `event_key` via `strings.xml` templates
(`cleansia_android/{customer,partner}-app/.../CleansiaFirebaseMessagingService.kt` — customer maps 11
keys, partner 6; unknown keys are **silently dropped**; device-locale localization; a Room-backed
in-app feed row is written alongside the system notification). The message contract
(`SendPushNotificationMessage`) pins "NEVER include PII — payload is visible on the device's lock
screen", and producers comply: the args on the wire today are `orderId`, `orderNumber`,
`disputeId`, `count`, `tier` — no names, no addresses — **plus, for `promo.new_sitewide` only,
admin-authored pre-localized `title`/`body` text** *(sentence corrected per panel finding CH-1;
see the inventory row below)*.

**iOS cannot mirror this.** A data-only FCM message neither wakes nor displays on iOS in background or
terminated state. Both iOS app delegates (`CustomerAppDelegate.swift`, `PartnerAppDelegate.swift`)
implement only token registration and `willPresent` — which fires **only for alert-type
notifications**, which these are not. There is no `didReceiveRemoteNotification`, no NSE, no
`UIBackgroundModes`. So iOS registers a token, is included in the multicast (`Device.Platform` is
never used to branch), receives the payload — and surfaces **nothing**, silently.

### The false-positive trap (pinned here so it never "closes" the ticket wrongly)

A Firebase-console test push sends a **`notification` (alert) payload**, so it *can* display on iOS
and produce a green "push works" — while every real event stays invisible, because the real events
are **data-only**. A console test validates the token/APNs bridge only, never the display path. The
verification for this ADR is a **real order-status event** on a device in background *and* terminated
state (see docs/architecture/push-notifications.md §0 step 4).

### Where the templates actually live today (the owner-lean premise, corrected)

The owner leaned "per-platform APNs alert, **localized from the existing eventKey templates**." The
investigation finding this ADR must record: **the backend has no eventKey→text templates.** The
`event_key` display templates exist *only* client-side (Android `strings.xml`; the iOS analog is the
per-app `Localizable.xcstrings`, already scaffolded for all 5 platform languages — en/cs/sk/uk/ru
lproj dirs exist in both apps). The backend's only localized template machinery is the SendGrid
email path (`EmailService` + `EmailTemplateTranslation` rows) — a different keyspace, addressed by
email template, not by `event_key`. **One deliberate exception (CH-1):** `promo.new_sitewide`
ships admin-authored, pre-localized literal `title`/`body` *in the data payload* — localized at
fan-out from stored `PreferredLanguageCode` with an `en` fallback
(`SendSitewidePromoFanoutHandler`) — because campaign text has, by design, no client-side template.
That is precedent for literal text on the FCM data channel, **not** for a server-side template
store: there is still no `event_key`→text template source for the 12 templated events. At dispatch
time the Functions consumer knows the user's
**stored** `User.PreferredLanguageCode` (nullable, resettable to null on anonymize) only if it adds a
user read; it never knows the **device** locale. Android localizes by device locale.

So "Option A with literal server-side text" is not "reuse existing templates" — it is *build a new
server-side template store* (13 keys × 5 languages), add a user read to the consumer, and accept
stored-preference (not device-locale) localization, which is *worse* parity with Android, not equal.
The loc-key variant below keeps the owner's actual intent — a server-side APNs alert block, a thin
iOS app, no NSE — while the templates stay exactly where they already are: in the apps.

### The event/args inventory (what the alert block must express)

| event_key | producer(s) | args on the wire | Android body arg |
|---|---|---|---|
| `order.confirmed` | TakeOrder, ConfirmRecurringOrder, HandlePaymentNotification | orderId, orderNumber | orderNumber |
| `order.on_the_way` | NotifyOnTheWay | orderId, orderNumber | orderNumber |
| `order.in_progress` | StartOrder | orderId, orderNumber | orderNumber |
| `order.completed` | CompleteOrder | orderId, orderNumber | orderNumber |
| `order.cancelled` | HandlePaymentNotification (expired), AutoCancelStaleRecurringOrders | orderId, orderNumber | orderNumber |
| `order.refunded` | CancelOrder, AdminCancelOrder, AdminRefundOrder, ResolveDispute | orderId (+disputeId; ResolveDispute sends **no orderNumber**) | orderNumber (`.orEmpty()`) |
| `dispute.reply` | AddDisputeMessage | orderId, disputeId | *(argless)* |
| `recurring.scheduled` | SendRecurringOrderReminders | orderId, orderNumber | orderNumber |
| `loyalty.tier_upgrade` | LoyaltyService | tier (enum name, e.g. `SilverMopper`) | tier → **client-side label lookup** |
| `membership.expiring_soon` / `.cancellation_effective` | SendMembershipLifecycleNotifications | *(none)* | *(argless)* |
| `order.new_available` | NewJobsDigestService (partner) | count | count |
| `promo.new_sitewide` | SendSitewidePromoFanoutHandler | title, body (admin-authored, pre-localized per stored `PreferredLanguageCode`, `en` fallback) | **customer renders the server text verbatim** (special-case branch bypassing `templateFor`); partner drops. *(Row corrected — CH-1.)* |

---

## Options considered

### A1 — APNs alert with literal localized text, resolved server-side. **Rejected.**
Requires what does not exist: a backend `event_key`→text template source × 5 languages, plus a user
read in the dispatch consumer to pick the language. *(CH-1 revision: the promo fan-out proves the
**delivery** half is easy — literal localized text already rides the data channel — but it does not
supply the missing half, the fixed template store; and promo text, authored per campaign, has no
client template to diverge from, while the 12 templated events do.)* Localizes by stored
`PreferredLanguageCode`
(nullable — needs a fallback chain) instead of device locale → a Czech-device user with an English
stored preference gets mismatched text, *diverging* from Android on the same event. Ships literal
status text ("Your cleaner is on the way") through FCM + APNs for every push — no customer PII in the
string itself, but a standing text-on-the-wire channel that every future event template must be
S6-reviewed against, forever. Creates a second localization surface to keep in sync with the four
app-side ones. Keeps the iOS app thin, but at the cost of a new server subsystem whose only job is
re-implementing what the app bundles already do.

### A2 — APNs alert via `title-loc-key`/`loc-key` + `loc-args`. **CHOSEN.**
The dispatcher attaches an `ApnsConfig` whose `aps.alert` carries **keys and allowlisted args**; iOS
resolves them against the app's own `Localizable.strings` (compiled from `Localizable.xcstrings`) at
display time, in the **device locale** — the same model, the same moment, and the same locale source
as Android's `getString(template.bodyRes, orderNumber)`. Zero server-side text, zero new backend
template store, zero user read in the consumer, no change to `IPushDispatcher`'s signature (the
factory derives everything from `eventKey` + `data` it already receives). The alert block is
APNs-scoped, so **Android delivery is bit-identical**. Trade-offs (priced in Consequences): loc-keys
must ship in each app's **main-bundle** catalog before the backend maps an event; version-skew can
display a raw key; `loc-args` are literal (no nested client-side lookups → the loyalty tier body goes
argless); stringsdict plural matching doesn't work on string args (the new-jobs body must be
count-agnostic phrasing).

### B — `mutable-content` + Notification Service Extension. **Rejected (for now).**
An NSE still requires a visible alert payload with `mutable-content: 1` to launch at all — so it is
A2 *plus* a new app-extension target per app (×2), extension provisioning/signing, an app group, and
a 30-second execution budget, to achieve... exactly what A2 already achieves for today's catalog
(key→localized text + args). The NSE earns its place only when a payload must be *transformed* on
device beyond substitution — rich media, decrypt-on-device, feed-row persistence at delivery time.
None of the 12 events needs that. **The seam stays open:** because A2 already puts a real
`aps.alert` on the wire, adding `mutable-content` + an NSE later is additive — no wire-contract
rewrite, no supersede of the Android contract.

### C — silent `content-available: 1` background push, app renders a local notification. **Rejected.**
The closest literal port of the Android model, and it does not work: background pushes are throttled
and best-effort, are **not delivered to terminated (user-killed) apps**, require `UIBackgroundModes:
remote-notification`, and display nothing themselves — failing the ticket's core AC (lock-screen
display in background AND terminated states).

### D — top-level FCM `Notification` field (one cross-platform alert). **Rejected — breaks Android.**
Setting `MulticastMessage.Notification` turns the message into a *notification message* on Android
too: background delivery bypasses `onMessageReceived`, which kills the local-render path, the channel
routing (order-updates vs dispute vs new-jobs), the per-order collapse tags, and the Room-backed
in-app feed insert. The whole point of the per-platform block is that Android must not notice.

---

## Decision

### D1 — The dispatcher attaches an APNs-scoped alert block; a pure factory builds the message

`FcmPushDispatcher.SendAsync` delegates message construction to a new **pure, static**
`FcmMessageFactory.Build(tokens, eventKey, data)` in `Cleansia.Infra.Clients.Fcm` (today the
construction at `FcmPushDispatcher.cs:62–75` is inlined and untestable — the existing tests
(`FcmPushDispatcherDisabledStateTests`) can only reach the disabled path because `FirebaseMessaging`
is live). The factory returns today's message **plus** `ApnsConfig` when — and only when — the event
is in the APNs display map (D2):

```csharp
// Sketch — the wire contract, not the final code.
var message = new MulticastMessage
{
    Tokens = tokens.ToArray(),
    Data   = payload,                                  // BYTE-UNCHANGED: data + event_key
    Android = new AndroidConfig { Priority = Priority.High },   // BYTE-UNCHANGED
    Apns = displayable ? new ApnsConfig
    {
        Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
        Aps = new Aps
        {
            Alert = new ApsAlert
            {
                TitleLocKey = $"push.{eventKey}.title",     // e.g. push.order.confirmed.title
                LocKey      = $"push.{eventKey}.body",
                LocArgs     = orderedAllowlistedArgs,       // D3 — e.g. ["A-1042"]
            },
            Sound    = "default",
            ThreadId = data.GetValueOrDefault("orderId")
                       ?? data.GetValueOrDefault("disputeId")
                       ?? eventKey,                          // grouping parity with Android's tag
        },
    } : null,
};
```

- `ApnsConfig` is attached **unconditionally of token platform** — FCM applies it only on the APNs
  route; Android deliveries ignore it. **No `Device.Platform` branch, no token split, one multicast
  per user as today.**
- `IPushDispatcher.SendAsync`'s signature is **byte-unchanged** (tokens, eventKey, data, ct — the
  factory needs nothing more). The consumer (`SendPushNotificationHandler`) is **byte-untouched**
  (ADR-0023 boundary).
- Out of scope, deliberately: `badge` (no server-side badge ledger exists), `category` (no action
  buttons), `interruption-level`/time-sensitive (needs an entitlement decision), `apns-collapse-id`
  (Mode-A claim-first already makes duplicates rare).

### D2 — The APNs display map is the server-side gate; unknown/unmapped keys stay data-only

The factory holds a static map of the **12 displayable events** (the union of what the two Android
apps render **from fixed client-side templates**): the 6 order-lifecycle keys, `dispute.reply`, `recurring.scheduled`,
`loyalty.tier_upgrade`, both `membership.*` keys, and `order.new_available`. For each: the two
loc-keys (derived `push.<event_key>.title|body`) and the **ordered** loc-args list.
`promo.new_sitewide` is **excluded from the loc-key map by its nature** *(rationale corrected —
CH-1)*: it has no fixed template anywhere — admin-authored at send time; the customer Android app
renders the server-shipped `title`/`body` verbatim — so it *cannot* be a loc-key event. Excluding
it from this ticket leaves a **known customer parity gap**: Android customers see sitewide promos,
iOS customers will not. The additive fix is trivial when the product wants it — a literal
`ApsAlert.Title`/`Body` taken from the `title`/`body` values already on the wire (no template
store, no new exposure: the same bytes already reach every device's `Data` today) — but marketing
display on iOS carries its own decisions (apns-priority 5 vs 10, interruption-level, the Promo
opt-in default) and is deliberately its **own follow-up ticket**, not a side effect of this one.

An event key **not** in the map gets **no** `ApnsConfig` — data-only, invisible on iOS. That is
deliberate **drop-parity** with Android's `templateFor(...) ?: return`. The map is therefore the
forward-compatibility gate: **a new event key may be added to the map only after its loc-keys have
shipped in both iOS apps' catalogs** (the client-first rule Android already lives by via the
"keep in sync with strings.xml" note on `NotificationEventCatalog`).

### D3 — The no-PII stance for the alert (the S6 line, made mechanical)

What may appear in `loc-args` — i.e., in APNs-relayed, lock-screen-visible substitution values — is a
**closed allowlist: `orderNumber` and `count`. Nothing else.**

- **Allowed:** `orderNumber` (a display number the customer already sees everywhere; carries no
  identity), `count` (a digit). Missing value → **empty string**, never null, never drop the alert
  (parity with Android's `.orEmpty()`).
- **Forbidden, permanently:** names, addresses, emails, phone numbers, free text (dispute message
  bodies), auth/tokens, and **internal ids** (`orderId`, `disputeId` ride only in `Data` for
  deep-linking — they never render).
- `tier` is **excluded** even though Android shows it: Android maps the wire enum (`SilverMopper`)
  through a client-side label lookup before substituting; APNs `loc-args` are literal — the raw enum
  identifier would render on the lock screen. The iOS `push.loyalty.tier_upgrade.body` is therefore
  **argless** ("You reached a new loyalty tier!"). If per-tier text is ever wanted on iOS, the
  cheap path is **factory-side per-tier key selection** *(CH-3 corrected the earlier claim that
  this would be the first NSE use case)*: a known `tier` value maps to
  `push.loyalty.tier_upgrade.body.<Tier>`, unknown values fall back to the generic argless key —
  the fallback lives server-side in the factory, so a new enum value can never render a raw key.
  A map-entry + catalog-keys follow-up, **not** an NSE.
- The allowlist is pinned by a unit test (TC-PUSH-APNS-5) so a future producer/mapper edit cannot
  quietly leak a new arg onto the lock screen.

The alert **text** itself never crosses the wire (keys only), so the S6 exposure of this design is
*strictly narrower* than what Android already displays on its own lock screen today.

### D4 — The iOS receive/tap wiring this contract implies (the follow-up iOS scope)

1. **Loc-keys live in each app target's own `Localizable.xcstrings`** (`CleansiaCustomer/Resources`,
   `CleansiaPartner/Resources`) — **not** in `CleansiaCore`: APNs resolves `loc-key` against the
   **main bundle's** `Localizable.strings` table only; an SPM package's resource bundle is invisible
   to it. This is a trap worth a pinned rule. Both apps ship the **full 12-key set × 5 languages**
   (24 strings per language per app) so no in-catalog event can ever render a raw key regardless of
   which app a token belongs to.
2. **Display needs no new iOS code.** Background/terminated: the OS renders the alert. Foreground:
   both delegates' existing `willPresent → [.banner, .sound]` shows it; there is no local-notification
   path on iOS, so no duplicate-display risk.
3. **Tap → deep-link:** partner is already wired (`didReceive response` →
   `PartnerNotificationDeepLink.resolve(userInfo)` → `onTap` → `PushNavigationModel`; the FCM `Data`
   keys — `event_key`, `orderId` — arrive in `userInfo` alongside `aps`, so the existing resolver
   works unmodified). **The customer app has neither a `didReceive` handler nor a resolver** — it
   gets the mirrored trio (delegate `didReceive` + `CustomerNotificationDeepLink` + navigation seam),
   routing order events → order detail, `dispute.reply` → dispute thread.
4. **The Android in-app feed has no iOS counterpart** (partner Android persists a Room row per push).
   Out of scope here; parity feed = own ticket if the product wants it.

### D5 — Sequencing and the deployment order

- **T-0403 first (depends_on already declared):** until the iOS apps register **FCM registration
  tokens** (not raw APNs hex) and the APNs .p8 is uploaded to Firebase, no iOS push can deliver —
  display work is unverifiable. The backend factory change is **inert for Android** and safe to ship
  in any order *relative to Android*; the *verification* of AC2 is gated on T-0403 + provisioning
  (push-notifications runbook §0).
- **Day-one catalog gate (added per CH-2):** both iOS AppDelegates already carry complete FCM-token
  registration wiring (`Messaging` delegate + `PushTokenForwarder` + the `requestFcmToken` pull),
  so "backend in any order" is only *Android*-safe. **Every iOS build that registers FCM tokens
  must carry the full 12-event loc-key catalog, and the Functions deployment that activates the
  APNs display map must not precede the first catalog-carrying public release of both apps** —
  otherwise every mapped event renders its raw `push.*` key as the lock-screen body on those
  builds (iOS displays a loc-key verbatim when it is absent from the main bundle's table).
  Internal/TestFlight builds may transiently violate this; public releases must not.
- **Client-first for new keys forever after:** loc-keys in both app catalogs → then the backend map
  entry → then the producer (D2's gate).

---

## Consequences

**Cheaper / safer:**
- iOS display works for all 12 catalog events with **no new backend subsystem** (no template store,
  no user read in the consumer, no `IPushDispatcher` signature change) and **no new iOS target**.
- Localization stays in exactly one place per platform (the app bundle), device-locale-correct on
  both platforms, in all 5 languages.
- **Android is byte-unchanged by construction** (APNs-scoped block) and **pinned by test** (the
  `Data` dictionary and `AndroidConfig` are asserted bit-identical to today's shape).
- The factory extraction makes the FCM wire shape unit-testable for the first time — every future
  wire-contract change lands with a pinned test instead of a device-only verification.
- The NSE door stays open, additively, for the day an event needs on-device transformation.

**More expensive / accepted residuals:**
- **Version skew can render a raw loc-key** (e.g. `push.order.confirmed.body`) if a backend map entry
  ever ships before the app catalogs carry the key, or a user runs a pre-key app version when a *new*
  event launches. Bounded by D2's client-first gate + D5's day-one catalog gate (today's 12 events
  therefore never skew on public builds — that is an enforced invariant, not an assumption; CH-2).
  Ugly-but-rare is accepted over Option D's break-Android and Option A1's text-on-the-wire.
- **No plural rules for `order.new_available`:** `loc-args` are strings, and stringsdict plural
  matching needs numeric specifiers — the body phrasing must be count-agnostic ("New jobs near you:
  %@"). Cosmetic, accepted.
- **`order.refunded` via ResolveDispute has no `orderNumber` arg** → the body substitutes empty (the
  same today on Android: "…order  has been…"). Pre-existing producer gap, *visible* now on two
  platforms; fix is a one-line producer arg addition — flagged for the backend ticket as a courtesy,
  not required by this ADR.
- The loyalty body loses its tier name on iOS (argless) — a real product downgrade vs Android's
  "You reached Silver Mopper!" (CH-3). Accepted for day one; the named upgrade path is factory-side
  per-tier key selection (D3) — a small map-entry + catalog-keys follow-up, **not** an NSE.
- **iOS customers will not receive sitewide promo pushes** while Android customers do (D2's
  exclusion; CH-1) — an acknowledged parity gap deferred to its own marketing-display ticket, with
  a trivially additive path (literal alert from the on-wire `title`/`body`).
- Two more localization tables to maintain (each iOS app's push keys) — but they were owed to the
  platform anyway; the alternative was a *fifth*, server-side one.

**No migration, no NSwag, no queue/wire-envelope change, no consumer change.**

---

## How a reviewer verifies compliance

**Mechanical:**
1. `FcmPushDispatcher.SendAsync` builds its `MulticastMessage` via the pure `FcmMessageFactory`;
   grep confirms no other construction site. `IPushDispatcher.cs` is byte-unchanged;
   `SendPushNotificationHandler.cs` is byte-unchanged (ADR-0023 §verify #4 still holds).
2. The factory's map contains exactly the 12 D2 events; `promo.new_sitewide` absent; loc-keys follow
   `push.<event_key>.title|body`; the loc-args allowlist is `{orderNumber, count}` and `tier`,
   `orderId`, `disputeId` never appear in any `LocArgs` list.
3. `Data` still contains `event_key` + the producer args, nothing more or less than today;
   `AndroidConfig` still `Priority.High` only; no `Notification` field anywhere; no
   `ContentAvailable`, no `MutableContent`.
4. iOS: both apps' `Localizable.xcstrings` (app targets, **not** CleansiaCore) contain all 24 keys ×
   5 languages; `Info.plist` gains **no** `UIBackgroundModes`; no NSE target exists.
5. The docs runbook's console-test warning (§0 step 4 / §6) still stands — the ticket's AC2
   evidence is a real event on a device, background AND terminated.

**Test contract (backend — red first, `TC-PUSH-APNS-*`, on the factory):**
6. **TC-PUSH-APNS-0 (per-event alert):** for each of the 12 mapped events with representative args,
   `Build(...)` yields `Apns.Aps.Alert` with the derived `TitleLocKey`/`LocKey` and the exact ordered
   `LocArgs` (e.g. `order.confirmed` + `orderNumber:"A-1042"` → `LocArgs == ["A-1042"]`;
   `order.new_available` + `count:"3"` → `["3"]`; `membership.*`, `dispute.reply`,
   `loyalty.tier_upgrade` → empty `LocArgs`).
7. **TC-PUSH-APNS-1 (Android regression pin):** for every event, the built message's `Data` is
   dictionary-equal to `{producer args} + event_key` and `Android.Priority == High` — asserted
   against a frozen expectation of **today's** shape.
8. **TC-PUSH-APNS-2 (drop parity):** an unmapped key (`promo.new_sitewide`, and an arbitrary unknown)
   → `Apns == null`, `Data` still intact.
9. **TC-PUSH-APNS-3 (missing-arg tolerance):** `order.refunded` without `orderNumber` → alert present,
   `LocArgs == [""]` — never null, never dropped.
10. **TC-PUSH-APNS-4 (aps furniture):** `Sound == "default"`; `ThreadId` = orderId when present, else
    disputeId, else eventKey; header `apns-priority == "10"`.
11. **TC-PUSH-APNS-5 (S6 allowlist pin):** an assertion over the map itself — the union of all
    mapped arg names ⊆ `{orderNumber, count}`. This test is the tripwire for D3.

**Test contract (iOS):**
12. `PartnerNotificationDeepLinkTests` extended: `userInfo` containing an `aps` alert dict **plus**
    the data keys still resolves (i.e., the new payload shape doesn't confuse the resolver).
13. New `CustomerNotificationDeepLink` resolver tests mirroring the partner suite (order events →
    `.order(orderId:)`, `dispute.reply` → dispute destination, unknown → nil).
14. QA device matrix (manual — display is not unit-testable): each of the 5 ticket events displays
    on lock screen in background AND terminated, in ≥2 locales; tap lands on the order; foreground
    shows the banner once.

---

## Roles affected

- **NEW — `agents/knowledge/roles/fcm-message-factory.md`** (created with this ADR): pure translator
  from `(eventKey, data)` to the per-platform FCM wire shape; owns the display map + arg allowlist;
  does NOT know tokens' platforms, user language, tenancy, or delivery outcomes.
- `agents/architecture/decisions/push-notifications.md` (living doc, created with this ADR) — the
  current shape: per-platform contract table, the display map, the client-first rule, the open NSE
  seam.
- Unchanged: `idempotency-guard` (Mode A untouched), the consumer, the queue contract.

---

## Challenges pre-answered (author's opening defense — panel to attack)

| # | Expected challenge | Author's position |
|---|---|---|
| PA-1 | "The owner leaned A1 (literal server-side text) — you picked something else." | **Refined, not overturned.** The owner's lean was premised on "the existing eventKey templates" living server-side; they do not (Context §3 — client `strings.xml`/xcstrings only; the backend's only template machinery is the SendGrid email path, keyed differently). A2 delivers every property the owner wanted — server-side APNs alert block, thin iOS, no NSE — and keeps localization where the templates actually are, with device-locale parity with Android. If the owner wants literal text *despite* the corrected premise, that is an owner escalation, not a panel matter. |
| PA-2 | "Raw loc-key on the lock screen under version skew is user-visible garbage." | Bounded and gated: the 12 current events ship in both apps' catalogs in the same batch (skew impossible for the existing catalog); *new* events are gated client-first by the D2 map. The residual is one ugly string for users who skip app updates across a *new-feature* launch — priced against A1's permanent text-on-the-wire channel and D's breaking Android. |
| PA-3 | "Why not the NSE and keep literally zero display metadata on the wire?" | The NSE *cannot* run without a visible alert + `mutable-content` — it does not remove the alert from the wire, it adds two app-extension targets, signing, and an app group on top of it. For key+args substitution it is A2 with extra failure modes. The seam stays open (Option B) for on-device transformation needs; today none exists. |
| PA-4 | "`orderNumber` on the lock screen violates the no-PII stance." | The stance (message doc-comment, S6) is *no customer PII*; the order display number identifies no person and is already shown on Android lock screens today via local render. The ADR narrows, names, and test-pins the allowlist (D3, TC-PUSH-APNS-5) — stricter than the status quo, not looser. |
| PA-5 | "Attach `ApnsConfig` only when the fan-out actually contains iOS tokens." | Needless coupling: the dispatcher would need `Device.Platform` knowledge (a new parameter or a repo read), splitting the multicast for zero wire benefit — FCM already applies the APNs block only on the APNs route. The factory stays pure and platform-blind. |

## Challenge

*(Architect panel, challenger mode — every point below was verified against source, not the ADR's
own citations.)*

### CH-1 — BLOCKING (factual defect): the promo inventory row is false, and D2's "drop-parity" rationale collapses with it

The ADR asserts (Context table + D2): *"`promo.new_sitewide` — rendered by NEITHER Android app
(dropped)"* and *"the only args on the wire today are `orderId`, `orderNumber`, `disputeId`,
`count`, `tier`"*. **Both claims are contradicted by code:**

- `customer-app/.../CleansiaFirebaseMessagingService.kt:79–90` **special-cases
  `promo.new_sitewide`** and renders server-authored `title`/`body` straight from the data payload
  (bypassing `templateFor` entirely; only the channel category is resolved locally).
- `SendSitewidePromoFanoutHandler.cs:160–179` enqueues `Args = { title, body }` — **admin-authored,
  pre-localized literal text** — resolved per recipient from stored `User.PreferredLanguageCode`
  with an `en` fallback (`ResolveLocale`, :220–226).

Three consequences the ADR must answer:

1. **Literal localized text already rides the FCM data channel today**, and the
   stored-preference-with-fallback locale-resolution machinery the ADR says A1 would have to
   *build* already exists in this very handler. A1's rejection ("requires what does not exist",
   "a standing text-on-the-wire channel… forever" as a *new* cost) is argued against a strawman
   codebase. Re-defend A1 against the real one.
2. **D2's exclusion of promo is justified by a false premise.** The truthful framing is not
   drop-parity — it is a **customer-facing parity gap**: Android customers receive admin marketing
   pushes; iOS customers will not. And the fix is conspicuously cheap: the localized text is
   *already in `data`* — a literal `ApsAlert.Title`/`Body` branch in the factory, no template
   store, no new wire exposure (the same bytes already reach every device today). Why is that
   dismissed without being priced?
3. The Context sentence enumerating "the only args on the wire" is what a future reviewer will
   trust when auditing S6. It must be corrected or it becomes folklore-grade wrong.

### CH-2 — BLOCKING (sequencing hole): "the backend factory change is safe to ship in any order" is only Android-safe

D5 claims the factory change is *"inert for Android and safe to ship in any order"* and PA-2 claims
skew is *"impossible for the existing catalog."* Neither holds for iOS: **both iOS AppDelegates
already carry complete FCM-token registration wiring** (`CustomerAppDelegate.swift:54–63` /
`PartnerAppDelegate.swift:55–64` — `Messaging` delegate + `PushTokenForwarder`, plus the
`requestFcmToken` pull). Any distributed iOS build that registers a token *before* its
`Localizable.xcstrings` carries the push keys will — the moment the backend map goes live — render
the **raw `push.order.confirmed.body` string as the lock-screen body for all 12 events**, because
iOS displays the loc-key verbatim when it is absent from the main bundle's table. That is exactly
the failure PA-2 declares impossible, on day one, for every event. D5 needs an explicit day-one
gate (catalog must ship in every token-registering release, and the map's activation must not
precede it), not just the "client-first for *new* keys" rule.

### CH-3 — STANDS-UNLESS-REVISED (mispriced alternative): "per-tier text on iOS is the first real NSE use case" is wrong

D3 downgrades the loyalty body to argless and records the upgrade path as an NSE. A strictly
cheaper path exists that the ADR never considers: **factory-side per-tier key selection** — map a
known `tier` value to `push.loyalty.tier_upgrade.body.<Tier>` and fall back to the generic argless
key for unknown values. The fallback lives *server-side in the factory*, so a future enum value can
never render a raw key (unlike client-side skew); nothing crosses the wire but a key; no NSE, no
app-extension targets. The argless day-one choice may still be right (translation surface,
one more map rule), but the ADR (a) records a false "only alternative" and (b) hides the product
cost — Android customers see "You reached Silver Mopper!", iOS customers see a generic sentence.
Price it honestly and name the real upgrade path.

### CH-4 — RULING REQUESTED (scope): the customer deep-link trio

The ticket's QA matrix (verification #14) includes "tap lands on the order," and the partner side
is already wired (`PartnerAppDelegate.swift:101–112` → `PartnerNotificationDeepLink.resolve`). The
customer app has **no** `didReceive` handler — a tap merely opens the app. D4.3 asserts the
mirrored trio is in scope; the panel should rule explicitly, because it roughly doubles the
iOS-lane surface and someone will otherwise try to split it out and ship display as a
half-experience.

### CH-5 — the PA-1 escalation question, pressed once with CH-1 in hand

Given CH-1, A1 is less alien than painted: "extend the promo mechanism to 12 events" is a
one-sentence pitch an owner might reasonably make. The author must show that even against the
*real* codebase, A2 preserves every property of the owner's lean — or this goes to
`questions/open.md`.

### Checked and found sound (silence is not assent — named explicitly)

- **PA-4 (orderNumber):** `SendPushNotificationMessage` doc-comment pins "NEVER include PII
  (customer name, address)"; `orderNumber` identifies no person and is already substituted into
  Android lock-screen bodies today (customer service `formatBody:194–219`). No challenge.
- **PA-5 (platform-blind `ApnsConfig`):** `SendPushNotificationHandler.cs:121–140` fans out one
  mixed token list; `Device.Platform` is never consulted; FCM applies `ApnsConfig` only on the
  APNs route. Splitting would push platform knowledge through the `IPushDispatcher` seam for zero
  wire benefit. No challenge.
- **PA-3 (NSE):** confirmed — an NSE requires a visible alert + `mutable-content: 1`; it is A2 plus
  failure modes. No challenge (but see CH-3 for the loyalty-specific misuse of this argument).
- **ThreadId parity:** `orderId ?? disputeId ?? eventKey` matches the customer tag chain
  (customer service `:122`) and subsumes the partner chain (`:123`). Sound.
- **ADR-0023 boundary:** the factory lives wholly inside `FcmPushDispatcher`; the consumer's
  claim-first ordering and `IPushDispatcher` signature are untouched. Sound.
- **Foreground path:** both delegates' `willPresent → [.banner, .sound]` will now fire (alert-type
  payload); no local-notification path exists on iOS, so no duplicate display. Sound.
- **Main-bundle trap (D4.1):** real — a third `Localizable.xcstrings` exists in *CleansiaCore*
  (`CleansiaCore/Sources/CleansiaCore/Resources/`), which APNs cannot see; the pinned "app targets
  only" rule is necessary, not pedantic.
- **Citation nit:** the runbook has no "§0.4" — the console-test warning lives at §0 step 4 and §6
  of `docs/architecture/push-notifications.md`. Fix the two citations.

## Defense

### CH-1 — CONCEDE + REVISE (facts corrected; the *decision* survives on corrected grounds)

Conceded in full on the facts: the customer Android app renders `promo.new_sitewide` from
server-shipped `title`/`body`, and literal pre-localized text already rides the FCM data channel
for that one event. The Context inventory row, the "only args on the wire" sentence, and the
"templates live client-side" section are **revised in place** (marked CH-1).

What does *not* change, and why:

- **Promo stays out of the loc-key map by its nature, not by the dead rationale.** It has no fixed
  template anywhere — it *cannot* be a loc-key event. The exclusion is re-justified honestly in D2
  as an acknowledged **customer parity gap** with the trivially additive fix named (literal
  `ApsAlert.Title`/`Body` from the on-wire values). It stays out of *this ticket's* scope because
  marketing display on iOS carries its own product decisions (apns-priority 5 vs 10,
  interruption-level, the Promo opt-in default) — a small, separate decision, flagged as a
  follow-up ticket for the PM, not a silent side effect of a transactional-display ADR.
- **A1's rejection is re-argued against the real codebase** (revision in Options A1): the promo
  precedent proves the *delivery* half of A1 is easy — it does not supply the missing half (a
  fixed `event_key`→text template store × 5 languages). And it clarifies the locale point rather
  than weakening it: promo text is authored per campaign and has **no client template to diverge
  from** — stored-preference localization is the only option and divergence-with-Android is
  impossible by construction. The 12 templated events are the opposite case: Android renders them
  in the *device* locale, so A1 would create visible same-event cross-platform divergence. The
  strawman is gone; the conclusion holds.

### CH-2 — CONCEDE + REVISE

Correct, and worse than the challenge states: the token wiring is already merged in both
delegates, so the ordering hazard is live, not hypothetical. D5 gains the **day-one catalog
gate**: every iOS build that registers FCM tokens must carry the full 12-event loc-key catalog,
and activating the APNs display map in the Functions deployment must not precede the first
catalog-carrying public release of both apps. PA-2's "impossible for the existing catalog" is
therefore true only *because of this gate* — the gate is now a stated invariant, not an
assumption.

### CH-3 — CONCEDE the false alternative + REVISE; REBUT changing the day-one shape

Conceded: per-tier key selection with a factory-side fallback achieves per-tier text with no NSE —
the D3 sentence claiming an NSE would be needed is replaced, and the Consequences bullet now
prices the product downgrade explicitly. Rebuttal on day one: shipping it now adds 4 keys × 5
languages × 2 apps of translation surface for one nicety and introduces a new map rule
(value-derived key selection) that deserves its own moment — the upgrade is purely additive later
(map entry + catalog keys, client-first gated). Argless ships day one.

### CH-4 — position: IN SCOPE; lead to ratify

D4.3 already declares the trio in scope, and the AC (tap → order) is unobservable without it.
It must land in the same iOS release train as the catalog (per CH-2's gate there is exactly one
such release) — shipping display without tap handling is a half-experience on an Apple-review
surface (ADR-0016).

### CH-5 — REBUT

Even against the corrected codebase, A2 preserves *every* property of the owner's lean: a
server-attached per-platform APNs alert block (the owner's mechanism), localization from the
existing eventKey templates (the owner's words — the templates exist only in the apps, and A2 uses
exactly them), a thin iOS app, no NSE. "Extend the promo mechanism to 12 events" is precisely A1
with its costs now measured against real code: a new fixed-template store the promo path never
needed, plus same-event locale divergence from Android that the promo path structurally cannot
exhibit. No property the owner asked for is lost; the premise correction is recorded immutably in
Context; if the owner reads this trail and still wants literal text, that is a superseding ADR —
not an open question blocking this one. No escalation.

## Verdict

*(Architect panel, lead mode — 2026-07-15.)* **RATIFIED AS AMENDED → status `accepted`. Consensus
reached; no owner escalation.**

Disposition of every challenge:

| Challenge | Ruling |
|---|---|
| CH-1 (promo facts / D2 rationale) | **CONCEDED and REVISED — resolved.** The decision (A2 loc-keys for the 12 templated events) survives on corrected grounds: promo *cannot* be a loc-key event, so its exclusion from the map is structural; its exclusion from the *ticket* is a scope choice now honestly recorded as a customer parity gap with the additive fix named. Amendments applied: Context args sentence, Context §3 promo-precedent paragraph, inventory row, Options A1 re-argument, D2 rationale, new Consequences bullet. **PM action: file the follow-up ticket "promo.new_sitewide display on iOS (literal ApsAlert from on-wire title/body)"** — a product/marketing decision (priority, interruption-level, opt-in default), not part of T-0404. |
| CH-2 (day-one skew / "any order" claim) | **CONCEDED and REVISED — resolved.** D5 gains the day-one catalog gate: every token-registering iOS build carries the full 12-event catalog; the Functions deploy that activates the display map must not precede the first catalog-carrying public release of both apps. PA-2's "impossible" is now an enforced invariant, not an assumption. |
| CH-3 (loyalty NSE mispricing) | **CONCEDED on the alternative, REBUTTED on day-one shape — resolved.** D3 now names factory-side per-tier key selection (with server-side fallback) as the real upgrade path; the argless day-one body stands, with the product cost priced in Consequences. |
| CH-4 (customer deep-link trio scope) | **RULED: IN SCOPE for T-0404's iOS lane.** The AC (tap → destination) is unobservable without it, and per the CH-2 gate there is exactly one release train it can ride: the same iOS release that ships the loc-key catalogs. Display-without-tap does not ship. |
| CH-5 (PA-1 / escalation) | **DEFENDED — resolved, no escalation.** A2 preserves every property of the owner's stated lean (server-attached per-platform alert block, localization from the existing eventKey templates — which live only in the apps — thin iOS, no NSE); the premise correction is recorded immutably in Context. If the owner wants literal server text despite the corrected premise, the path is a superseding ADR, not a block on this one. |
| PA-3, PA-4, PA-5, ThreadId parity, ADR-0023 boundary, foreground path, main-bundle trap | **Checked by the challenger, found sound — no challenge raised.** |

Zero blocking challenges remain. The ADR is **immutable from this point** — changes require a
superseding ADR. Living documentation created in the same step (deliberation protocol §parallel
documentation): `agents/architecture/decisions/push-notifications.md` (current shape + trade-off
space) and `agents/knowledge/roles/fcm-message-factory.md` (CRC card).

**Ratified implementation instruction (backend lane):** extract the pure static
`FcmMessageFactory.Build(tokens, eventKey, data)` in `Cleansia.Infra.Clients.Fcm`;
`FcmPushDispatcher.SendAsync` becomes its only caller (replacing the inline construction at
`FcmPushDispatcher.cs:62–75`); implement D1's wire shape gated by D2's 12-event map with D3's
`{orderNumber, count}` allowlist; land tests TC-PUSH-APNS-0…5 red-first. No change to
`IPushDispatcher`, `SendPushNotificationHandler`, producers, or the queue contract.

**Ratified implementation instruction (iOS lane):** all 24 loc-keys × 5 languages in **both** app
targets' `Localizable.xcstrings` (never CleansiaCore's); the customer deep-link trio
(`didReceive` + `CustomerNotificationDeepLink` + navigation seam) mirroring the partner shape;
extend `PartnerNotificationDeepLinkTests` for the alert-carrying `userInfo` shape; same release
train as (or earlier than) the backend map activation per D5's gate.

---

## Amendment A1 (2026-07-18, T-0412) — `promo.new_sitewide` literal pass-through

The base ADR structurally excluded `promo.new_sitewide` from the D2 display map (no fixed
template — the admin authors title/body per campaign), which left iOS customers with **no promo
push at all** while Android renders the server-authored text. This amendment adds exactly one
event-scoped exception, decided by the T-0412 panel (author → challenger → lead, `## Decision
(panel)` in the ticket).

**Amended:** `FcmMessageFactory.BuildApns` gains a branch keyed on
`NotificationEventCatalog.PromoNewSitewide` **and that key only** that emits a LITERAL
`aps.alert` (`Title`/`Body` from the wire `title`/`body`, blank/missing → no alert = Android
drop-parity), at `apns-priority: 5` + `interruption-level: passive` + **no sound** (the iOS analog
of Android's `IMPORTANCE_LOW` promo channel; the correct App-Review posture for marketing). The
D2 12-event loc-key map, the D3 `{orderNumber, count}` allowlist, and every other verification are
**untouched** — this is a supersede-IN-PART of the single `TC-PUSH-APNS-2` assertion that promo
ships data-only.

**Explicitly NOT adopted (the challenger's rejected generalization):** a generic "any unmapped
event carrying title/body displays literally" fallback. That would convert the display map from a
gate into a suggestion — any future producer naming its args `title`/`body` would silently surface
on iOS with no client-first or S6 review. A whole-catalog tripwire test pins that the literal
branch fires for `promo.new_sitewide` and nothing else.

**Accepted limitation:** the literal text is single-language per send (the fanout picks the
recipient's stored `PreferredLanguageCode`, en fallback) — identical to Android, and the admin
form enforces all 5 locales non-empty, so cross-platform divergence is zero. The eventual
device-locale fix (all-locales-in-payload + client-side selection) is the first genuine use case
for the `mutable-content`/NSE seam the base ADR left open.

**Promo opt-in:** unchanged and already correct — the fanout targets only `Where(p => p.Promo)`,
domain default `false`, both apps' toggles default-off. Nothing built here.
