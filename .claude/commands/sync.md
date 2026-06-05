# /sync — Detect & flag NSwag / contract regeneration

Detect where the backend API contract changed and the generated clients are now stale, and produce
the precise **owner-only** regeneration instructions. The agents do **not** regenerate clients —
this command tells the owner exactly what to run.

## Usage
```
/sync                 # detect stale contracts across all clients
/sync customer        # focus one client (customer | partner | admin)
```

## What it does
Act as the **Backend Dev / Reviewer** in contract-parity mode:

1. Find backend DTO/endpoint changes (recent diffs in `Cleansia.Web.*` controllers and
   `Features/**/DTOs`) not yet reflected in the generated TypeScript clients under
   `src/Cleansia.App/libs/core/services/.../client/`.
2. Report, per affected client, the breaking vs. additive changes and the regeneration command:
   - `npm run generate-partner-client`
   - `npm run generate-admin-client`
   - `npm run generate-customer-client`
3. Flag the corresponding ticket(s) with `manual_step: nswag-regen` and hold dependent
   frontend/mobile work until the owner confirms regeneration.

## Rules
- **Never** run `npm run generate-*-client` or hand-edit generated client files — owner-only (S9).
- Surface contract breakage (removed/renamed/retyped fields) explicitly — stale clients throw on
  deserialization.

## Example
```
/sync customer
```
