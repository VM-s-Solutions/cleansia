# Cleansia iOS — TestFlight lanes

Local [fastlane](https://fastlane.tools) lanes that build and ship each app to
TestFlight in one command. They run **on your Mac** so they reuse your
working-tree Stripe key + `GoogleService-Info.plist` and your Xcode-managed
signing — no secrets in CI.

## One-time setup

1. Install Ruby deps (from `src/cleansia_ios`):
   ```bash
   bundle install
   ```
2. Create an **App Store Connect API key** (App Store Connect ▸ Users and Access
   ▸ Integrations ▸ Keys). Download the `.p8` once; store it outside the repo.
3. Copy the env template and fill it in:
   ```bash
   cp fastlane/.env.example fastlane/.env    # .env is gitignored
   ```
   Set `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_PATH`, and `ASC_TEAM_ID`.
4. Make sure the app records exist in App Store Connect (`cz.cleansia.customer`,
   `cz.cleansia.partner`) and each App ID has its capabilities enabled: Push,
   Sign in with Apple and App Groups (`group.cz.cleansia.customer`) on
   `cz.cleansia.customer`; App Groups on its Live Activity extension
   `cz.cleansia.customer.widgets` and its Notification Service Extension
   `cz.cleansia.customer.notificationservice`; Push and App Groups
   (`group.cz.cleansia.partner`) on `cz.cleansia.partner` and App Groups on
   `cz.cleansia.partner.notificationservice`. The lanes archive and export with
   `-allowProvisioningUpdates`, so automatic signing registers the extension App
   IDs and the groups on the first signed archive after they were added.
   The **first** archive is easiest done once by hand
   in Xcode Organizer so Xcode bootstraps the distribution certificate + App
   Store profiles; after that these lanes are non-interactive.

## Ship a beta

From `src/cleansia_ios`:

```bash
bundle exec fastlane customer   # Customer app → TestFlight
bundle exec fastlane partner    # Partner app  → TestFlight
bundle exec fastlane all        # both
```

Prefix each with `FASTLANE_SKIP_DOCS=1`, or fastlane rewrites this hand-written
README with its generated lane list.

### Pointing a build at another API host

A Release build — so every TestFlight build — calls the **production** API
(`API_BASE_URL` under `configs: Release:` in each `project.yml`). While those
domains have no DNS, a build meant to be used must say where to go instead:

```bash
bundle exec fastlane customer api_base_url:https://api-cleansia-customer-mobile-weu-dev.azurewebsites.net
bundle exec fastlane partner  api_base_url:https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net
```

The URL is injected as a command-line build setting, which outranks the
`project.yml` value, for that one archive only — nothing is edited. `all` refuses
the option, because the two apps call different hosts. Such a build still
carries the **production** APNs entitlement that App Store export always signs
in, while the DEV backend sends Live Activity pushes through the APNs sandbox,
so those do not reach it.

Each lane: regenerates the OpenAPI clients + both `.xcodeproj` → picks the next
build number (one past TestFlight) → archives Release through
`Cleansia.xcworkspace` with automatic signing → uploads. Internal testers get it
within minutes (no review).

> The archive goes through the **workspace**, so the packages are the versions
> pinned in its committed `Package.resolved` — the ones CI builds. Archiving an
> app's `.xcodeproj` on its own re-resolves them, floats transitive Firebase
> dependencies past those pins and rewrites `CleansiaCore/Package.resolved`.

> The build number is injected at archive time (`CURRENT_PROJECT_VERSION`), never
> written into `project.yml`, so it survives xcodegen regeneration and keeps the
> app and its extensions (Live Activity, Notification Service) on the same version. To raise the **marketing
> version** (e.g. `1.0.0` → `1.1.0`), bump `MARKETING_VERSION` in both `project.yml`.
