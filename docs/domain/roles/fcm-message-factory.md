# Role — `FcmMessageFactory` (CRC card)

> Introduced by **ADR-0025** (iOS push display via per-platform APNs alert with loc-keys). A pure,
> static translator inside `Cleansia.Infra.Clients.Fcm`, extracted from the previously-inlined
> message construction in `FcmPushDispatcher.SendAsync` so the FCM wire shape is unit-testable.
> Status: **binding** — ADR-0025 accepted 2026-07-15 (panel consensus; see the ADR's Verdict).

## Responsibility (one sentence)
Translate `(deviceTokens, eventKey, data)` into the exact per-platform FCM wire shape — the
byte-stable data-only payload + `AndroidConfig` for Android, plus an APNs-scoped
`aps.alert` (`title-loc-key`/`loc-key`/allowlisted ordered `loc-args`, sound, thread-id,
`mutable-content: 1`) **iff** the event is in its display map — deterministically and without I/O.

## Collaborators
- `FcmPushDispatcher` — its only caller; hands the factory's `MulticastMessage` to
  `FirebaseMessaging.SendEachForMulticastAsync` and owns everything after the wire (init, failure
  classification, dead-token prune signaling).
- The **APNs display map** it owns internally: the 32 displayable event keys (ADR-0025 D2 — union
  of what the two Android apps render *from fixed client-side templates*; `promo.new_sitewide`
  excluded **by nature**: it is a literal-text event with no fixed template anywhere — panel
  finding CH-1) → derived loc-keys (`push.<event_key>.title|body`) + ordered arg names.
- The **loc-args allowlist** it enforces: `{orderNumber, count, amount}` only (ADR-0025 D3, widened by
  one slot in Amendment A2 on owner ruling 2026-09-13; pinned by TC-PUSH-APNS-5). `amount` rides the
  **three no-show outcome keys only** — `order.no_cleaner_refunded`, `order.no_cleaner_refund_pending`
  and `order.no_cleaner_nothing_charged`, each `["orderNumber", "amount"]` — and is a server-formatted
  money figure with its own currency's symbol (`MoneyText.Format`: "250 Kč"), never a name, an id or
  free text.

## Does NOT know
- **Which platform a token belongs to** — `ApnsConfig` is attached platform-blind; FCM routes it.
  Never accept a `Device.Platform` parameter; that coupling was explicitly rejected (ADR-0025 PA-5).
- **The user, their language, or the tenant** — localization is resolved on the device; the factory
  sees only event key + string args. Since 2026-10-01 (ADR-0025 Amendment A3) the `mutable-content`
  flag lets each iOS app's Notification Service Extension render the keys in the language picked inside
  the app, read from the app's App Group; a missing or failing extension shows the alert iOS resolved in
  its system language. Promo carries no flag — its literal text is already in the account's language —
  and an unmapped key gets no APNs block at all.
  → [Which language a notification is in](/architecture/push-notifications#language)
- **Display text** — it emits keys and args, never sentences of its own. The sole sanctioned future
  exception is the promo follow-up (ADR-0025 verdict, CH-1): a pass-through of the admin-authored
  `title`/`body` values *already present in `data`* into a literal `ApsAlert` — never a new
  literal-text parameter. Anything else literal means ADR-0025 is violated or superseded.
- **Delivery semantics** — idempotency (ADR-0023 Mode A), retry/ack classification, token pruning,
  and the disabled/no-op path all belong to the dispatcher and consumer, not the factory.
- **The queue message shape** — it receives already-unwrapped `(eventKey, data)`; envelopes and
  message keys (ADR-0002 D2.1/D2.1a) are upstream concerns.

## Watch-list
- A new event key may enter the display map **only after** its loc-keys ship in BOTH iOS apps'
  main-bundle `Localizable.xcstrings` (client-first rule, ADR-0025 D2) — otherwise version-skew
  renders a raw key on the lock screen.
- **`order.cash_not_paid` is the next one in** (owner ruling 2026-10-06, the door non-payment). Both iOS
  bundles carry `push.order.cash_not_paid.title|body` since `d3aa991bc` and `223db7c6a`, so the rule above
  is met. It is not in the map yet: until it is, the push is data-only and an iOS customer sees nothing.
  Its entry is `["orderNumber", "amount"]`, which makes it the fourth key `amount` rides — the price owed,
  formatted by `MoneyText.Format` like the no-show credit — and it joins the pinned lists in
  `FcmMessageFactoryTests` in the same change
  → [Push notifications — the door non-payment push](/architecture/push-notifications#cash-not-paid).
- **Day-one catalog gate (ADR-0025 D5, CH-2):** the map must not go live before the first public
  release of both iOS apps carrying the full 23-event catalog — both AppDelegates already register
  FCM tokens, so a catalog-less build + live map = raw `push.*` keys on lock screens.
- **Per-tier loyalty text is a factory concern, not an NSE** (CH-3): if wanted, map known `tier`
  values to `push.loyalty.tier_upgrade.body.<Tier>` with a factory-side fallback to the generic
  argless key — never put `tier` in `loc-args`.
- **The Notification Service Extension exists now, for language only** (ADR-0025 Amendment A3). It
  renders the same `push.*` keys and allowlisted args the map already emits. If an event ever needs
  other on-device transformation (rich media, decryption, feed persistence at delivery), that is a new
  decision for the extension — do not smuggle it in via literal-text args here, and do not widen the
  flag to promo or to unmapped keys.
