# T-0698 — Extras have no admin CRUD, so they can only be priced by editing the seed

**Status:** `todo` · **Size:** M · **Filed:** 2026-09-09

## What

There is no admin surface for Extras anywhere in the tree — no `CreateExtra`, no `UpdateExtra`, no
`DeleteExtra`, no Angular feature lib. `GetExtraOverview` is the only feature, and it is the
customer-facing read.

The consequence after the multicurrency work: `ExtraPrices` is a per-currency price table like
`ServicePrices` and `PackagePrices`, and the other two get their editor in chunk 4. Extras get
nothing. **An extra's price can only be changed by editing `insert_seed_data.sql` and re-seeding**,
which is a developer action against a table that holds live prices — and once PRO exists, an
operation the owner has forbidden outright.

## Why it is filed rather than done

The owner asked for this CRUD during the multicurrency decisions ("Indeed, build CRUD for it"), and it
was scoped into the programme and then never reached: waves A and B and chunk 3 were schema work, and
chunk 4 is the wire batch. Owner ruling 2026-09-09: split it out and deliver it after chunk 4, rather
than growing the batch that already needs an NSwag regeneration, two mobile spec re-dumps and a Mac
session.

## Acceptance

- Admin create / update / delete / paged-list for Extras, matching the Services and Packages shapes.
- The price editor authors a row per active currency, the same way chunk 4 does for the other two.
- `Extra.Slug` is immutable after creation — order rows snapshot it (`OrderExtras.Slug`), so a rename
  would silently re-point history.
- Deleting an extra referenced by an order is refused, mirroring the catalogue FK restrict guards.
- Admin client regenerated; no customer, partner or mobile client changes (`ExtraListItem` stays as it
  is — it is on both mobile specs).
