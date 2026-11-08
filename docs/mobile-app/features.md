# Mobile App Features

This page describes **how a feature is built** in the mobile apps — what belongs in the shared
library, what a feature looks like on each platform, and where to look for a given piece of
behaviour.

It deliberately does **not** inventory screens. Mobile ships every sprint; a prose list of features
is stale before it is read, and a stale list is worse than none because it looks authoritative. The
directory tree *is* the inventory — the conventions below tell you how to read it.

For what the apps do from the outside, the audience docs are the better read:
[Partner App](/partner-app/overview) and [Customer App](/customer-app/overview) describe the same
domain from the web side, and the mobile apps target the same order lifecycle and the same backend
rules.

## Shared versus app-owned

The split is the same on both platforms, and it is deliberate: anything a wrong implementation could
make *insecure* or *inconsistent between the two apps* lives in the shared library, with exactly one
definition.

| Concern | Android (`:core`, `cz.cleansia.core`) | iOS (`CleansiaCore`) |
|---|---|---|
| Token storage | `auth/TokenStore` (EncryptedSharedPreferences) | `Auth/KeychainTokenStore` |
| Outgoing headers | `auth/AuthInterceptor` + `auth/DeviceHeadersInterceptor` | `Auth/HeaderAdapter` + `Auth/AnonymousAllowList` |
| 401 refresh | `auth/AuthAuthenticator` | `Auth/SessionRefresher` |
| Device identity | `auth/DeviceIdProvider` | `Auth/DeviceIdProvider` |
| Forced sign-out fan-out | `auth/SessionManager` + `auth/SessionScopedCache` | `Auth/SessionManager` + `SessionScopedCacheRegistry` |
| Result / error envelope | `network/ApiResult`, `network/ApiError` | `Network/ApiResult`, `Network/ApiError` |
| One-shot action state | `ui/state/ActionState` | `State/ActionState` |
| Design tokens | `ui/theme/` — `BrandColors`, `SemanticColors`, `Type`, `Spacing`, `Shape` | `DesignSystem/` |
| Shared components | `ui/components/Cleansia*` | `Components/Cleansia*` |
| Global snackbar | `snackbar/SnackbarController` | `Snackbar/SnackbarController` |
| Push token lifecycle | `notifications/PushTokenRepository` | `Push/PushTokenRegistrar` |
| The in-app language, where the app's own locale does not reach | `settings/AppLocale.localizedContext` (API 26–32) | `Localization/AppGroupLanguage` (the App Group, read by the extensions) |
| Location + geocoding | `location/` | `Location/` |
| Serviced countries/cities | `servicearea/` | `ServiceArea/` |
| Photo compression (both strip EXIF/GPS on re-encode) | `media/ImageCompressor` — reads the orientation tag first, via `androidx.exifinterface` | `Media/ImageCompressor` |
| Formatters, validators | `format/`, `validation/` | `Format/`, `Validation/` |
| Server HTML rendered in-app (the contract for work, ADR-0068 — JavaScript off, no navigation) | `ui/components/HtmlContentView` (`AndroidView { WebView }`) | `Components/HtmlContentView` (`WKWebView`) |

Everything else — screens, view models, per-feature API clients, navigation, the app's own theme
wrapper — is app-owned, because the partner and the customer app genuinely disagree about it. The
contract for work is the one legal text rendered in-app rather than linked to the web page: the
partner apps' `WorkContractSheet` (Android `features/orders/WorkContractSheet.kt`, iOS
`Features/Orders/WorkContractSheet.swift`; modes take / accept / read) shows the job facts and the
order's text over a *Swipe to accept the contract for work* slider (`SlideToCommit` / `SlideToConfirm`)
and every take path opens it first, sending the previewed text row's id with the take; the customer
apps' `WorkContractScreen` / `WorkContractView` render an accepted contract with its frozen facts from
the order detail's *Read the contract*, and the customer wizards' confirm step links to the public web
`/work-contract` page. → [Business rules — the contract for work](/product/business-rules#work-contract)

**Two shared inputs take a paste as it was copied** (since 2026-10-24):

- **The one-time-code entry** (`ui/components/CodeInput.kt`, `Components/CodeInput.swift`), behind the
  partner app's e-mail confirmation and the customer app's e-mail verification, is one real text field
  drawn as six boxes: a tap anywhere on them focuses it, and a long press opens its edit menu with
  Paste. Customer e-mail verification hides the separate paste action while retaining that edit menu
  and native code AutoFill. Partner confirmation keeps the standalone action: iOS uses the system's
  Paste button, which reads the clipboard without the prompt a read from code raises; Android uses
  a *Paste* text button, shown while the
  clipboard holds text, checked whenever the window regains focus, because Android 10 and later let
  only the focused app read the clipboard. The Paste button replaces what was typed with the
  clipboard's code (`CodeInput.pasting`, `pasteOneTimeCode`), so a fragment shows as itself and is
  never completed from typed digits; a clipboard without digits changes nothing. Every edit of the
  field itself — typing, its own long-press Paste, AutoFill or a keyboard's clipboard suggestion — goes
  through one rule per platform, held to one case list (`OneTimeCode.accept`, `acceptOneTimeCode`):
  only ASCII digits are kept, up to the code's length, and a run of at least that many digits arriving
  in one edit is a whole code and replaces the code typed so far; anything shorter is typing. The iOS
  customer app's two reset-code fields, the forgotten password's and the Security page's, keep and
  send their code by the same rule.
- **The phone field** keeps a `+` only while nothing has been kept before it
  (`PhoneNumberSanitizer.sanitize`, `sanitizePhoneInput`), so a number copied with a space, a label or
  a bracket in front of its country code keeps the code; until then only a `+` that was the very first
  character survived. The Android customer app's two profile phone fields, plain text fields, go
  through the same function since the same day.

→ [Partner onboarding — e-mail confirmation](/partner-app/onboarding#step-2-email-confirmation),
[what each step checks](/partner-app/onboarding#step-checks)

**The shared text field says what it holds** (since the fix plan of 2026-10-25). Every password
field, and the e-mail field of sign-up and sign-in, passes its AutoFill type through `CleansiaTextField`
— on iOS `textContentType` (`.username`, `.newPassword`, `.password`; the customer Sign up's e-mail
`.emailAddress`), on Android a `contentType` semantics on its modifier (`NewUsername`, `NewPassword`,
`Username`, `Password`) — so the phone's password manager offers a strong password on sign-up, a reset
and Security, and fills the saved one at sign-in. Neither app declares password rules: the platforms'
default suggestions already meet the password rule. On iOS a field with the `.emailAddress` keyboard
also turns auto-capitalisation and autocorrection off, as Android's `KeyboardType.Email` already did.
→ [Mobile overview — strong passwords](/mobile-app/overview#webcredentials)

::: warning One definition, not two copies
`ApiError`/`ApiResult` and the auth spine exist **once** per platform on purpose. A second copy in an
app module is the drift that produced "blank screen" bugs before the shared module existed. If you
need a variation, widen the shared type rather than forking it.

Two pre-`:core` duplicates survive on Android and are **known, tracked for migration, not
precedent**: `cz.cleansia.customer.ui.state.ActionState` (an identical copy of `:core`'s — its doc
comment says so) and the customer app's `ui/format/` formatters, which shadow `core/format/`. Import
the `:core` definition in new code.
:::

## Rating a completed cleaning {#rating-cleaning}

Both customer apps put the overall star rating first in the completion popup, with larger stars
and grouped optional feedback. Tags and comments remain optional; individual service scores
stay behind the existing disclosure. Submit stays below the scrolling form, with a quieter Not now
or Cancel action. Loading, errors, saved-review editing and the rating payload keep the same rules.

On a completed account booking without a review, **Rate your cleaning** is the primary action in the
fixed order-detail footer and **Book again** is secondary. After a review is saved, Book again resumes
its normal priority and the saved review remains in the detail with Edit. Guest bookings never gain
these rating actions. The automatic prompt still asks once per booking and skips an existing review.

The customer detail measures its fixed header and footer before allowing the sheet to collapse; a
short viewport exposes less map when the action stack needs the space. Other order states retain
their existing minimum, and other iOS sheet callers use the default geometry.

## Compact detail and payment recovery {#detail-payment-recovery}

Both customer details keep saved stars and Edit together at ordinary text sizes, with wrapping at
accessibility sizes. For an order without a discount, its country/currency context and total share
one row, with the price at the trailing edge. Discounted orders retain their discount chips and
original/current amounts. Narrow or enlarged-text layouts wrap or use a vertical fallback rather
than clipping a price. The full address is one naturally wrapping block containing the available
street, postal code, city and country; blank components do not reserve extra rows.

Photo classification is strict: Before is type 1, After type 2, Entrance type 3;
an absent or null phase remains in Other. A thumbnail carries its phase and stable photo identity to the fresh
gallery read; a removed image opens the collection without selecting a different photo. Fullscreen
paging stays within the selected collection. Access notes are absent from accessibility while
concealed, conceal again after 60 seconds, on inactive/background transitions and on order changes.

On the customer detail, the cleaning estimate occupies phase four of the thin, borderless status
tracker. A valid latest InProgress timestamp and positive estimate produce elapsed fill and
remaining/overrun wording.
The current phase carries a moving highlight, paused for reduced motion and app inactivity.
Invalid or future timing leaves an active tinted track without invented elapsed fill; reaching an estimate
does not advance the real lifecycle or complete phase five. Mascot sizing uses visible artwork bounds, including animation
frames, and the expanded sheet hides the ornament before it can be clipped.

The partner detail has had no tracker, timer, elapsed fill or estimate since 2026-11-01. Its head is
the Active hero's: a calendar leaf, the status line naming the job's current step and its time
(*Setting off · in 3 h 40 min*, *Getting in · was due at 14:00*, *Cleaning · 48 min left*,
*Finishing · 11 min left*), the booked window, the pay and the street; then the day line before the
work starts or, in progress, the window bar, the booked window with now on it. Under the head the run
sheet, *Setting off → Getting in → Cleaning → Finishing*, stamps each done step and opens the current
one with what it needs; *Cleaning* gives way to *Finishing* at the first after photo, or on a crew job
at the cleaner's own mark ([below](#crew-completion)). Time left always counts to the booked end, never
from the actual start, and past it the line reads *booked until 16:00*. The partner mascot stands on
the head's top edge (64 pt, 52 pt on a small iPhone) and is left out at accessibility text sizes
(Android from font scale 1.3); the public job number sits at the foot of the page.

The six-second Home indicator and automatic advance use the same clock. Manual swiping or a
gallery control pauses and resets the countdown through the interaction and settling. Once the
page settles, its selected capsule stays visible and starts a fresh six-second fill before
automatic paging resumes. Backward and cancelled drags also restart that interval. Screen-reader
or reduced-motion settings, app inactivity and an offscreen carousel continue to pause automatic
progress; returning starts a fresh dwell for the current page.

Booking review uses aligned label/value rows. The immediate post-booking confirmation replaces the
booking form inside the same sheet, at the same height: iOS keeps the large presentation, and
Android shows it in the booking sheet itself, which then rests only fully open or closed. It is one
ticket running from under the title to the actions. The header carries Booking received with the
public order reference beneath it, and the welcoming mascot stands on the ticket's edge. The
ticket's four rows answer, in order: whether a cleaner has taken it yet (*No cleaner has taken it
yet*, that the customer is told when one does, and what happens if none can — on a card booking a
full refund plus credit, on cash a free cancellation plus credit — with one dashed empty seat per
cleaner the job needs and the free-cancellation deadline); when and where (a calendar tile beside the
day, the time window and the street); what was booked (the service, rooms and baths, the dirtiness
when it is not normal, the extras by name); and what was paid (Total, then the credit and card
shares, or the cash line). The deadline is the order's own `freeCancellationHours` before the slot;
when that moment is already behind or the hours are unknown, the sentence that always holds shows
instead: free to cancel while no cleaner has taken it. The create-response confirmation code is not
shown. Spare height is spent in fixed steps — row spacing, then the mascot (116 pt on a standard
iPhone, 136 dp on a tall Android phone, smaller on compact phones), then row spacing again — decided
once at the hand-over, so it never collects into one empty band and nothing moves when the order
lands. At accessibility text sizes the ticket scrolls and View order stays pinned. The actions are a
neutral outlined Back to home beside a filled View order, which become View order over a Back to home
text button when a label would be cut.

This panel acknowledges creation with Booking received, without reading later lifecycle status or
showing elapsed, overrun, completion or cancellation variants. Live progress belongs to order
detail. The date, time, address, crew size, rooms, extras and payment method come from the submitted
booking, so they show at once; the amounts and the deadline come only from the server and hold bars
at their real size until they land. Unavailable details show Total with a dash, one line saying so
and Try again, which fetches the order again, without inventing payment facts. The booking overlay
stays until the details load or 1.2 s pass, and at least 600 ms (the card path raises it again after
the payment sheet); the confirmation swaps in beneath it. The overlay's cleaning figure stands as tall
as the welcoming figure will on the ticket: from the review step on, the confirmation the booking will
arrive as is laid out unseen beneath the sheet and the overlay reads its figure (the size tier's
estimate only before that). The card grows to fit, its message under the mascot, and only where the
booked ticket's figure differs from that draft's does the card's ease to it over 250 ms before it leaves.
The overlay card then fades and its scrim lifts while the mascot travels from the card onto the
ticket, changing into the welcoming figure at one size all the way: each pose is scaled by its own
figure, so the trip is a move, never a resize, and nothing on the ticket enters or moves. Plus
activation's overlay keeps its fixed mascot box. With reduced motion the card and the scrim crossfade
out in 150 ms and the thumbs-up is already in place. The welcome plays once and then rests on its thumbs-up
still, which is also what shows while the app is inactive or motion is off. The actions respond and
screen-reader focus moves to the title once the arrival settles. View order closes the sheet and
opens order detail;
Back to home, Back, a drag down from anywhere on the confirmation or a scrim tap close it and land
on Home, never back on the reset form. Dispute context displays the public order number while
internal identifiers remain routing/submission values.

Saved-card capture retains its setup identity in app-owned session state, keyed by country and
currency. The existing wipe registry erases it and cancels verification. Sheet completion starts at
most eight card reads, spaced 1.5 seconds apart; pending state remains available for load/resume or
manual verification, and simultaneous requests share one cycle. Only the returned savedCardId confirms
success; when the capture currency is known, it must also match. The existing nil-currency fallback
still requires that exact card identity. Restart requires fresh consent and does not cancel an old Stripe
setup or prevent a late webhook. Every sheet callback carries the original attempt and session
generation, so it cannot complete a replacement setup.

Partner invoice readiness uses both PDF-path presence and generation failure. A nonblank path keeps
Open available, including a previous PDF after failed regeneration; no-path states show a message
and refresh. Neither the stored path nor internal invoice ID is rendered. Download ownership and
storage checks remain server-owned. Partner quick actions keep full accessible labels and, when a
label does not fit, drop their icons, then put Navigate over Call and SMS, then stack; withdrawing
keeps its existing eligibility, and iOS offers native Apple/Google navigation choices without
changing price visibility rules.

iOS Live Activity registration uses an explicit public signing-environment marker and bounded
transient retries, with foreground recovery through existing order refresh. User-dismissed
activities remain suppressed for that session/order lifecycle. OS delivery timing and native strong
password rendering still require physical-device observation; source and simulator checks do not
establish instant delivery or reproduce a faint system password suggestion.

## Guest bookings in the customer's account {#listed-guest-bookings}

Both signed-in customer apps list/count guest bookings matching the confirmed session e-mail, badge
their cards and detail, and show the note explaining they were made without signing in. They offer
guest-term cancellation and hide receipt, photos, review/automatic review prompt, issue/no-show,
payment, preferred-cleaner and Live Activity actions. A guest photo request is not sent.
There is no anonymous guest screen reintroduced.
→ [Guest listing and its limits](/product/business-rules#listed-guest-bookings)

## Crew completion in the partner apps {#crew-completion}

With two or more assigned cleaners, each uses the **I'm done** slide (*Marks your part* on its second
line). After their own mark the pinned move waits for the others: *Call · Lenka*, naming the first
crew-mate whose part is still open, or the words *Waiting for Lenka* when the server sent no phone for
her. Once all seats are marked, **Slide to complete** — the solo wording since 2026-11-01, the shared
**Complete job** before — is available subject to the existing photo/payment gates; on a card job Stripe
has not charged it is a locked slide reading *This order's payment hasn't been confirmed yet.* One
assigned cleaner keeps **Slide to complete**.
The gesture is unchanged. The server owns the rule and returns translated `order.crew_not_done`
for a stale premature Complete. Crew steps are detail actions; a list swipe does not mark a seat.

While the job is in progress, the run sheet's current step lists *Before you can complete*: each
cleaner by given name with their done stamp or *not yet* (the crew-mate in green, the cleaner in sky,
colour as identity only), then the after photo and a line saying either of them can complete once both
parts are marked. A crew-mate whose part is still open has *Call* on her row. Her number goes only to
the phone's dialer and is never shown, read aloud, copied or stored, and no other state of the job
offers it. The cleaner's own mark, never a crew-mate's, moves her status line and run sheet from
*Cleaning* to *Finishing*; the on-the-way and start stamps of a crew job are the order's, drawn
*job 14:12*, because status is per order. A first mark pushes
`order.crew_mate_done` to the other eligible cleaners, including those already marked. Android
re-fetches after a local action and supports pull-to-refresh/on-resume freshness checks; iOS loads
on entering the detail and after its local actions. Following the push can open the job. An already
open detail does **not** refresh merely because the foreground push arrived; reopen or refresh to
see the changed crew. The server's two-hour automatic close still needs all marks, an after photo
and settled payment. → [ADR-0077](/decisions/adr-0077)

**Compatibility:** installed partner builds without I'm done cannot complete a job with two or more
assigned cleaners. With zero marks it never qualifies for automatic close. Use updated clients or
partner web for crew jobs while Android tester releases are deferred and TestFlight signing is
blocked. The new iOS crew-push catalog keys are present in source and registered in the APNs map;
older installed apps do not gain those keys from the server.

Authentication email fields on login, registration and password recovery retain their labels and show an example when focused and empty: `customer@example.com` in Customer and `partner@example.com` in Partner, on both native platforms. The example is a placeholder, never prefilled or submitted as an account. Native email keyboards, credential AutoFill and validation remain unchanged.

## Choosing a password at e-mail confirmation {#confirmation-password}

Both apps reuse the just-registered password only in memory; later confirmation visits ask for a
password and matching confirmation with the existing policy. Submission waits for a valid choice.
Both customer and partner send it with the code so only the mailbox holder chooses the working
password. Update before confirming to use this flow. A legacy confirmation sends no password,
withdraws the stored hash and needs Forgot password before password login; updating afterwards
cannot recreate it. → [Auth and identity](/flows/auth-and-identity#confirmation-password)

## Anatomy of a feature

### Android

```
partner-app/src/main/java/cz/cleansia/partner/
├── features/<area>/            # Compose screens + ViewModels + area-local composables
│   ├── <Area>Screen.kt
│   ├── <Area>ViewModel.kt      # @HiltViewModel, exposes StateFlow
│   └── <Area>Card.kt …         # presentational pieces
├── data/<area>/                # repositories + their Hilt @Module
│   ├── <Area>Repository.kt
│   └── <Area>Module.kt
├── core/                       # app-level plumbing: network/, auth/, notifications/,
│                               # devices/, settings/, servicearea/, location/
├── navigation/                 # PartnerNavHost.kt + NavRoutes.kt
└── ui/theme/                   # the app's Material 3 theme over :core's tokens
```

The customer app uses the same shape with one naming difference: its data layer lives under
`core/<area>/` (`<Area>Api.kt`, `<Area>Dtos.kt`, `<Area>Repository.kt`, `<Area>Module.kt`) and its
navigation entry point is `navigation/CleansiaNavHost.kt` + `navigation/Routes.kt`.

Hilt modules sit **next to the repository they provide**, not in one central `di/` package — so
deleting a feature deletes its wiring with it.

### iOS

```
CleansiaPartner/
├── Sources/
│   ├── CleansiaPartnerApp.swift        # @main entry
│   ├── PartnerAppContainer.swift       # the app's AppContainer (DI root)
│   ├── PartnerClients.swift            # MobileApiClient adapter over the generated client
│   ├── AppConfig.swift                 # reads API_BASE_URL out of the generated Info.plist
│   ├── L10n*.swift                     # typed accessors over Localizable.xcstrings
│   ├── Features/<Area>/
│   │   ├── <Area>View.swift            # SwiftUI view
│   │   ├── <Area>ViewModel.swift       # @MainActor final class : ViewModel (ObservableObject)
│   │   └── <Area>Content.swift …       # extracted subviews
│   └── Data/                           # most clients live app-level here (Partner*Client.swift);
│                                       # a few sit beside their feature under Features/<Area>/Data/
├── Tests/                              # hosted XCTest target, one file per view model
└── Resources/Localizable.xcstrings
```

The customer app is the same shape but consistently keeps each feature's clients beside the feature —
`Sources/Features/<Area>/Data/<Area>Client.swift` — and adds the `LiveActivity/` target directory.

State is `ObservableObject` + `@Published`, not `@Observable`: the deployment floor is iOS 16
(ADR-0014), where the observation macro is unavailable.

### The state contract, side by side

Both platforms use the same three-layer contract — the client/repository returns `ApiResult`, the
view model translates it into a `UiState` plus an `ActionState`, and errors go through the shared
snackbar via the app's error localizer. The `Devices` screen exists on both platforms and shows the
whole shape:

::: code-group

```kotlin [Android]
// partner-app/.../features/devices/DevicesViewModel.kt
sealed interface DevicesUiState {
    data object Loading : DevicesUiState
    data object Error : DevicesUiState
    data class Loaded(val devices: List<UserDeviceDto>) : DevicesUiState
}

@HiltViewModel
class DevicesViewModel @Inject constructor(
    private val devicesRepository: DevicesRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    /* … */
) : ViewModel() {

    private val _state = MutableStateFlow<DevicesUiState>(DevicesUiState.Loading)
    val state: StateFlow<DevicesUiState> = _state.asStateFlow()

    private val _revokeState = MutableStateFlow<ActionState>(ActionState.Idle)
    val revokeState: StateFlow<ActionState> = _revokeState.asStateFlow()

    fun load() = viewModelScope.launch {
        _state.value = DevicesUiState.Loading
        when (val result = devicesRepository.getMyDevices()) {
            is ApiResult.Success -> _state.value = DevicesUiState.Loaded(result.data)
            is ApiResult.Error -> {
                snackbar.showError(errorTranslator.translate(result.error))
                _state.value = DevicesUiState.Error
            }
        }
    }
}
```

```swift [iOS]
// CleansiaCore/Sources/CleansiaCore/Devices/DevicesViewModel.swift
@MainActor
final class DevicesViewModel: ViewModel {
    @Published private(set) var state: UiState<[UserDevice]> = .loading
    @Published private(set) var revokeAction: ActionState = .idle

    private let client: DevicesClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    func load() async {
        state = .loading
        switch await client.myDevices() {
        case let .success(devices):
            state = .loaded(devices)
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }
}
```

:::

Two differences are real and intentional:

- **`UiState` is shared on iOS, per-feature on Android.** `CleansiaCore/State/UiState` is a generic
  `loading | error(ApiError) | loaded(T)` enum. Android view models declare their own
  `sealed interface <Feature>UiState` because their loaded cases usually carry more than one payload.
- **`ActionState` is shared on both** — `Idle | Submitting | Error(message)`. Success is
  deliberately **not** a state: it is an effect that returns the action to `Idle` and (where needed)
  emits on a separate one-shot channel (`SharedFlow` / `PassthroughSubject`), so the same action stays
  re-armable for a retry.

## Freshness and session-scoped caches

Two shared mechanisms decide when a screen re-fetches and when it must forget everything:

| Mechanism | Android | iOS | Purpose |
|---|---|---|---|
| Freshness watermark | `core/freshness/Staleness` | `State/Staleness` | a repository stamps `markFresh()` after a successful fetch; a view model checks `isStale()` on screen entry and skips the round trip if the cache is warm. A user-initiated pull always bypasses it — the user's intent outranks the cache age. |
| Session-scoped cache | `auth/SessionScopedCache` (a Hilt multibinding) | `SessionScopedCacheRegistry` | any repository holding per-user state joins the set and is wiped automatically on sign-out, on forced sign-out, on account deletion (the customer apps) and at sign-in before the new tokens are stored. There is no hand-maintained list to keep in sync, so the four paths cannot drift. |

A repository may hold both, and should reset its `Staleness` from its `clear()` so the watermark
does not survive a session swap.

**The customer's loyalty cache is the case that matters most**, because it holds the points and the
credit balance. Every action that can move the credit invalidates it. A cancellation re-reads it at
once, and so does the card confirmation of a recurring occurrence, whose credit the server takes before
it mints the payment intent, so the re-read runs as soon as the confirm succeeds, before the payment
sheet opens. A booking re-reads it with the orders on iOS; on Android it marks it stale, because the
shell's scope ends as it leaves for the success screen, and the shell re-reads it on return.
Otherwise the cache goes stale after 30 seconds, and Home and Rewards re-read a stale cache on entry,
as does Profile on Android for its *Credit* row, showing the cached figures meanwhile (since
2026-10-02; Rewards used to keep whatever it held until a pull). A re-read on entry shows no spinner:
on Android the Rewards pull indicator follows the user's own pull only, as Home's does.

## Partner dashboard

Screens are not inventoried here, but the partner dashboard's rules span both apps and several files,
so they are written down once (Android `features/dashboard/`, iOS `Features/Dashboard/`). It answers
the four questions a cleaner has on opening the app: what is my state today, what is my next job, what
am I earning, and when am I paid.

- **The greeting** gives the date and the day's state: *You're free today*, *1 job today* or *N jobs
  today*, counted from the cleaner's upcoming orders that start today, leaving out any that were
  cancelled. Those orders are read from the start of the cleaner's local day, so a job earlier today
  still counts, even one already done; a job cancelled today does not. While they are unknown, because
  they have not been read yet, their read failed or the session has no employee ID to read them for,
  the state is left off and the date stands alone: an unread list is not a free day.
- **One hero, chosen in order:** the next job; otherwise, when any work is open, the available work
  (how many jobs are open and up to what they would pay); otherwise the empty hero. The next job is the
  soonest confirmed, on-the-way or in-progress order with a start time among the ten upcoming orders
  read, so a job done earlier today is passed over, and an order with no start time is never the next
  job.
- **When the next job starts** reads *In 25m* under an hour; under a day, *Today 14:00* on today's
  date and *In 5h 20m* past midnight; *Tomorrow 14:00* only on the next calendar day, so a job 47 hours
  away, two midnights on, shows its date. Any other start, a past one included, such as a job in
  progress, shows its date and time. Android writes *Wed 14 Oct · 14:00*, and outside the current year
  *14 Oct 2027 · 14:00*, dropping the weekday; iOS takes the same fields through its localized date
  template, as its other partner dates do.
- **Money** on the dashboard is in whole units, grouped, with the symbol of the currency the server
  names: *Kč* in every phone language, the same symbol Earnings, Invoices and Period pay print. A figure
  the server names no currency for carries no symbol; neither app guesses one from the phone's locale
  (Android `core/money/CurrencySymbols`, iOS `EarningsFormat.currencySymbol`). The web dashboard follows
  the same no-guess rule → [Currency formatting](/partner-app/dashboard#currency-formatting).
- **Shortcuts:** Profile, Pay history, Documents and Help. Help opens an e-mail to
  `support@cleansia.cz`; on a phone with no mail app it copies the address and says so, as the
  registration lock's support row and the customer app's Help do. An iPhone copies it too when Mail has
  no account set up, because Mail still takes the link on its setup screen.
- **A loaded dashboard is kept for 60 seconds:** coming back to the tab within that window repeats
  none of the dashboard's own reads; after it the dashboard refreshes in place, behind what it shows,
  and a pull always reads. Both apps still re-read the bell's unread count on every return, and iOS
  reads the pending-offers card when its 30-second store is stale. How each platform issues and
  publishes the reads is in [Native mobile request ownership](/architecture/frontend#partner-dashboard).

## Partner Orders in the native apps {#partner-orders}

Orders is the partner apps' board and work list (redesigned 2026-10-31; Android `features/orders/`, iOS
`Features/Orders/`). Its rules span both apps, and the shared ones are pure functions in
`OrdersBoardRules` on each platform, unit-tested on both.

Since 2026-11 both apps draw Orders on the app's own design system: every text on the shared type
scale, with no hand-set sizes, and the pieces the customer's order detail uses, from Core. The job
detail is a map band with the back button (since 2026-11-05): before the job is taken, its area blurred and
dimmed under *Exact address after you take the job*, placed from the area's name with no coordinate; once
taken, at every step, the map centred on the address with a pin and the street.
The map provider's logo and attribution stay sharp and visible on both, riding the panel's top edge. One
panel, Core's snap sheet, sits over the band with the state's mascot on its top edge, then the head (the
number and its status pill, the calendar line, the status line and progress, the street and the pay),
the day line, the run sheet on Core's timeline points, sections under icon headers with label and value
rows, and the move pinned at the foot: the app's one slide, or its locked face with the reason when the
move is not open yet. The panel rests expanded at the band's foot, under the fixed back button; dragged
down, or its handle tapped, it collapses onto its head, which stays pinned while the rest scrolls inside
the panel (since 2026-11-05; at the largest text sizes the head scrolls too). The map never moves or
stretches with the panel or the scroll, and opens at the band's scale and pin. Before the take it is
still. On her own job she pans and pinch-zooms it, never turning or tilting it, like a delivery app's
map: the pin and its street stay on the address, and once she has moved it a recentre button beside
the back button frames the job again (since 2026-11-05). A tap on the map opens nothing; Navigate
is in the run sheet. The Active hero reuses the detail's head. On iOS the detail
and the Orders root mount Core's edge swipe-back (`InteractivePopGestureEnabler`).

- **Three tabs on one sliding switcher** — *Available*, *Active* and *History*. Active carries its
  tab's total from the server as a count; Available carries that total less the loaded rows whose
  booked slot has ended, so it never reads 1 over *No open jobs right now*, and *See N open jobs*
  (on Active and in the job detail) reads the same number. Both are hidden while unknown or zero and
  read *99+* above 99; History has none. A label never breaks or ellipsizes: a count that does not fit
  beside it moves under it, and failing that lives only in the spoken label (*Available, 6 jobs*); as a
  last resort Android sets the label smaller, never below 12 sp.
- **Available** lists the board soonest first, the server's order. *Earnings high → low* is offered
  only once every row is loaded, because the server sorts pay within a page only. A page is 100 jobs,
  then *Load more*; a job whose booked slot has ended leaves the list as it leaves the board
  ([Offerability](/product/business-rules#offerability-until-the-booked-slot-ends)).
- **Days, then jobs.** Each day is one panel (since 2026-11-01), headed by one calendar leaf, the day's
  name, the cleaner's own job that day (*your job 15:00–18:00*, or *yours …* on every header of the page
  when one does not fit) and the day line, a strip from 07:00 to 19:00 widened to fit the data, with
  their own booked time solid, the open jobs dashed, now as a tick and a clash as one red block. Every
  mark is at least 12 pt wide (12 dp on Android): a shorter one is centred on its span and kept on the
  track, so a 7-minute job still shows; the Active hero and the job detail's head draw the same line.
  **A job row is one button that opens the job** (since 2026-11-05), on Available, Active's soonest
  open job and the detail's *Also open that day*; it carries no *Take*, and a job is taken from the
  detail's pinned *Take this job*. Its text column — the booked window, with *Under way* beside it once
  someone on the job has set off or started and the cleaner's pay for one seat trailing it (*Your share*
  on a team job; no figure when the server sends no pay), then the area (*Praha 4*) and the distance
  band from home, then the scope and the flags — sits beside a chevron in a lightly tinted column,
  vertically centred, on the card's trailing edge. The reserved card keeps its own rule: a pay tile
  with *Confirm*, its scope beside the tile unless full width makes the card shorter. There is no
  duration: the window says it. **The pay figure** is one string from the
  app's money formatter wherever it is drawn: the row, the reserved card, *Later*, the hero, the
  detail head, the detail's cash to collect, the take sheet and History. It reads *746,93 Kč*, and
  *1 290 Kč* when whole; the smaller haléře drawn until 2026-11 are gone.
  Flags follow, full width:
  *Overlaps your 15:00–18:00 job*; *Can be taken until 13:00* once the start has passed on a job nobody
  has set off for; *Cash: collect 1 290 Kč for the company*; *2 cleaners needed · 1 already on it*
  beside a ring per seat; and an increased or heavy dirt level. **Amber is for money only**: the cash
  flag is amber and the overlap red, and every other flag, the dirt level's included, keeps its icon in
  neutral ink. No per-hour figure anywhere.
- **The overlap is the server's rule, checked early.** A job overlaps when its booked window and one of
  the cleaner's own Active jobs' windows intersect; touching windows do not. The flag names the earliest
  such job, and the row still opens. When the Active list failed to load or
  is incomplete nothing is flagged, and the server stays the guard
  → [Take order validations](/partner-app/order-management#take-order-validations).
- **A job reserved for the cleaner comes first**: a pending offer joined by id to its board row, which
  is fetched by id when it is beyond the loaded page; when it cannot be found there is no card, and the
  Dashboard's card still lists it. It says *A customer asked for you*, until when, and that it then goes
  to other cleaners — stated, never counted down — and never shows the offer's customer price.
  *Confirm* opens the take sheet; *Decline* asks first.
- **When cash jobs are hidden**, the Available tab says so in amber above the list, and a compact
  notice under that line gives the cash held, to the cent, a meter of it against the limit (amber up to
  the limit, red past it) and the way out: *They come back once you hold 3 000 Kč or less: hand cash
  over, or it is set off against your next payout.* There is no expanding line (since 2026-11-01), and
  an empty tab shows the same notice in place of its *Cash you hold* panel. It reads
  `GET api/EmployeePayroll/GetCashHeld` (the first row with `cashJobsHidden`) and says nothing when
  that read fails → [Business rules — the float cap](/product/business-rules#cash-held).
- **The distance band is the server's**, from the cleaner's stored home, with a footnote under the list
  that it is straight-line and rough → [Distance from home](/product/business-rules#distance-band).
  Orders reads no phone location.
- **Active leads with one job**: the cleaner's own job in progress, else on the way, else the soonest
  confirmed, with its address, the customer, *Navigate*, *Call* and *SMS*, under the same status line
  as the job detail's head (*Setting off · in 3 h 40 min*, *On the way · in 20 min*,
  *Cleaning · 1 h 30 min left*). On a confirmed job the set-off slide stays locked until the
  [start grace window](/product/business-rules#start-grace-window) before the start opens, with the
  reason and the countdown inside it (*You can set off from 13:00* · *in 3 h 40 min*, or the date when
  it is another day), then reads *Swipe to mark on the way*; on the way, *Slide to start cleaning*;
  in progress, *Open job*, which opens the job detail. The list has no Complete and no per-row swipes
  — a job is finished from its detail — and the other jobs follow under *Later*. Active and History ask
  only for the cleaner's own jobs; when the app cannot read the cleaner's id they show an error rather
  than an unscoped list.
- **History** shows the completed jobs of *This week*, the current month or the previous month (*All
  time* is gone): how many, the cleaner's pay summed on the phone per currency, and the hours as *about
  41 h booked* — each job's whole booked window, never divided by the crew and never called worked.
  Weeks start on Monday in both apps, whatever the phone's region (since 2026-11-01). A current month
  with no completed job yet, as on the 1st, says *0 Kč* and *0 jobs* and, when last month had any,
  shows last month under it as a reference, with *Show October ›*. An empty week reads nothing extra.
  *Payouts are in Pay & Earnings ›* opens Pay & Earnings (its tab on iOS, its screen on Android). Active
  and History read every page of what they list.
- **Every state pulls to refresh.** An empty tab offers *Check again* too; a failed first load says so,
  with *Retry*; a failed refresh keeps the rows under *Couldn't refresh. Showing jobs from 10:14.* (with
  the day when it was not today); a failed *Load more* keeps what was loaded. A failed read of the board
  is announced to screen readers rather than raised as a snackbar.
- **Taking a job from the board keeps one sheet open** through five states: loading (the card's facts at
  once, the contract still coming), ready, submitting, **refused** — the server's reason in the app's
  existing words, and *Back to jobs* — and **taken**, with the street, whether it can still be cancelled
  in the app, and *Back to jobs* or *Open job*. Above the contract it states the cancel rule (*Free to
  cancel until …* only on the contract versions whose release clause it summarises; on a job already
  under way, that the reward shown is the full reward for the place and that it cannot be cancelled in
  the app once taken → [Offerability](/product/business-rules#offerability-until-the-booked-slot-ends);
  once the start has passed, that it cannot be cancelled either), on a team job that the app does not
  divide the work and that an overlapping job cannot be taken, and on a cash job, on those same
  versions, that the cash belongs to the company and is set off
  → [Business rules — the contract for work](/product/business-rules#work-contract). A contract reward
  that differs from the card's says it was updated from the card's figure. **A take from the board, the
  job detail or the Dashboard's offers ends on the Job taken panel** (since 2026-11-05): the title and mascot, a map
  strip with the pin and the street (once the job is read back), the day square with the day and window,
  the area and the reward, the cancel rule on one line, *Open job* and *Back to jobs*, and no Close. From
  the detail, *Open job* closes it onto the taken job and *Back to jobs* goes to the board's Available
  tab; from the Dashboard's offers, *Open job* opens the job and *Back to jobs* closes it onto the
  offers list, the area and reward shown from the offer until the job is read back. The job detail and
  the Dashboard's offers keep their own handling of a refusal. Job photos stay camera-only.
- **Four plain lines** — a lead and four lines summarising the contract for work: not let in, a late
  cancellation by the customer, complaints, own equipment — sit between the job's facts and the contract
  text (above its version line) in take and accept modes, never when an accepted contract is read, and only on the contract versions they were
  checked against, a list each app keeps. Any other version hides them
  → [Business rules — the contract for work](/product/business-rules#work-contract).
- **The job detail's edge states**, the same on both apps (its head and run sheet are
  [above](#detail-payment-recovery)):
  - **No board row.** With the board's row for the job, the detail draws the job from it at once and,
    if the rest fails, says *Couldn't load the rest of this job.* with *Retry*. Opened without one, from
    a push, the Dashboard or the removal notice, it knows nothing yet, not even whether the job is the
    cleaner's: the head, the run sheet and the move's slot are flat bars at their real size, and a
    screen reader hears only *Loading…*. A job not taken then has no distance band, and *Take* waits,
    disabled with a spinner, until the cleaner's own jobs have answered and the overlap can be checked;
    if that read fails, *Take* is offered and the server stays the guard. A load that fails says
    *Couldn't load this job.* with *Retry*, then what the app still holds: the cleaner's next jobs and
    the open jobs of the next seven days. A job the server no longer shows them (`order.not_found`, a
    push opened after someone took the job) says *This job is no longer open.*, with no *Retry*.
  - **Taken off the job.** Opened from the removal notice, a page stands in place of the job, which the
    server no longer shows them: *You were taken off this job*, the job's day, window and street when
    the board still holds its row, *An administrator took you off this job and gave this reason:* and
    the reason as written. With no reason on record it says *Job reassigned* and *This job is no longer
    on your schedule.* Under it come their next jobs, this one left out, and the open jobs of the next
    seven days, each from the board or read by the page itself. A list still being read holds its place
    as bars and one that fails is left out; with both failed the panel ends in *Browse jobs* and *Check
    again* → [Business rules](/product/business-rules#placement-is-an-offer).
  - **Cancelled.** The head reads *Cancelled at 08:10* in ink over a grey leaf, with no pay figure, and
    adds why when the system cancelled it (*· nobody could let you in*, *· the card payment was not
    completed* …). The day line shows the freed window as an empty slot, labelled *11:00–13:00 free*
    while it is still ahead. The pay line follows the reason. With none (the customer or an
    administrator) it is the general fee rule, *If the customer cancels late, you get your share of the
    fee collected.* A lockout the company confirmed reads, in bold, *The company confirmed it: you're
    paid your full reward.*, and every other system reason *Cancelled without a fee: nothing is paid for
    this job.* Then *Payouts are in Pay & Earnings*, the line that the customer's details were removed,
    their jobs later today (else their next jobs), the jobs open today and *See N open jobs*.
  - **Late.** On a confirmed job whose start has passed with nobody set off, the head reads *Setting
    off · was due at 14:00* and *Withdraw* is gone. *Getting in* already carries the lockout line, as
    on the way, and under *Swipe to mark on the way* sits *Already there? Start cleaning*, which opens
    the start confirmation: the server takes a start from a confirmed job.
- **The *In progress now* banner is gone** from every tab. On Available it could only ever show another
  cleaner's job, since the cleaner's own jobs are not on the board; their own job in progress is the
  Active hero.
- **Accessibility.** Each row is one spoken sentence in its visual order, read as one button that opens
  the job; the reserved card's *Confirm* and *Decline* are separate buttons that name the job. The take
  sheet's swipe, the hero's slides and the
  job detail's slides answer a VoiceOver or TalkBack activation; a locked slide reads as a disabled
  button whose label is its reason.

## Localization

Both platforms ship English, Czech, Slovak, Ukrainian and Russian — the same fixed set as the web
apps.

| Platform | Catalogs | Accessed via |
|---|---|---|
| Android | `res/values/strings.xml` (en, base) + `values-cs`, `values-sk`, `values-uk`, `values-ru`, on `:core` and both apps | `context.getString(R.string.…)` / `stringResource(...)` |
| iOS | `Localizable.xcstrings` in `CleansiaCore/Sources/CleansiaCore/Resources/` and in each app's `Resources/` | the app's typed `L10n*.swift` accessors; `CoreL10n` inside the package |

Backend error keys resolve through the platform's own naming convention, **not** the web apps'
`api.*` namespace — see [API Integration](/mobile-app/api-integration#error-keys-become-user-facing-text).

**The language picked in the app is the language of everything the app draws, and of every push
whose text the device writes** — the push banner, the notification feed and the Live Activity card,
whatever the phone is set to — except, probably, the iOS Live Activity's start alert, which iOS resolves
in the app's system language ([ADR-0029](/decisions/adr-0029#amendment-a5) A5, not yet checked on a
device). The sitewide promo push is not one of them: the server writes its text,
in the account's language. Neither platform gets that for free outside the app's own screens: iOS
resolves a push's keys in the app's system language, and Android 26–32 localizes only activity
contexts. Each closes the gap with the one shared seam in the table above. → [Which language a notification is in](/architecture/push-notifications#language)

**A count takes the plural form of the language picked in the app, not the phone's.** On iOS the
language switch points the bundles at the chosen language but never changes the process locale, so a
catalog plural formatted with `Locale.current`, or with no locale, picks its form by the phone's
rules. Until 2026-10-03 both iOS apps did that: an English phone running the app in Czech read
*2 pokojů* for *2 pokoje*, in Ukrainian *5 кімнати* for *5 кімнат*, and in Russian *Нужно 5 клинера*
for *Нужно 5 клинеров*. Each app's `L10n.plural` now formats against `CoreL10n.locale`, the locale of
the in-app language tag (`Locale.current` only for an empty tag). Android needed no change:
`pluralStringResource` reads the activity's resources, which carry the AppCompat in-app locale.

## Tests

| Suite | Location | Run with |
|---|---|---|
| Android `:core` | `core/src/test/` | `./gradlew :core:testDebugUnitTest --rerun` |
| Android partner | `partner-app/src/test/` | `./gradlew :partner-app:testDebugUnitTest --rerun` |
| Android customer | `customer-app/src/test/` | `./gradlew :customer-app:testDebugUnitTest --rerun` |
| iOS `CleansiaCore` | `CleansiaCore/Tests/CleansiaCoreTests/` | the `CleansiaCore` scheme, **from the package directory** |
| iOS partner | `CleansiaPartner/Tests/` | the `CleansiaPartner` scheme, from the workspace |
| iOS customer | `CleansiaCustomer/Tests/` | the `CleansiaCustomer` scheme, from the workspace |

The exact invocations, and the two ways these commands can report a false green, are in
[Overview → Verifying a change](/mobile-app/overview#verifying-a-change-without-a-false-green) and
[Overview → Building and testing](/mobile-app/overview#building-and-testing).

Android suites are pure JVM and there is no emulator step in CI. `:core` and `:customer-app` set
`unitTests.isReturnDefaultValues = true` so calls into `android.util.Log` and friends return
zero/null/false instead of throwing *"Method X not mocked"* — which is what you want for
fire-and-forget logging inside a class under test. iOS suites are hosted XCTest targets on a
simulator.

The two platforms substitute dependencies differently, and both choices are deliberate:

- **iOS** uses hand-written fakes over the client protocol — `Tests/Fake*Client.swift`,
  `Tests/Support/*Fakes.swift`.
- **Android** uses MockK, Turbine for flow assertions (in the two app modules), **and MockWebServer**
  where the wire contract matters. A hand-written Retrofit interface carries that contract in its
  `@Query` **names**;
  a mocked interface can only pin argument *values*, never the names the server binds. MockWebServer
  lets the test assert the URL that actually went out.
