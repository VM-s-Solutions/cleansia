# Cleansia iOS

Swift/SwiftUI parity ports of the Android customer + partner apps, sharing the backend Mobile API
contract. Architecture: ADR-0013 (port strategy), ADR-0014 (iOS-16 floor + `ObservableObject` state),
ADR-0016 (App Review / lint bar), ADR-0018 (design parity). Reference apps: `src/cleansia_android/`.

## Layout

```
src/cleansia_ios/
├── Cleansia.xcworkspace/        # opens CleansiaCore + both app projects
├── CleansiaCore/                # shared SPM package (the Android :core equivalent)
│   ├── Package.swift            # platforms: [.iOS(.v16)]
│   ├── Sources/CleansiaCore/
│   │   ├── State/               # sealed UiState / ActionState enums
│   │   ├── Network/             # ApiResult / ApiError (one definition, ADR-0011 D4)
│   │   ├── Auth/                # hand-written auth/session/header spine (later ticket)
│   │   ├── DesignSystem/        # design tokens (later ticket)
│   │   ├── Components/          # Cleansia* SwiftUI components (later ticket)
│   │   ├── Snackbar/            # global snackbar bus (later ticket)
│   │   ├── DI/                  # AppContainer protocol + AuthNetworkBoundary (lazy refresh/authed seam)
│   │   ├── Location/            # map/geocoding seam (later ticket)
│   │   ├── Push/                # APNs registration (later ticket)
│   │   └── Format/              # order/dispute formatters (later ticket)
│   └── Tests/CleansiaCoreTests/
├── Config/                      # Base.xcconfig (committed) + Local.xcconfig (gitignored, owner-local)
├── openapi/                     # openapi-generator config + codegen README (Swift business client)
├── scripts/                     # generate-api-clients.sh, refresh-mobile-spec.sh, check-local-config.sh
├── CleansiaPartnerApi/          # GENERATED swift5 client (gitignored — regenerate, never edit)
├── CleansiaCustomerApi/         # GENERATED swift5 client (gitignored — regenerate, never edit)
├── CleansiaPartner/             # partner app target (cz.cleansia.partner)
│   ├── project.yml              # XcodeGen spec — bundle id, iOS-16, signing placeholder, API_BASE_URL
│   ├── Info.plist
│   ├── Resources/Localizable.xcstrings
│   └── Sources/                 # PartnerAppContainer (AppContainer), AppConfig, App entry
├── CleansiaCustomer/            # customer app target (cz.cleansia.customer)
│   └── (same shape; CustomerAppContainer + AppConfig)
├── .swiftlint.yml               # STRICT, blocking — force_* = error (ADR-0016)
└── .swiftformat                 # STRICT — runs --lint in CI
```

## Local build configuration (do this once, before you build)

Two settings are per-developer and are **never committed**: the Stripe publishable key and your
Apple `DEVELOPMENT_TEAM`. They live in one gitignored file shared by both apps:

```sh
cp src/cleansia_ios/Config/Local.xcconfig.example src/cleansia_ios/Config/Local.xcconfig
# then fill in STRIPE_PUBLISHABLE_KEY (pk_...) and DEVELOPMENT_TEAM (10 chars)
```

`Config/Base.xcconfig` is committed, supplies empty defaults, and `#include?`s `Local.xcconfig`
last so your values win. Because nothing tracked and nothing generated carries the values, `git
pull`, a branch switch and `xcodegen generate` all leave them alone — fill the file in once and
never touch it again.

If you skip it, a build tells you so by name (`scripts/check-local-config.sh` runs as a pre-build
phase on both app targets) rather than failing silently:

| What is missing | Simulator / Debug | Release |
|---|---|---|
| `STRIPE_PUBLISHABLE_KEY` | warning; card payment hidden, cash only (fail-closed) | build **error** |
| `DEVELOPMENT_TEAM` | no diagnostic — the simulator needs no signing | see below |

A secret key (`sk_...`) pasted into the publishable slot is always a build error. Verify the
severity rules with `./scripts/tests/check-local-config.test.sh`.

> **A device build with no team fails with Xcode's own error, not ours** — Xcode resolves signing
> before any build phase runs, so you get *"Signing for 'CleansiaPartner' requires a development
> team. Select a development team in the Signing & Capabilities editor."* **Do not follow that
> advice.** The Signing & Capabilities editor writes into the `.xcodeproj`, which is gitignored and
> rebuilt by `xcodegen generate` — your team id would be silently dropped on the next regenerate,
> which is the bug this whole layout exists to remove. Set `DEVELOPMENT_TEAM` in
> `Config/Local.xcconfig` instead.

## Generating the Xcode projects (Mac, owner/dev)

The two `.xcodeproj` files are produced by [XcodeGen](https://github.com/yonyz/XcodeGen) from the
checked-in `project.yml` specs (a hand-written `.pbxproj` is fragile and merge-hostile; the spec is the
source of truth). They are gitignored — regenerate after pulling or editing a `project.yml`:

```sh
brew install xcodegen          # once
cd src/cleansia_ios/CleansiaPartner  && xcodegen generate
cd src/cleansia_ios/CleansiaCustomer && xcodegen generate
open src/cleansia_ios/Cleansia.xcworkspace
```

The package builds and tests on an iOS simulator. `CleansiaCore` is iOS-only (`platforms: [.iOS(.v16)]`),
so a bare `swift build` host-builds for macOS and fails the iOS-only SwiftUI availability checks — use an
iOS-simulator destination instead:

```sh
cd src/cleansia_ios/CleansiaCore
xcodebuild -scheme CleansiaCore -destination 'platform=iOS Simulator,name=iPhone 17' build test
```

## Generated business API client (swift5 + URLSession)

The typed business client is generated by `openapi-generator` from the **shared committed mobile specs**
that Android also reads (`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`) — one
backend contract, three clients (web NSwag, Android kotlin, iOS swift5), so the platforms can't drift.

- Config + the never-hand-edit discipline: `openapi/README.md` and `openapi/openapi-generator-config.*.yaml`.
- Regenerate: `scripts/generate-api-clients.sh [partner|customer]` (needs `openapi-generator` 7.x).
- Output (`CleansiaPartnerApi/`, `CleansiaCustomerApi/`) is **gitignored and machine-owned** — change the
  spec or the config and regenerate; never hand-edit it.
- The **auth/session/header spine is hand-written** (`CleansiaCore/Auth`) and **excluded from codegen** —
  see below. Only the business endpoints are generated.

Each app depends on its own generated package. After the first generation, add the local package to the
app's `project.yml` (these lines are commented-in only once the package directory exists, otherwise
`xcodegen generate` fails on the missing path):

```yaml
packages:
  CleansiaCore: { path: ../CleansiaCore }
  CleansiaPartnerApi: { path: ../CleansiaPartnerApi }   # CleansiaCustomerApi for the customer app
targets:
  CleansiaPartner:
    dependencies:
      - package: CleansiaCore
      - package: CleansiaPartnerApi
```

> **First real generation is owner-gated** (`manual_step: mobile-spec-regen`). The committed specs are
> stale (pre-T-0272); the toolchain wiring is complete and runnable now but emits a stale client until the
> owner regenerates the shared specs. See `MANUAL_STEPS.md` step 7.

## Docs

- `docs/header-parity-contract.md` — the invisible out-of-band Mobile API contract the hand-written auth
  spine must honour (`X-Device-Id`/`X-Device-Label`/`X-Time-Zone`, the no-`Bearer`-on-anon allow-list,
  single-use refresh + theft detection, the empty-token unconfirmed-email gate, body-token transport).

## Owner manual steps

See `MANUAL_STEPS.md`.
