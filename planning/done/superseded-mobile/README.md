# Superseded mobile customer-app planning docs

These docs were created while the customer Android app was being scaffolded.
They predate the actual feature shipments and contain status markers
("Phase 3 unverified", "Phases 4–6 not started") that no longer reflect
reality.

The customer Android app now has:
- Real order detail with cancel/review/receipt/photos/disputes
- LiveProgressHero with status-aware mascot + live progress bar
- 30s background polling for active orders
- Loyalty tiers, promo codes, referrals
- SavedAddress integration with Mapbox
- Profile completion onboarding
- Booking flow with server-authoritative live quotes
- Rebook from completed orders
- Animated mascot system (welcoming + cleaning poses)
- Sentry crash reporting
- Disputes evidence upload

For the actual current state of shipped features, see:
- `planning/done/SHIPPED-SUMMARY.md`
- The code itself (`src/cleansia_customer_android/`)

These docs are kept here for historical reference only. Do not use them
as a source of truth for what's pending — they're out of date.
