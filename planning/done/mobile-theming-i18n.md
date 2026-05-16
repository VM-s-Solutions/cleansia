# Mobile — Dark theme + Multilanguage

What was implemented and what needs follow-up.

## Done

### Dark theme
- `AppSettings` data class + `ThemePreference` / `LanguagePreference` enums (`core/settings/AppSettings.kt`)
- `AppSettingsRepository` backed by Jetpack DataStore (`core/settings/AppSettingsRepository.kt`)
- `SettingsModule` Hilt module providing the singleton repo
- `MainActivity` now extends `AppCompatActivity`, collects settings as state, exposes `LocalAppSettings` CompositionLocal, and passes resolved `darkTheme: Boolean` into `CleansiaTheme`
- Base XML theme switched to `Theme.AppCompat.DayNight.NoActionBar`
- `AppearanceScreen` — System / Light / Dark radio picker with live change
- Profile preferences row "Appearance" → navigates to picker

### Multilanguage
- `LanguagePreference` enum covers System + en/cs/sk/uk/ru
- `LanguageScreen` — radio picker that writes preference AND calls `AppCompatDelegate.setApplicationLocales()` to switch instantly
- Profile preferences row "Language" → navigates to picker
- `values-cs/strings.xml` — **full Czech translation** of ~180 user-facing strings (nav, auth, home, orders, booking wizard, rewards, profile, appearance, language)

## Todo — owner action

### 1. Full translations for sk / uk / ru
`values-sk/strings.xml`, `values-uk/strings.xml`, `values-ru/strings.xml` currently contain only `app_name` and `splash_tagline`. Android falls back to English for any missing key, so the app works — but Slovak/Ukrainian/Russian users will see a mix of their language and English strings.

**Recommendation**: copy `values-cs/strings.xml` as a template and have a native speaker or translation service localize.

### 2. Strings not yet translated in CS
The core user flow is covered. The long-tail strings that were NOT translated to Czech in this pass (will show in English even on Czech locale):

- Order detail screen labels (`order_detail_*`) — ~20 strings
- Auth screens: sign-up, forgot password, email verify, validation errors — ~30 strings
- Profile sub-screens: Edit, Security, Payment methods, Notifications toggles, Help & support, FAQ — ~60 strings
- Address form error messages (`address_error_*`)
- Rewards milestone perk descriptions

Not blocking — app is functional. Add these during a focused second pass.

### 3. Dark mode color review
All Compose code uses `MaterialTheme.colorScheme.*` tokens that switch automatically between `LightColors` and `DarkColors` palettes defined in `ui/theme/Theme.kt`. **Eyeball every screen in dark mode** for any hardcoded colors that don't switch:

- Gradient hero cards (Home, Rewards, Referral) use explicit `Sky600/Sky400` / purple gradients. These look fine in both themes.
- Mascot PNGs have white backgrounds — check they don't look weird on dark surfaces. May need transparent versions.
- Trust strip icons use `SuccessText` / `WarningStar` constants — verify contrast.

### 4. Persist locale across app restarts
`AppCompatDelegate.setApplicationLocales()` persists via the AppCompat backupAgent automatically on API 33+. For older APIs, we also save the preference in DataStore and should re-apply it on `MainActivity.onCreate` if the current `AppCompatDelegate.getApplicationLocales()` doesn't match. Add this to the activity if you see the language reset on some devices.

### 5. RTL support
Not applicable currently (no Arabic / Hebrew locale). Czech / Slovak / Ukrainian / Russian are all LTR. Skip.

### 6. Date/number formatting
Currency (`1 299 CZK`) is hardcoded as strings with CZK. If the app ever serves non-Czech markets, wire up `java.text.NumberFormat.getCurrencyInstance(Locale)` and `DateTimeFormatter.ofLocalizedDate(FormatStyle, Locale)`. Not blocking for Czech market.

## Usage

**Change theme from profile:**
Profile → Preferences → Appearance → pick System / Light / Dark

**Change language from profile:**
Profile → Preferences → Language → pick System / English / Čeština / Slovenčina / Українська / Русский

Both changes apply live without app restart.
