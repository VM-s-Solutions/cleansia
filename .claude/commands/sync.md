# /sync — Detect and run NSwag / contract regeneration

Detect where the backend API contract changed and the generated clients are now stale, and produce
the required regeneration commands. Run them under `CLAUDE.md`, verify the resulting clients, and
report the commands taken.

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
3. Run regeneration before dependent frontend/mobile work, commit the generated clients with
   the contract change, and record the commands and verification on the corresponding tickets.

## Rules
- Run `npm run generate-*-client` when the contract changes; never hand-edit generated client files
  (S9). Shared DEV deployment/reset requires explicit owner approval; PRO access remains prohibited.
- Surface contract breakage (removed/renamed/retyped fields) explicitly — stale clients throw on
  deserialization.

## Example
```
/sync customer
```
