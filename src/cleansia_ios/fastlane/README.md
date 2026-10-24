# Cleansia iOS — TestFlight lanes

Local [fastlane](https://fastlane.tools) lanes that build and ship each app to
TestFlight in one command. They run **on your Mac** so they reuse your
working-tree Stripe key + `GoogleService-Info.plist` and your Xcode-managed
signing — no secrets in CI.

## One-time setup

1. Work in a clone **outside `~/Desktop`, `~/Documents` and `~/Downloads`**,
   for example `~/cleansia-release`. The lanes refuse a clone in those folders:
   the tests they run before archiving read repo files, and macOS does not let
   the simulator open files there.
2. Install the Xcode named in `../.xcode-version` (26.3 today) in
   `/Applications`. The lanes select it by the version `xcodebuild -version`
   reports, and refuse to run without it; `ios-ci.yml` tests with the same one.
   The app's file name does not matter.
3. Install Ruby deps (from `src/cleansia_ios`):
   ```bash
   bundle install
   ```
4. Create an **App Store Connect API key** (App Store Connect ▸ Users and Access
   ▸ Integrations ▸ Keys). Download the `.p8` once; store it outside the repo.
5. Copy the env template and fill it in:
   ```bash
   cp fastlane/.env.example fastlane/.env    # .env is gitignored
   ```
   Set `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_PATH`, and `ASC_TEAM_ID`.
6. Put each app's `GoogleService-Info.plist` in its `Firebase/` folder. A lane
   refuses to archive an app without it, since that build would have neither
   push nor crash reporting.
7. Make sure the app records exist in App Store Connect (`cz.cleansia.customer`,
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

From `src/cleansia_ios`, with everything committed:

```bash
FASTLANE_SKIP_DOCS=1 bundle exec fastlane partner  api_base_url:https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net
FASTLANE_SKIP_DOCS=1 bundle exec fastlane customer api_base_url:https://api-cleansia-customer-mobile-weu-dev.azurewebsites.net
```

Keep the `FASTLANE_SKIP_DOCS=1` prefix, or fastlane rewrites this hand-written
README with its generated lane list, and the next run refuses the dirty tree.
`bundle exec fastlane all` ships both apps in sequence, but takes no
`api_base_url`, so it only fits once the production hosts resolve.

### Always point a build at a host that answers

A Release build — so every TestFlight build — calls the **production** API
(`API_BASE_URL` under `configs: Release:` in each `project.yml`). While those
domains have no DNS, a build without `api_base_url:` reaches no backend, so
every build meant to be used passes the DEV host, as above.

The URL is injected as a command-line build setting, which outranks the
`project.yml` value, for that one archive only — nothing is edited. `all` refuses
the option, because the two apps call different hosts. Such a build still
carries the **production** APNs entitlement that App Store export always signs
in, while the DEV backend sends Live Activity pushes through the APNs sandbox,
so those do not reach it.

## What a lane does

1. Refuses an app whose `Firebase/GoogleService-Info.plist` is missing.
2. Refuses a clone under `~/Desktop`, `~/Documents` or `~/Downloads`, then
   selects the pinned Xcode for the rest of the run.
3. Regenerates the OpenAPI clients and both `.xcodeproj`.
4. Refuses uncommitted or untracked changes under `src/cleansia_ios` and the
   mobile specs, checked after the regeneration, so a stale committed
   `Info.plist` counts too. The gitignored owner files never count.
   `allow_dirty:true` lets such a tree through: its build is stamped
   `<sha>-dirty` and never tagged.
5. Runs the app's test suite optimised (`-O`, whole module, with
   `TEST_RUNNER_MallocScribble=1`) on the pinned Xcode, the way CI does. This
   catches miscompiles that only the archiving compiler makes at `-O`, which a
   Debug run cannot see. A failure stops the lane before anything reaches App
   Store Connect; the log and the result bundle, which holds a crashed test
   host's frame, are in `build/tests-<app>`.
6. Picks the next build number (one past TestFlight) and archives Release
   through `Cleansia.xcworkspace` with automatic signing. The commit and branch
   are stamped into the app's `Info.plist` as `CleansiaGitCommit` and
   `CleansiaGitBranch`, and the lane stops if the exported app names any other
   commit.
7. Uploads the archive's dSYMs to Firebase Crashlytics, authenticated by the
   app's own `GoogleService-Info.plist`. A failed symbol upload only logs an
   error: the build still ships, and its crash reports arrive unsymbolicated.
8. Uploads to TestFlight with **What to Test** set to the bundle id, version
   and build, the commit and branch, the API host the exported app calls and
   the Xcode version.
9. Tags the commit `ios/<app>/<version>-<build>` (annotated, with the same
   notes) and pushes the tag only when the commit is already on a remote
   branch. The repository is public, so pushing a tag publishes the commits it
   points at; otherwise the tag stays local and the lane prints the
   `git push origin <tag>` to run once the commit is pushed. A failed push only
   warns, because the build is already up. Pushing uses your git credentials.

Internal testers get the build within minutes of processing (no review).

To check everything up to the upload without touching App Store Connect, run
the same steps 2–5 alone:

```bash
FASTLANE_SKIP_DOCS=1 bundle exec fastlane preflight app:partner
```

To find the source of a TestFlight build later:

```bash
git show ios/partner/1.0.0-15
plutil -extract CleansiaGitCommit raw CleansiaPartner.app/Info.plist
```

> The archive goes through the **workspace**, so the packages are the versions
> pinned in its committed `Package.resolved` — the ones CI builds. Archiving an
> app's `.xcodeproj` on its own re-resolves them, floats transitive Firebase
> dependencies past those pins and rewrites `CleansiaCore/Package.resolved`.
> The pre-archive tests and the archive share one package checkout,
> `build/SourcePackages`, which is also where `upload-symbols` comes from.

> The build number is injected at archive time (`CURRENT_PROJECT_VERSION`), never
> written into `project.yml`, so it survives xcodegen regeneration and keeps the
> app and its extensions (Live Activity, Notification Service) on the same version. To raise the **marketing
> version** (e.g. `1.0.0` → `1.1.0`), bump `MARKETING_VERSION` in both `project.yml`.

> To move to another Xcode, change `../.xcode-version` and install that Xcode on
> the archiving Mac in the same step: CI and the lanes both follow the file.
