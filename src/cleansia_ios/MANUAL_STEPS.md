# iOS — owner manual steps

These steps require a Mac, Xcode, and/or an Apple Developer account. Agents do not run them.

## 1. Install the toolchain (once)

```sh
brew install xcodegen xcbeautify
```

`openapi-generator` must be **exactly 7.10.0** — the version CI and Android pin — and
`scripts/generate-api-clients.sh` refuses any other. **Not** `brew install openapi-generator`: that is
the latest release. Install the jar CI installs (the checksum is in `.github/workflows/ios-ci.yml`) and
put a wrapper ahead of `/opt/homebrew/bin` on `PATH`. It needs a JDK 11+; macOS ships none, and
Homebrew's `openjdk@21` is keg-only, so the wrapper calls its `java` directly:

```sh
brew install openjdk@21
curl -fsSL -o ~/openapi-generator-cli-7.10.0.jar \
  https://repo1.maven.org/maven2/org/openapitools/openapi-generator-cli/7.10.0/openapi-generator-cli-7.10.0.jar
echo "615e014705af34861e789e0b2a11075d3c80db134f881e937265479a4a83996e  $HOME/openapi-generator-cli-7.10.0.jar" \
  | shasum -a 256 -c -
mkdir -p ~/.local/bin
printf '#!/usr/bin/env bash\nexec /opt/homebrew/opt/openjdk@21/bin/java -jar "$HOME/openapi-generator-cli-7.10.0.jar" "$@"\n' \
  > ~/.local/bin/openapi-generator
chmod +x ~/.local/bin/openapi-generator
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zprofile
```

Then open a **new** shell and check it — this must print `7.10.0`:

```sh
openapi-generator version
```

If a Homebrew copy is already installed, `brew unlink openapi-generator` takes it off `PATH`
(`brew link` puts it back). The fastlane lanes run the same script, so they need the wrapper too.

## 1b. Create the local build config (once, and only once)

```sh
cp src/cleansia_ios/Config/Local.xcconfig.example src/cleansia_ios/Config/Local.xcconfig
```

Open it and set the two values:

- `STRIPE_PUBLISHABLE_KEY` — your Stripe **publishable** key (`pk_test_...` / `pk_live_...`).
  Never a secret key; the build refuses one.
- `DEVELOPMENT_TEAM` — your 10-character Apple Developer Team ID.

One file, both apps. It is gitignored, so `git pull`, `git checkout` and `xcodegen generate`
cannot wipe it — which they previously did to the same values held in `project.yml` and
`Info.plist`. **Do this before step 2**, or the first customer build warns that card payment is
disabled.

## 2. Generate the API clients, then the Xcode projects

Clients first: both `project.yml` files reference the generated `Cleansia{Partner,Customer}Api`
packages. On a fresh clone `xcodegen generate` run before them stops with `Spec validation error:
Invalid local package`; over an older generation it succeeds and the build uses the stale client.
This is the order CI runs, and it is the same after every pull.

```sh
cd src/cleansia_ios
./scripts/generate-api-clients.sh
(cd CleansiaPartner  && xcodegen generate)
(cd CleansiaCustomer && xcodegen generate)
```

Open `src/cleansia_ios/Cleansia.xcworkspace` in Xcode. Confirm both app schemes
(`CleansiaPartner`, `CleansiaCustomer`) build for an iOS-16 simulator and that the `CleansiaCore`
package resolves.

## 3. Verify the package on the command line

`CleansiaCore` is an iOS-only package (`platforms: [.iOS(.v16)]`), so a bare `swift build` compiles for
the macOS host and fails the iOS-only SwiftUI availability checks. Verify against an iOS simulator:

```sh
cd src/cleansia_ios/CleansiaCore
xcodebuild -scheme CleansiaCore -destination 'platform=iOS Simulator,name=iPhone 17' build test
```

**Keep the clone out of `~/Desktop`, `~/Documents` and `~/Downloads`.** macOS privacy protection
does not let the simulator read those folders, and a good number of tests read repo files at run
time (string catalogs, sources, mascot assets). There they fail with *"Operation not permitted"* —
builds and archives are unaffected, so it looks like a test regression when it is not. A
`-derivedDataPath` inside those folders goes further: `CleansiaCoreTests.xctest` does not load at all.

## 4. Signing & provisioning (Apple Developer)

`CODE_SIGN_STYLE` is `Automatic` and `DEVELOPMENT_TEAM` defaults to empty in
`Config/Base.xcconfig`. Before running on a device or submitting to TestFlight:

- Set `DEVELOPMENT_TEAM` in `Config/Local.xcconfig` (step 1b) — **not** in `project.yml`, where
  the next pull would delete it, and **not** in Xcode's Signing & Capabilities editor, which writes
  into the regenerated `.xcodeproj` and loses it on the next `xcodegen generate`. fastlane instead
  passes `ASC_TEAM_ID` from `fastlane/.env` as an xcarg, which overrides the file; either is
  accepted by the pre-build check.
- Register the bundle ids `cz.cleansia.partner` and `cz.cleansia.customer` in the developer portal.
- Create/download provisioning profiles + certificates.

These are owner-only; agents do not manage provisioning.

## 5. Install the lint toolchain — CI is wired and blocking

```sh
brew install swiftlint swiftformat
```

The strict configs are checked in (`.swiftlint.yml`, `.swiftformat`) and **`ios-ci.yml` runs both as a
blocking job** — this is no longer a later ticket.

Two things that will otherwise cost you a round trip:

- **The CI versions are pinned deliberately: SwiftFormat `0.60.1`, SwiftLint `0.65.0`.** They are
  downloaded from their releases rather than brew-installed, into a `pinned-tools` dir placed earlier in
  `PATH` than `/opt/homebrew/bin`, because a brew bump silently adds new default rules and turns a green
  branch red without a source change. `brew install` gives you *latest*, so check `--version` and match
  the pins locally, or CI will disagree with your clean local run.
- **Run `swiftformat` FIRST, then `swiftlint` — that is the CI order.** A red step labelled *SwiftLint*
  is very often SwiftFormat; the two are reported separately but the format pass is what usually fails.
  Format any hand-written Swift before committing.

## 6. Brand fonts (Poppins + Nunito) — already bundled

The design system mirrors Android's Poppins (headings) / Nunito (body) pairing, and iOS cannot fetch
Google Fonts at runtime the way Android does, so the faces ship in the repo. **This step is done** — all
six `.ttf` files are committed under `CleansiaCustomer/Resources/Fonts/` and
`CleansiaPartner/Resources/Fonts/` (SIL OFL). Nothing to do unless you are adding a weight.

> ⚠️ **If you do add one, do NOT list it in `Info.plist`.** `Info.plist` is **generated by xcodegen**
> from `project.yml` → `info.properties`, so a hand-added `UIAppFonts` entry is deleted by the next
> `xcodegen generate` — and because the generated file is committed, it looks hand-editable and the loss
> reads as someone else's revert. Declare it in `project.yml` and regenerate. The runtime alternative,
> `CleansiaFont.registerBundledFonts(in: .main)`, needs no plist entry at all.

If a face is ever missing, `CleansiaFont` falls back to the system font at the same sizes and weights —
the apps still build and run, they just lose the brand typeface. Note that this fallback is **per glyph**:
Poppins covers no Cyrillic at all, so `ru`/`uk` headings already fall back while Latin ones do not.

## 7. Generate the Swift API clients

> **The first generation has HAPPENED — this section is now the re-run instruction, not a blocker.**
> Verified 2026-08-14: the committed specs carry `Device/Mine`, the device revoke and
> `EmployeePayroll/GetPeriodPays`; `CleansiaPartnerApi` and `CleansiaCustomerApi` exist on disk and are
> wired into both `project.yml` files under `packages:` **and** the target's `dependencies:`. No iOS
> feature work is gated on this.

The typed business clients are generated from the **shared committed mobile specs**
(`src/cleansia_android/openapi/{partner,customer}-mobile-api.json`) with `openapi-generator` (swift5 +
URLSession). A **spec re-dump** needs the mobile API hosts running; generating from the specs already
committed does not.

**To regenerate the clients — this is the whole command, and it needs nothing running:**

```sh
src/cleansia_ios/scripts/generate-api-clients.sh                 # both apps
```

It reads the committed specs off disk. It does **not** need the API up, and it is what you want after
pulling a branch whose backend DTOs changed.

**Only if you changed the backend contract yourself**, refresh the committed specs first — this one
does need the mobile API hosts running, and it is a separate step, deliberately not chained to the
line above:

```sh
src/cleansia_ios/scripts/refresh-mobile-spec.sh                  # partner:5002 + customer:5004
```

With no host running it now reports a skip and exits 0, leaving the committed specs alone. It used to
`exit 1`, and because it sat directly above the generate step in one copy-paste block, it took the
step you actually wanted down with it.

### The regeneration "did not take"

If a type the spec clearly defines is still missing after running the generator — `OrderReviewDto` with
no `tags`, `OrderListItem` with no `hasReview`, `ReviewTag` not in scope — **the generator is not the
problem and running it again will not help.**

`CleansiaCustomerApi` / `CleansiaPartnerApi` are **local `path:` SPM packages** (`project.yml:39-40`)
and are gitignored (`.gitignore:14-15`). Rewriting their sources on disk does not change their
identity, so Xcode and SPM keep serving the copy they already resolved. The generator reports success,
the files on disk are correct, and the build still sees the old shape.

Order matters — regenerate, then clear what cached it, then rebuild:

**Quit Xcode before deleting or regenerating a project.** Deleting the two `.xcodeproj` while Xcode
had the workspace open made it re-resolve packages without them and rewrite the committed
`Cleansia.xcworkspace/xcshareddata/swiftpm/Package.resolved` down to the one pin `CleansiaCore`
needs. If `git status` shows that file modified afterwards, `git checkout` it.

```sh
cd src/cleansia_ios
./scripts/generate-api-clients.sh customer      # prints the model count it produced

# prove the contract actually landed on disk before blaming the build
ls CleansiaCustomerApi/Models | grep -i reviewtag
grep -n "tags" CleansiaCustomerApi/Models/OrderReviewDto.swift

# then clear the caches that are still holding the old package
rm -rf ~/Library/Developer/Xcode/DerivedData/CleansiaCustomer-*
rm -rf CleansiaCustomer/.swiftpm CleansiaCustomer/CleansiaCustomer.xcodeproj/project.xcworkspace/xcshareddata/swiftpm
(cd CleansiaCustomer && xcodegen generate)
```

**The models are at `CleansiaCustomerApi/Models/`, NOT `CleansiaCustomerApi/Sources/…`.** Despite
`useSPMFileStructure: true`, `swiftPackagePath: .` puts every source at the package root and the
generated `Package.swift` declares `path: "."` to match. Looking in a `Sources/` directory that never
existed makes a perfectly good generation look like a failed one.

Verified by running the committed config against the committed spec: 160 models, including
`Models/ReviewTag.swift`, with `OrderReviewDto.tags: [ReviewTag]?` and `OrderListItem.hasReview: Bool?`.
So if those are missing on a machine, the spec and the config are not the cause.

In Xcode the equivalent is **File → Packages → Reset Package Caches**, then Product → Clean Build
Folder. If the two `grep`s above find nothing, the generator genuinely did not run — check that
`openapi-generator` is on PATH and is 7.10.0.

`openapi-generator` must be **7.10.0** (§1) — the hand-written request spine subclasses generator
internals, and the script now stops with the version it found rather than generating with another.

This emits `CleansiaPartnerApi/` and `CleansiaCustomerApi/` (gitignored, machine-owned — never
hand-edit; see `openapi/README.md`). Both are already wired into their app's `project.yml`, under
`packages:` and the target's `dependencies:`; there is nothing to uncomment.

The **auth client stays hand-written** (`CleansiaCore/Auth`) and is **excluded from codegen** — only the
business endpoints are generated. Generation does not block the rest of Phase 0, which builds against
`URLSession` + `CleansiaCore` with no generated client.
