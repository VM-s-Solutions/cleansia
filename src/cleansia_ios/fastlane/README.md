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
   `cz.cleansia.partner`) and each App ID has its capabilities enabled — see the
   repo's TestFlight runbook. The **first** archive is easiest done once by hand
   in Xcode Organizer so Xcode bootstraps the distribution certificate + App
   Store profiles; after that these lanes are non-interactive.

## Ship a beta

From `src/cleansia_ios`:

```bash
bundle exec fastlane customer   # Customer app → TestFlight
bundle exec fastlane partner    # Partner app  → TestFlight
bundle exec fastlane all        # both
```

Each lane: regenerates the OpenAPI client + `.xcodeproj` → picks the next build
number (one past TestFlight) → archives Release with automatic signing → uploads.
Internal testers get it within minutes (no review).

> The build number is injected at archive time (`CURRENT_PROJECT_VERSION`), never
> written into `project.yml`, so it survives xcodegen regeneration and keeps the
> app and its Live Activity extension on the same version. To raise the **marketing
> version** (e.g. `1.0.0` → `1.1.0`), bump `MARKETING_VERSION` in both `project.yml`.
