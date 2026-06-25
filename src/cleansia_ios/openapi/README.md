# OpenAPI codegen — Swift business clients

This directory holds the openapi-generator config for the typed Swift API clients. The clients are
generated, **never committed, and never hand-edited**.

## Source of truth

Both Android and iOS read the **same two committed specs** so the platforms can't drift:

| Spec | App | Generated package |
|---|---|---|
| `src/cleansia_android/openapi/partner-mobile-api.json` | Partner | `CleansiaPartnerApi` |
| `src/cleansia_android/openapi/customer-mobile-api.json` | Customer | `CleansiaCustomerApi` |

- Android: `openapi-generator` **kotlin** (`jvm-retrofit2`) — see the per-app `build.gradle.kts`.
- iOS: `openapi-generator` **swift5** (`urlsession`, async/await) — `openapi-generator-config.*.yaml` here.

## What is generated vs hand-written

- **Generated:** the business endpoints (orders, dashboard, services, payments DTOs, …) — typed models
  + `URLSession`-backed APIs with `async`/`await`.
- **Hand-written, excluded from codegen:** the **auth/session/header spine** in
  `CleansiaCore/Sources/CleansiaCore/Auth`. The body-token transport, single-use refresh + theft
  detection, the no-`Bearer`-on-anon allow-list, and the empty-token unconfirmed-email gate can't be
  expressed by the generated surface (see `docs/header-parity-contract.md`, ADR-0013). The Auth/Device
  endpoints in the spec are out of scope for the generated client.

## Never hand-edit the generated client

The output packages (`CleansiaPartnerApi/`, `CleansiaCustomerApi/`) are **gitignored** and
machine-owned. To change them you change the **spec** (owner regen) or the config in this directory,
then regenerate — edits made by hand are silently overwritten on the next run.

## Regenerate

```sh
brew install openapi-generator        # once, 7.x
src/cleansia_ios/scripts/generate-api-clients.sh            # both apps
src/cleansia_ios/scripts/generate-api-clients.sh partner    # one app
```

Refresh the committed specs from a running mobile API host (mirrors Android's `dumpOpenApiSpec`):

```sh
src/cleansia_ios/scripts/refresh-mobile-spec.sh             # partner:5002 + customer:5004
```

## Wiring into the build

Android makes the generated client a compile input by having every Kotlin/KSP compile task
`dependsOn("openApiGenerate")`, so the generator runs before the app source is resolved. iOS does the
equivalent out-of-band: the generator is **not** an in-Xcode run-script phase (a per-build network/codegen
step is fragile, and the first gen is owner-gated anyway). Instead:

1. Run `scripts/generate-api-clients.sh` once after a spec change → it emits the `Cleansia{Partner,Customer}Api`
   local SPM package(s) (gitignored).
2. Each app's `project.yml` declares that package as a local SPM dependency (currently commented out, since
   the package does not exist until the first gen). After the first generation, uncomment the
   `Cleansia{Partner,Customer}Api` entry under `packages:` **and** under the target's `dependencies:`, then
   run `xcodegen generate`.
3. SPM resolves the local package and the app target links the generated models + APIs. The app-side
   `MobileApiClient` adapter (`Sources/PartnerClients.swift` / `CustomerClients.swift`) then wraps the
   generated APIs behind the `CleansiaCore` `MobileApiClient` seam.

Net effect matches Android: a spec change → regenerate → the app compiles against the fresh typed surface,
and any backend shape drift becomes a compile error instead of a silent runtime mismatch.

## First real generation is owner-gated

> **`manual_step: mobile-spec-regen`** — the committed specs are stale (pre-T-0272; they are missing
> `Device/Mine`, `Device/{id}` revoke, and `EmployeePayroll/GetPeriodPays`). The **first real client
> generation** is held until the owner regenerates the shared specs. The toolchain *wiring* (config +
> scripts + discipline) is complete and runnable now; it just produces a stale client until the regen.
