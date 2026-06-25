# iOS — owner manual steps

These steps require a Mac, Xcode, and/or an Apple Developer account. Agents do not run them.

## 1. Install the project generator (once)

```sh
brew install xcodegen
```

## 2. Generate the Xcode projects

```sh
cd src/cleansia_ios/CleansiaPartner  && xcodegen generate
cd src/cleansia_ios/CleansiaCustomer && xcodegen generate
```

Open `src/cleansia_ios/Cleansia.xcworkspace` in Xcode. Confirm both app schemes
(`CleansiaPartner`, `CleansiaCustomer`) build for an iOS-16 simulator and that the `CleansiaCore`
package resolves.

## 3. Verify the package on the command line

```sh
cd src/cleansia_ios/CleansiaCore && swift build && swift test
```

## 4. Signing & provisioning (Apple Developer)

The `project.yml` specs ship signing placeholders: `DEVELOPMENT_TEAM` is empty and
`CODE_SIGN_STYLE` is `Automatic`. Before running on a device or submitting to TestFlight:

- Set the Apple Developer **Team** on each app target (or set `DEVELOPMENT_TEAM` in the `project.yml`
  `settings.base` and regenerate).
- Register the bundle ids `cz.cleansia.partner` and `cz.cleansia.customer` in the developer portal.
- Create/download provisioning profiles + certificates.

These are owner-only; agents do not manage provisioning.

## 5. Install the lint toolchain for the CI gate (later ticket wires CI)

```sh
brew install swiftlint swiftformat
```

The strict configs are already checked in (`.swiftlint.yml`, `.swiftformat`). The blocking CI job
itself is a later ticket.

## 6. Bundle the brand fonts (Poppins + Nunito)

The design system mirrors Android's Poppins (headings) / Nunito (body) pairing. iOS cannot fetch the
Google Fonts at runtime the way Android does, so the `.ttf` files must be bundled into each app target:

- Add to each app's `Resources/`: `Poppins-Medium.ttf`, `Poppins-SemiBold.ttf`, `Poppins-Bold.ttf`,
  `Nunito-Regular.ttf`, `Nunito-SemiBold.ttf`, `Nunito-Bold.ttf` (download from Google Fonts; SIL OFL).
- List the six files under `UIAppFonts` in each app's `Info.plist`, **or** call
  `CleansiaFont.registerBundledFonts(in: .main)` at app launch.

Until the fonts are bundled, `CleansiaFont` falls back to the system font at the same sizes/weights, so
the apps still build and run — they just don't render in the brand typeface.

## 7. Generate the Swift API clients — `manual_step: mobile-spec-regen`

The typed business clients are generated from the **shared committed mobile specs**
(`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`) with `openapi-generator` (swift5 +
URLSession). The toolchain wiring is complete (`openapi/`, `scripts/generate-api-clients.sh`), but the
**first real generation is owner-gated**: the committed specs are stale (pre-T-0272 — missing
`Device/Mine`, `Device/{id}` revoke, `EmployeePayroll/GetPeriodPays`).

```sh
brew install openapi-generator                                   # once, 7.x

# (owner) refresh the shared specs from the running mobile API hosts first
src/cleansia_ios/scripts/refresh-mobile-spec.sh                  # partner:5002 + customer:5004

# then generate
src/cleansia_ios/scripts/generate-api-clients.sh                 # both apps
```

After the first generation, wire each generated package into its app: uncomment the
`Cleansia{Partner,Customer}Api` entry under `packages:` **and** under the target's `dependencies:` in
`CleansiaPartner/project.yml` / `CleansiaCustomer/project.yml`, then re-run `xcodegen generate`. See
`openapi/README.md` ("Wiring into the build") for the full flow and the never-hand-edit discipline.

This emits `CleansiaPartnerApi/` and `CleansiaCustomerApi/` (gitignored, machine-owned — never
hand-edit; see `openapi/README.md`). After the first generation, add each local package to its app's
`project.yml` and regenerate the Xcode project (the dependency lines are in `README.md`).

The **auth client stays hand-written** (`CleansiaCore/Auth`) and is **excluded from codegen** — only the
business endpoints are generated. Generation does not block the rest of Phase 0, which builds against
`URLSession` + `CleansiaCore` with no generated client.
