# Push Notifications — Living Decision Doc

> Living companion to the immutable ADRs (`../../backlog/adr/`). Current shape of the push pipeline
> and its display contract. Canonical dev-facing runbook: `docs/architecture/push-notifications.md`.
> Governing ADRs: **ADR-0002** (dispatch/outbox contract, D2.1 keys, consumer classification),
> **ADR-0010/0023** (idempotency — push consumer is **Mode A**, claim-before-act),
> **ADR-0025** (**accepted** 2026-07-15, panel consensus — per-platform APNs alert with loc-keys).

## Current shape (as of ADR-0025, accepted 2026-07-15)

```
producer handler ──enqueue──▶ notifications-dispatch ──▶ SendPushNotificationHandler (Functions)
  (post-commit,                (QueueEnvelope +            claim-first (Mode A) → prefs gate →
   outbox/pending)              deterministic key)         device fan-out → IPushDispatcher
                                                                │
                                                        FcmPushDispatcher
                                                                │  builds via FcmMessageFactory (ADR-0025)
                                                                ▼
                                              ┌────────── one MulticastMessage ──────────┐
                                              │ Data: { event_key, args… }   (both OS)   │
                                              │ AndroidConfig: Priority.High (Android)   │
                                              │ ApnsConfig: aps.alert loc-keys (iOS only)│
                                              └──────────────────────────────────────────┘
```

## The per-platform display contract (ADR-0025)

| | Android | iOS |
|---|---|---|
| Wire | data-only (`event_key` + args) — **byte-unchanged since ADR-0002** | same data **plus** APNs-scoped `aps.alert` |
| Who renders | `CleansiaFirebaseMessagingService.onMessageReceived` → local notification + Room feed row | the OS, from `title-loc-key`/`loc-key`/`loc-args` |
| Templates live | app `strings.xml` (customer 11 keys, partner 6) | each app target's **main-bundle** `Localizable.xcstrings` (`push.<event_key>.title|body`, ×5 languages) — **not** CleansiaCore (APNs can't see SPM resource bundles) |
| Locale source | device locale | device locale (OS-resolved) |
| Unknown event key | silently dropped (`templateFor == null`) | no `ApnsConfig` attached (server-side map is the gate) → invisible: **drop parity** |
| Tap → deep link | `NotificationDeepLink.encode` → MainActivity intent | `didReceive response` → `{Partner,Customer}NotificationDeepLink.resolve(userInfo)` → `PushNavigationModel` (customer wiring is follow-up scope) |
| Foreground | local notification | `willPresent → [.banner, .sound]` (already wired both apps) |
| In-app feed | partner: Room-backed | none (out of scope; own ticket if wanted) |

**Displayable event map (12):** `order.confirmed|on_the_way|in_progress|completed|cancelled|refunded`,
`dispute.reply`, `recurring.scheduled`, `loyalty.tier_upgrade`, `membership.expiring_soon`,
`membership.cancellation_effective`, `order.new_available`. **Excluded:** `promo.new_sitewide` —
**it cannot be a loc-key event** (no fixed template anywhere: admin-authored at send time; the
customer Android app renders the server-shipped pre-localized `title`/`body` from the data payload
verbatim — `SendSitewidePromoFanoutHandler` localizes at fan-out from stored
`PreferredLanguageCode`, `en` fallback). **Known parity gap: iOS customers get no promo pushes**
while Android customers do; the additive fix (literal `ApsAlert.Title/Body` from the on-wire
values) is its own follow-up ticket (priority / interruption-level / opt-in decisions) — flagged to
the PM in the ADR-0025 verdict (CH-1).

**Lock-screen/no-PII stance (ADR-0025 D3):** `loc-args` allowlist = `{orderNumber, count}` — test-pinned.
`orderId`/`disputeId` ride only in `Data` for deep-linking; `tier` is argless on iOS (Android does a
client-side label lookup APNs can't — the named upgrade path is **factory-side per-tier key
selection** with a server-side fallback to the generic key, *not* an NSE; ADR-0025 CH-3);
names/addresses/emails/free-text: never, anywhere.

## Standing rules

1. **Client-first for new events:** loc-keys in BOTH iOS app catalogs (and Android strings) ship
   *before* the backend display-map entry, which ships *before* the producer. The server map is the
   gate that makes skew bounded.
1a. **Day-one catalog gate (ADR-0025 D5, panel CH-2):** every iOS build that registers FCM tokens
   must carry the full 12-event loc-key catalog — the token wiring is *already merged* in both
   AppDelegates — and the Functions deploy that activates the APNs display map must not precede the
   first catalog-carrying **public** release of both apps. Otherwise every mapped event renders its
   raw `push.*` key as the lock-screen body (iOS shows a loc-key verbatim when it's absent from the
   main bundle's table).
2. **Never set the top-level FCM `Notification` field** — it flips Android to notification-messages,
   bypassing `onMessageReceived` (breaks local render, channels, collapse tags, the feed).
3. **The Firebase-console test push is a false positive for iOS** — it sends an alert payload; the
   real events are data-only+apns. Verify with a real order-status event, background AND terminated.
4. **The push consumer stays Mode A** (claim-before-act, ADR-0023) — display changes never touch
   claim ordering.
5. `IPushDispatcher.SendAsync(tokens, eventKey, data, ct)` is the seam; a second provider (e.g. raw
   APNs, another vendor) implements it and gets the factory's contract for free.

## Trade-off space (why the current shape)

- **Literal server-side text (rejected):** no backend `event_key`→text template source exists
  (templates are client-side; the email path's `EmailTemplateTranslation` is a different keyspace);
  would localize by stored `PreferredLanguageCode` (nullable) instead of device locale — visible
  same-event divergence from Android. The promo path is NOT a counterexample (CH-1): campaign text
  is authored per send and has no client template to diverge from — precedent for literal text on
  the data channel, not for a template store.
- **NSE (deferred seam):** still requires a visible alert + `mutable-content` to run; adds 2 app
  extension targets/signing/app-group for nothing today. Becomes worth it the day an event needs
  on-device transformation (media, decrypt-on-device, feed persistence at delivery — **not**
  per-tier labels, which are factory-side key selection; CH-3). Additive later — the alert is
  already on the wire.
- **`content-available` silent push (rejected):** throttled, not delivered to user-killed apps,
  displays nothing — fails the lock-screen AC.

## Known residuals / open items

- Version skew can show a raw `push.*` loc-key for *new* events on old app builds (bounded by rule 1).
- `order.new_available` body must be count-agnostic phrasing (loc-args are strings; no stringsdict
  plural matching).
- `order.refunded` via `ResolveDispute` sends no `orderNumber` → empty substitution on BOTH platforms
  (pre-existing producer gap; one-line fix, flagged in the T-0404 backend ticket).
- Sequencing: iOS display is unverifiable until **T-0403** (FCM registration tokens on iOS) +
  provisioning (runbook §0) land; the backend factory change is Android-inert, but activating the
  APNs map is gated by standing rule 1a (day-one catalog gate) — "any order" is only Android-safe.
- Customer iOS tap→deep-link trio (`didReceive` + `CustomerNotificationDeepLink` + navigation
  seam): **ruled IN SCOPE for T-0404's iOS lane** (ADR-0025 verdict, CH-4) — same release train as
  the catalogs; display-without-tap does not ship.
- Loyalty body is argless on iOS (no tier name) — accepted product downgrade; upgrade = per-tier
  key selection follow-up (CH-3).
- Promo parity gap on iOS (see the display-map note above) — follow-up ticket for the PM (CH-1).

## History

- 2026-07-15 — ADR-0025 **accepted** (panel consensus). Challenges upheld and folded in: CH-1
  (promo inventory corrected — customer Android renders server-shipped literal text; exclusion
  re-justified as a parity gap + follow-up ticket), CH-2 (day-one catalog gate added to D5), CH-3
  (per-tier loyalty text = factory key selection, not an NSE), CH-4 (customer deep-link trio ruled
  in scope). PA-1/3/4/5 defended; no owner escalation.
- 2026-07-15 — ADR-0025 authored (proposed): per-platform APNs alert via loc-keys; panel convened.
- 2026-07-14 — T-0404 filed from the push-chain adversarial audit; T-0403 batch fixed iOS FCM-token
  registration + re-registration collision.
- Earlier: ADR-0002 established the dispatch contract (data-only, deterministic keys); ADR-0010/0023
  the durable idempotency and the Mode A/B boundary (push = Mode A).
