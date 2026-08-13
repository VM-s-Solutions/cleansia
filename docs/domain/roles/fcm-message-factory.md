# Role — `FcmMessageFactory` (CRC card)

> Introduced by **ADR-0025** (iOS push display via per-platform APNs alert with loc-keys). A pure,
> static translator inside `Cleansia.Infra.Clients.Fcm`, extracted from the previously-inlined
> message construction in `FcmPushDispatcher.SendAsync` so the FCM wire shape is unit-testable.
> Status: **binding** — ADR-0025 accepted 2026-07-15 (panel consensus; see the ADR's Verdict).

## Responsibility (one sentence)
Translate `(deviceTokens, eventKey, data)` into the exact per-platform FCM wire shape — the
byte-stable data-only payload + `AndroidConfig` for Android, plus an APNs-scoped
`aps.alert` (`title-loc-key`/`loc-key`/allowlisted ordered `loc-args`, sound, thread-id) **iff** the
event is in its display map — deterministically and without I/O.

## Collaborators
- `FcmPushDispatcher` — its only caller; hands the factory's `MulticastMessage` to
  `FirebaseMessaging.SendEachForMulticastAsync` and owns everything after the wire (init, failure
  classification, dead-token prune signaling).
- The **APNs display map** it owns internally: the 12 displayable event keys (ADR-0025 D2 — union
  of what the two Android apps render *from fixed client-side templates*; `promo.new_sitewide`
  excluded **by nature**: it is a literal-text event with no fixed template anywhere — panel
  finding CH-1) → derived loc-keys (`push.<event_key>.title|body`) + ordered arg names.
- The **loc-args allowlist** it enforces: `{orderNumber, count}` only (ADR-0025 D3; pinned by
  TC-PUSH-APNS-5).

## Does NOT know
- **Which platform a token belongs to** — `ApnsConfig` is attached platform-blind; FCM routes it.
  Never accept a `Device.Platform` parameter; that coupling was explicitly rejected (ADR-0025 PA-5).
- **The user, their language, or the tenant** — localization is resolved client-side from the
  device's bundle/locale; the factory sees only event key + string args.
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
- **Day-one catalog gate (ADR-0025 D5, CH-2):** the map must not go live before the first public
  release of both iOS apps carrying the full 12-event catalog — both AppDelegates already register
  FCM tokens, so a catalog-less build + live map = raw `push.*` keys on lock screens.
- **Per-tier loyalty text is a factory concern, not an NSE** (CH-3): if wanted, map known `tier`
  values to `push.loyalty.tier_upgrade.body.<Tier>` with a factory-side fallback to the generic
  argless key — never put `tier` in `loc-args`.
- If an event ever needs true on-device transformation (rich media, decryption, feed persistence
  at delivery), that is the Notification Service Extension seam (ADR-0025 Option B) — do not
  smuggle it in via literal-text args here.
