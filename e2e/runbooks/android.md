# Android runbook: the customer and partner apps on DEV

How an agent builds the customer and partner apps against DEV, installs them on its own emulator and drives the P1
mobile steps with `adb` and `uiautomator`. Every step marked **proven** was run once on the `cleansia35` AVD (Pixel
class, API 35 google_apis arm64, 1080 x 2400 at 420 dpi) against DEV on 2026-11-14. The cases a step covers are in
`e2e/cases/`.

Never run any of this against PRO: both debug builds default to the DEV mobile hosts
(`api-cleansia-customer-mobile-weu-dev…`, `api-cleansia-partner-mobile-weu-dev…`), which is what you want.

## 1. Build and install

```bash
cd src/cleansia_android
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home   # keg-only: `java` on PATH is the macOS stub
export ANDROID_HOME=$HOME/Library/Android/sdk                                     # replaces local.properties, which is gitignored and absent
export MAPBOX_ACCESS_TOKEN=pk.e2e-placeholder-no-map                              # see below

df -h /Users/michael                      # under 3 GB free: stop. The two builds leave about 1.1 GB under */build.
/Users/michael/cleansia-work.noindex/tools/withlock.sh gradle \
  ./gradlew :customer-app:assembleDebug :partner-app:assembleDebug --console=plain --no-daemon
E=<evidence folder>                       # the .apk files go here, never under the repo
cp customer-app/build/outputs/apk/debug/customer-app-debug.apk $E/customer-debug.apk
cp partner-app/build/outputs/apk/debug/partner-app-debug.apk   $E/partner-debug.apk

$ANDROID_HOME/emulator/emulator -avd cleansia35 -no-window -no-audio -no-boot-anim -no-snapshot-save \
  -gpu swiftshader_indirect > $E/emulator.log 2>&1 &              # under withlock.sh emulator when other agents share the Mac
export PATH=$ANDROID_HOME/platform-tools:$PATH
adb wait-for-device; until [ "$(adb shell getprop sys.boot_completed | tr -d '\r')" = 1 ]; do sleep 3; done
adb install -r $E/customer-debug.apk; adb install -r $E/partner-debug.apk
adb shell am start -n cz.cleansia.customer.debug/cz.cleansia.customer.MainActivity   # partner: cz.cleansia.partner.debug/cz.cleansia.partner.MainActivity
```

- A warm Gradle cache builds both apps in under a minute (`BUILD SUCCESSFUL in 46s`). The apks are 111 MB and 99 MB.
- The debug package is the release package plus `.debug`: `cz.cleansia.customer.debug`, `cz.cleansia.partner.debug`.
- **A blank `MAPBOX_ACCESS_TOKEN` crashes the customer app** the first time an order detail opens
  (`MapboxConfigurationException: … requires providing a valid access token`), and any sign-in to an account with an
  unrated completed order lands there. The build accepts a blank token and says the map "will fail to load"; it kills
  the process instead. Any string starting `pk.` stops the crash and leaves the map tiles blank, which no step needs.
  The real token is in the owner's `~/.gradle/gradle.properties`; never read it.
- **A blank `STRIPE_PUBLISHABLE_KEY` hides Card.** Step 4 of the booking offers only `Cash`. Same as iOS without
  `Local.xcconfig`. Card, the saved card and Google Pay are therefore not driven here (section 5).
- The build copies `google-services.sample.json` to the gitignored `google-services.json`; push never arrives.
- Free the disk when done: keep the two `.apk` files, `rm -rf customer-app/build partner-app/build core/build build`.
- Shut the emulator down at the end: `adb emu kill`. Never leave it running.

## 2. Driving with adb

Both apps are Compose. **There are no resource ids and no test tags**: the only id in a dump is
`<package>:id/action_bar_root`. A tap targets a node's `text` or `content-desc` (a button's text, a card's whole
sentence, a checkbox's description), or a coordinate. A `uiautomator dump` also lists rows that are scrolled out of
view, with stale or zero bounds: read the y before tapping.

```bash
# source me
UI=/tmp/ui.xml
dump(){ adb shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1; adb pull /sdcard/ui.xml $UI >/dev/null 2>&1; }
show(){ dump; python3 -I - $UI <<'PY'          # bounds [flags: c clickable, s scrollable, h checked, f focused] text desc
import sys,xml.etree.ElementTree as ET
for n in ET.parse(sys.argv[1]).iter('node'):
    t,d,b=n.get('text'),n.get('content-desc'),n.get('bounds')
    if t or d: print(b,''.join(k[0] for k in ('clickable','scrollable','checked','focused') if n.get(k)=='true'),repr(t),repr(d))
PY
}
tap(){ dump; python3 -I - $UI "$1" <<'PY' | { read x y || { echo "NOT FOUND: $1" >&2; return 1; }; adb shell input tap $x $y; }
import sys,re,os,xml.etree.ElementTree as ET     # TAPC=1: substring match. TAPN=-1: the last match.
want,sub,idx=sys.argv[2],os.environ.get('TAPC')=='1',int(os.environ.get('TAPN','0')); hits=[]
for n in ET.parse(sys.argv[1]).iter('node'):
    for v in (n.get('text'),n.get('content-desc')):
        if v and (v==want or (sub and want in v)):
            b=list(map(int,re.findall(r'\d+',n.get('bounds')))); hits.append(((b[0]+b[2])//2,(b[1]+b[3])//2)); break
if hits: print(*hits[idx])
PY
}
typ(){ adb shell input text "$(printf '%s' "$1" | sed 's/ /%s/g')"; }             # spaces are %s
clr(){ adb shell input keyevent KEYCODE_MOVE_END; for i in $(seq 60); do echo KEYCODE_DEL; done | xargs adb shell input keyevent; }
hidekb(){ adb shell dumpsys input_method | grep -q 'mInputShown=true' && { adb shell input keyevent KEYCODE_BACK; sleep 1; }; }
shot(){ adb exec-out screencap -p > "$1"; }
slide(){ adb shell input swipe 127 $1 1000 $1 700; }                              # the confirm slider, handle at x 127
```

Rules learned the hard way:

- **Close the keyboard before tapping a button under a field.** The soft keyboard covers the lower 35 % of the
  screen; a tap on `Sign In` at y 1546 landed on the digit row and typed `5` into the password. `hidekb` presses Back
  only when the keyboard is up, because a Back with no keyboard leaves the screen (or the app).
- A field has no text until it holds some: tap it by the coordinate of its floating label (the label's centre, then
  `typ`). Tapping a label node by text does not always focus the field.
- A password goes in from a file, never on a command line that is logged and never in a screenshot:
  `typ "$(cat $PW_FILE)"`. The sign-in field shows dots, but the dump prints the dot count.
- **Slide buttons are real gestures**: `swipe 127 <y> 1000 <y> 700` from the handle to the far end. The customer
  `Slide to pay` is at y 2232, the partner `Swipe to accept the contract for work` at y 2200. A slider drawn pale is
  disabled: read the screenshot, the dump does not say.
- The server is slow when DEV is cold: sign out takes several seconds, `Accept` on the contract about 5. Poll the dump,
  do not tap again. On 2026-11-14 at 10:25 all DEV hosts stopped answering for a while (`/health` timed out at 40 s),
  so a sign-up sat on its spinner; check `curl …/health` before blaming a step.
- A selected payment row is a toggle: tapping `Cash` twice leaves nothing selected and the slider pale.
- The customer app opens a **rating sheet** (`How was your clean?`) for each unrated completed order whenever the
  home screen shows, and again after each `Not now`. Loop `Not now` / `Back` until the home tab bar appears.
- Time is the emulator's: it follows the Mac (Prague). A time chip reads in the market's zone.

Tab bars (the labels are not in the dump of the customer bar):

| App | Tabs |
|---|---|
| Customer | Home (160,2200), Orders (350,2200), the round Book button (540,2180), Rewards (730,2200), Profile (920,2200) |
| Partner | `Dashboard`, `Orders`, `Invoices`, `Profile` as text nodes (y 2247) |

First launch: the customer app opens on `Welcome back` (Sign In). The partner app opens a carousel
(`Welcome to Cleansia Partner`, `Skip` at the top right, `Next`), then `Welcome back!`. Both apps then ask
`Allow Cleansia to send you notifications?` / `Allow Cleansia Partner to send you notifications?` (`Don’t allow`,
with the typographic apostrophe), on the first launch of 2026-11-15 though not of 2026-11-14. Two more system dialogs
come later: the partner's first `Add` under Photos asks
`Allow Cleansia Partner to take pictures and record video?` (`While using the app`), and the customer's `Add address`
asks `Allow Cleansia to access this device’s location?` (`Don’t allow`, with the typographic apostrophe).

## 3. Accounts

Customer `e2e.<run>.customer@example.com`, cleaner `e2e.<run>.cleaner@example.com`, one password per run in the run
folder. Register them in the app or through the APIs (see the Running section of `e2e/README.md`); a registered
account opens no session until its e-mail is confirmed, and `@example.com` never receives the code, so run
`sql-scripts/dev-e2e-confirm-accounts.sql` through `execute-sql.yml` (the project's confirm script does it). A cleaner
also needs the profile, an identity document and the admin approval before the board shows work.

For the steps that need history, a saved card or an approved cleaner, use the accounts of a book-stage run: its
`e2e.<run>.cleaner` is approved and its `e2e.<run>.customer` has a saved card, and `.run/<run id>/state.json` holds
their password and the ids of their orders. Never use an account outside `e2e.%@example.com`. The steps below were
proven with `e2e.a11140858.customer@example.com` and `e2e.a11140858.cleaner@example.com` (both registered in the
apps; the confirm script ran for them afterwards) and, for the steps that need history, with an older customer and
cleaner pair that was not an e2e one. A fresh customer has no address and no saved card: add the address through the API
(`POST /api/SavedAddress/Add`, bearer token from `/api/Auth/Login` on the customer mobile host), because the map in the
app cannot place a pin without the real Mapbox token (section 5).

## 4. The P1 steps

Customer app, coordinates are the 1080 x 2400 screen.

| Step | Cases | Recipe | Proven |
|---|---|---|---|
| Register in the app | AUTH-002, AUTH-010 | Sign In: tap `Register`. Fields: First name (300,535), Last name (700,535), Email (540,722), Password (500,912), Confirm password (500,1262), each `tap` then `typ`; `hidekb`; tick `Accept terms and privacy policy` (a checkbox, content-desc); `TAPN=-1 tap "Create account"` (the title has the same text). Lands on `Verify your email` (`Verification code, 6 digits`, `Verify code`, `Resend code`). | yes |
| Sign in before confirmation | AUTH-010 | Sign In with the unconfirmed account (Email (500,555), Password (400,745), `hidekb`, `TAPN=-1 tap "Log in"`): the app shows `Verify your email` again, with `Choose the password you'll sign in with once your email is confirmed.` No session. | yes |
| Sign in | AUTH-009, AUTH-015 | Same fields with a confirmed account. A first sign-in shows `Welcome aboard, <name>!` and `2 of 3 done` with the phone step: tap the `Phone number` field (650,985), `typ` nine digits, `hidekb`, `Save and continue`; the card turns `Ready to book` and `Skip for now` leaves the birth-date step. Then the home tab (`Cleaning at`, the bell `Notifications, N unread`). A fresh account reads `Select an address`. **The phone must be one no other account holds**: `PUT /api/User/UpdateCurrentUser` answers 400 `user.existing_phone_number` for `+420777123456`, the number this recipe used to give, and the step shows the refusal under the phone field (a timeout shows `Check your internet connection and try again.` as well). Type a number built from the run id (`603150609` was free). The card turns `Ready to book` as soon as the field holds nine digits, before anything is saved, so `Skip for now` right after a refused `Save and continue` leaves the account with no phone; the first slide of a booking then fetches the profile, finds no phone and opens `Edit profile` with nothing booked (`BookingSubmitOutcome.ProfileIncomplete`). Recover there: Phone field (400,1812), `clr`, `typ +420603150609`, `hidekb`, `Save changes`. | yes |
| Quote | BOOK-001, BOOK-011 | Book (540,2180) opens a four step sheet. Step 1 `What do you need?`: tap a service by its title (`Eco-Friendly Cleaning`); the footer reads `Continue · 960 Kč`, the live server quote; `TAPC=1 tap Continue`. Step 2 `How clean is your home?`: `Normal`, `Increased` (+15%), `Heavy` (+30%); Continue needs one. | yes |
| Date and time | BOOK-019, BOOK-015 | Step 3 `When & where`: the saved address is pre-filled; day chips `Today`, `Sun 15`, `Mon 16`, `Tue 17` (tap the number, y 945); part-of-day cards `Morning`, `Afternoon`, `Evening` with `N slots available`; a time chip is a node with content-desc `10:00` and, inside the express band, `12:30, Express +20%`. Swipe up to see them. Step 4 itemises `Express surcharge (+20%)`: 960 Kč became 1,152 Kč for a start about two hours out. | yes |
| Book cash | BOOK-031, BOOK-033, BOOK-048, TAKE-002 | Step 4 `Confirm booking`: scroll to `How do you want to pay?`, tap `Cash` (`Pay your cleaner after the visit.`; selected is the blue row), scroll to the end, tick `Accept terms and privacy policy` (the early-performance request), then `slide 2232`. Result sheet `Booking received`, `ORD-xxxxxxxx`, `Please pay in cash to the cleaner on the day.`, `Back to home`, `View order`. The sheet keeps its draft when it is dismissed: reopening Book lands on Step 4 with `Cash` and the terms already chosen, so read the checkbox (`checked` flag) before tapping it, or the tap unticks it. Wait for each step's screen before the next `tap`: a `Continue` tapped while the sheet is still animating hits whatever lies under it, which once switched on the `Inside fridge cleaning` extra (+150 Kč). | yes (ORD-FBAE64B7, Tue 17 Nov 10:00; ORD-4E968983, Sat 14 Nov 12:30 express; ORD-DAD2463F, Mon 16 Nov 10:00, 960 Kč) |
| Cash needs a saved card | BOOK-033 | A customer without a saved card: the `Cash` row is greyed, the line `Save a valid card for this currency to pay in cash.` sits under it and the slider stays pale. | yes (`e2e.a11140858.customer`) |
| Cash cap | BOOK-037 | With two cash bookings open (this account also held the iOS lane's), a third cannot be placed: after the slide the `Cash` row deselects and the slider turns pale, the app's answer to `order.cash_open_bookings_limit_reached`. Cancel one, choose `Cash` again. | yes |
| Order detail, feed | XCUT-011, TAKE-014 | After the take the detail reads `Confirmed`, `Step 2 / 5`, `<cleaner first name> accepted your booking`. The bell lists, newest first, `All done!`, `Cleaning in progress`, `Cleaner is on the way`, `Cleaner found!`, each with the order number. | yes |
| Order detail | | Orders tab: segments `All 11`, `Upcoming 2`, `Completed 5`, `Cancelled 4`; an order is a card with `#ORD-…`, status chip (`New`, `Confirmed`, `Completed`, `Cancelled`), date, address, service, price. Open it: `Step 1 / 5` `Booking received`, price breakdown, `Cancel order`. | yes |
| Cancel | CANCEL-001, CANCEL-002, CANCEL-005 | Order detail, `Cancel order`. The sheet `Cancel this cleaning?` quotes the fee first: `This booking is within the cancellation grace period. No cancellation fee.` Pick a reason chip (`Booked by mistake`), `Confirm cancellation`. The order shows `Cancelled`. | yes (CANCEL-002: free, inside the oops window, with a cleaner on the job; CANCEL-001: an untaken booking, where the sheet reads `Free cancellation — no cleaner has taken this booking yet.`) |
| Receipt | PAY-034 | A completed cash order: scroll to the end, with swipes that start above y 1600 (the three footer buttons cover everything below y 1850, and a swipe that starts on them scrolls nothing; the order then looks to end at `Instructions`); `Download receipt` with `RCP-2026-0013` and a button of the same name. The PDF opens in the emulator's Google PDF viewer (`01M7….pdf`). | yes |
| Register twice, consents (phase 2) | AUTH-002 | The row above, twice, on the run's `e2e.<run>.androidreg1@example.com` and `e2e.<run>.androidreg2@example.com` with the run's password (`typ "$(cat $PW_FILE)"`). Screenshot `Create account` before the tick (pale and inert), tick `Accept terms and privacy policy`, `TAPN=-1 tap "Create account"`: `Verify your email`. When both exist, `assist confirm`, `assist address` for each (section 6), sign in, the phone step with a number built from the run id and unique per account, and later `assist evidence`. | no |
| Card booking, save ticked or not (phase 2) | PAY-012, PAY-022, PAY-023 | Only if Step 4 offers `Card` / `Pay securely via Stripe` (section 6: the build needs the owner's Stripe key); if it offers `Cash` only, record all three as not drivable and stop. Book Bathroom, tomorrow, Normal. Step 4: tap `Card`; **androidreg1 ticks `Save this card for my next bookings`, androidreg2 leaves it empty**; tick the terms checkbox; `slide 2232`. PaymentSheet is Stripe's own UI (not driven before: read the dump): card `4242424242424242`, expiry `1234`, CVC `123`, then the pay button. Result `Booking received`; the order reads `Paid by card`, payment Paid, status `New`; the bell shows `Booking confirmed ✅` (`Your booking #ORD-… is confirmed. Tap to see the details.`). `Profile`, `Payments`, `Saved card`: androidreg1 `Visa •••• 4242`, androidreg2 `No card saved.` Then `assist evidence` with `E2E_ASSIST_EXPECT=card` or `nocard`, and cancel the paid order in the app so no card order stays open on the board. | no |
| Level step, picker range, express (phase 2) | BOOK-011, BOOK-019, BOOK-015 | Run customer, `Book`, any service, `Continue`. Step 2 `How clean is your home?` (`Dirtiness level`): none chosen and `Continue` inert (screenshot); `Increased` (`+15%`) and `Heavy` (`+30%`) move the footer `Continue · …` by 15 % and 30 % of the subtotal and print `Increased dirtiness surcharge (+15%)` / `Heavy dirtiness surcharge (+30%)`. Step 3 on a day-ahead chip: `Morning` starts at `08:00`, `Evening` ends at `19:45`. Express: only after the first-booking cancel below has freed a cash slot (the customer holds two). `Today`, a chip `<time>, Express +20%`, step 4 `Express surcharge (+20%)`, `Cash`, terms, `slide 2232`: `Booking received`. Runs only 04:00 to 15:45 UTC. Cancel that express order afterwards (untaken, free) so it does not sit on the board. | no |
| First booking cancel (phase 2) | CANCEL-002, CANCEL-005, CANCEL-008 | The run's `cash` order is the account's first booking, taken by the run cleaner in the book stage, and is booked in the first minutes of that stage (06:45 start). Between 15 and 60 minutes after it was booked (Android run: from about 07:06, to be safe until 07:45 UTC), `Cancel order`: the sheet `Cancel this cleaning?` prints `Cancelling within 60 minutes of booking is free, even after a cleaner has accepted.` and `No cancellation fee.` (screenshot); `Booked by mistake`, `Confirm cancellation`: `Cancelled`. Partner app, run cleaner: `Active` no longer lists it and the bell shows `Job cancelled`, `A job you accepted was cancelled: #ORD-…`. If the sheet prints 15 minutes or a fee, the window is gone: record CANCEL-002 as missed. | no |
| Translated refusal, five locales (phase 2) | XCUT-019 | Per locale: `adb root`, `adb shell setprop persist.sys.locale <tag>` with `en-GB`, `cs-CZ`, `sk-SK`, `uk-UA`, `ru-RU`, restart (`adb shell stop; adb shell start`, then force-stop the app), on a fresh install: the in-app language, once set, overrides the device locale at launch. Check a screen text changed, then sign in as `e2e.<run>.nobody@example.com` (this lane's run id; never registered) with the wrong password `Wrong-pass-1`; 7 s or more between attempts. The message equals the app's `error_validation_invalid_password`: `Incorrect password.` / `Nesprávné heslo.` / `Nesprávne heslo.` / `Невірний пароль.` / `Неверный пароль.` in both apps. Never "An error occurred" and never the key. Reset with `adb shell setprop persist.sys.locale en-GB`. | no |
| Log out | | Profile, swipe up twice, `Log out`, confirm `Log out` (`TAPN=-1`). | yes |
| Book by card | BOOK-028, PAY-012 | Needs the Stripe key (section 5). | no |

Partner app (cleaner).

| Step | Cases | Recipe | Proven |
|---|---|---|---|
| Register in the app | AUTH-033 | Sign In: `Sign up here`. Fields: First Name (300,705), Last Name (700,705), Email (540,890), Password (500,1080), Confirm Password (500,1430); tick `Accept terms and privacy policy`; `hidekb`; `Sign Up` (540,1843). | partly: the form was filled and submitted and the account exists, but DEV was not answering (section 2) and the button only returned to `Sign Up`; the screen after a successful sign-up was not seen |
| Registration lock | AUTH-034, AUTH-051 | Sign in with a cleaner who is not approved: the screen `Finish setup to start earning`, `0 of 4 steps complete`: `Profile` (`Complete profile`: Phone number, Date of birth, Street, City, ZIP code, Country, Registration number, Bank details, Passport / ID, Nationality), `Documents` (`Upload documents`), `Contract documents` (`Read and accept`), `Approval`; only `Sign out` leaves it. No board, no tabs. | yes |
| Upload a document | AUTH-037 | Profile, `My Documents` lists the country's checklist (`Identity card Required`, `Work permit Optional`, each `Approved` or `Not uploaded`). The `Add document` button (content-desc, 964,2221) opens `Take Photo`, `Choose from Gallery`, `Choose File`. `Choose File` opens the system picker: `adb push file.png /sdcard/Download/`, the picker's roots icon (`Show roots`), `Downloads`, the file; then the `Document type` dialog (`Identity card`, `Passport`, …, `Other`). | partly: up to the `Document type` dialog, then `Cancel` so the approved cleaner's documents stayed as they were |
| Sign in | AUTH-009 | After `Skip`: Email (500,1020), Password (400,1210), `typ`, `hidekb`, `tap "Sign In"`. Dashboard `Good morning, <name>`, bell `Notifications, N unread`, tabs. | yes |
| Board | TAKE-001, TAKE-002, TAKE-004 | `tap Orders`. Segments `Available, N jobs`, `Active`, `History`; sort `Soonest first`. A job is one clickable node whose content-desc is the sentence `Tuesday, 17 November, 10:00–10:02, you earn 300 Kč. Praha 1, under 3 km from home. Eco-Friendly Cleaning, 1 room, 1 bath. Cash: collect 960 Kč for the company.` Tap it with `TAPC=1 tap "Tuesday, 17 November"`. | yes |
| Open a job | TAKE-004 | Detail shows `#ORD-…`, `New`, `Exact address after you take the job`, `Property & scope`, `Shown after you take it` (street, customer name and phone, how to get in) and the pinned `Take this job`. | yes |
| Take with the contract | TAKE-010, TAKE-013 | `tap "Take this job"`. The sheet `Contract for Work` shows reward, frozen facts and the contract text. `slide 2200` on `Swipe to accept the contract for work` (do not scroll the sheet: a swipe not at the handle moves the text). After about 5 s `Job taken` with `Open job` and `Back to jobs`; the order is `Confirmed`, `Setting off`, with `Free to cancel until …`. | yes |
| Customer cancels a taken job | CANCEL-008 | After the customer cancels, the job leaves `Active` and the bell lists `Job cancelled`: `A job you accepted was cancelled: #ORD-…`. | yes |
| Invoices | PAYOUT-035, PAYOUT-056 | `tap Invoices`: `Total earned`, cards `INV-2026-00000N` with `Pending`, `Paid`, `Cancelled`. Open one: `Subtotal 575.00 Kč`, `Total`, `Cash set off -360.00 Kč`, `Bank transfer to you 215.00 Kč`, `Pay period`, `View period pay`, `Payment references`. | yes |
| Set off | EXEC-001, EXEC-003 | Orders, `Active`. The job card carries the action. Before the window it reads `You can set off from 11:30` (disabled). From 60 minutes before the slot: `Setting off, you can go now` and `Swipe to mark on the way`; `swipe 130 1568 1000 1568 700` changes the card to `On the way`. The customer's bell gets `Cleaner is on the way`. | yes |
| Start | EXEC-002 | The card now offers `Slide to start cleaning`, the same swipe. It becomes `Cleaning, 1 h 1 min left` with `Open job`; the detail reads `In progress`, `Step 3 / 4`, and the timeline `Setting off` / `Getting in` / `Cleaning` each `done at hh:mm`. | yes |
| Photos | EXEC-014, EXEC-015, EXEC-016 | `Open job`, section `Photos`: `Before · 0` and `After · 0`, each with `Add`. Tap `Add` under After (688,2080): the permission dialog `Allow Cleansia Partner to take pictures and record video?` (`While using the app`), then the emulator's camera (`com.android.camera2`): `Shutter`, then `Done`. The app uploads it: `After · 1` and, in the timeline, `After photo 11:32`. The pinned button before it reads `Take the after photo`. | yes (after photo). The before photo goes the same way from `Add` under Before (222,2080) and shows `Before · 1` (proven 2026-11-15) |
| Cash | EXEC-026, EXEC-029 | After the photo the pinned button reads `Mark 1 152 Kč as collected` (the stored total, not typed) with `Customer did not pay` beneath. `TAPC=1 tap "as collected"`, the dialog `Mark cash as collected?` (`This cannot be undone.`), `Yes, cash collected`. The timeline gains `Cash collected`. | yes (collected); `Customer did not pay` not tapped |
| Complete | EXEC-036, EXEC-020, PAYOUT-001, PAYOUT-002 | Pinned `Slide to complete`: `swipe 127 2232 1000 2232 700`. The job reads `Completed`, `Done · finished 11:32`, `Step 4 / 4`; the customer's name and phone stay with `Customer's details close tomorrow at 11:32`. Dashboard: `This period, not invoiced yet 300 Kč`, `Day 1 of 3`, `Invoiced when the period closes on 16 Nov 2026`. The customer's order shows `Completed`, `Payment Paid`, then the receipt. | yes (ORD-4E968983, cash 1,152 Kč, completed in 2 minutes, reward 300 Kč) |
| Customer did not pay (phase 2) | EXEC-029 | The run cleaner takes the run's `cashToCancel` order (about 09:50 UTC; the start window opens 60 minutes before its slot, so about 08:50). In the window: set off, start, after photo (`Add` under After (688,2080), camera works), then the pinned `Customer did not pay` (under `Mark … as collected`), the dialog `Customer did not pay?`, `Yes, report it`; toast `Reported. The job is completed and the customer has been told.` and the job reads `Completed`. The last step of the run customer's session: the customer then owes the total and cannot book. Give the run id to the suite runner: payroll writes the debt off. | no |
| Locks and rejection (phase 2) | AUTH-034, AUTH-049 | A cleaner registered in the app (`Sign up here`, `e2e.<run>.androidpartner@example.com`, the run's password), then `assist confirm`: sign in, the lock `Finish setup to start earning` reads `0 of 4 steps complete` with `Profile`, `Documents`, `Contract documents`, `Approval`. Then, after the people stage reports AUTH-049 passed, sign in as R2's applicant `e2e.<R2 run>.applicant@example.com` with R2's password: the `Approval` row shows `Your application was rejected. Contact support for details.`, the admin's reason `Photo of the identity card is unreadable` verbatim and `Contact support`. | no |

## 5. What could not be driven, and why

- **Card booking, saved card, PaymentSheet (BOOK-028/029, PAY-012/022/023)**: the build has no Stripe publishable key
  (the owner's, in `~/.gradle/gradle.properties`), so Step 4 offers `Cash` only. With the key the PaymentSheet is
  Stripe's own UI. Pay by card in the customer web app (the Playwright project) and read the order in the app.
- **Adding an address in the app**: `Add address` asks for location (tap `Don’t allow`), then a map with `Use this
  location`; with the placeholder Mapbox token the map is blank, the pin never resolves and the button does nothing.
  A saved address from the API is picked up on the next launch. With the owner's real token it was not tried.
- **Cleaner profile, identity document, contract documents, admin approval (AUTH-035, 041, 046, 049)**: the profile
  form and the document dialogs were not completed, and the approval is the admin API's. Only the lock screen and the
  document picker were driven.
- **The before photo, `Customer did not pay`, a premature `Complete` (EXEC-029, EXEC-043)**: not tapped, to keep the
  job on its happy path.
- **Cancel with a fee and the refund (CANCEL-003, 006, 007, 015, 016, 018)**: only the free path was driven. The fee
  tiers need a booking inside 24 h with no oops window left, and the customer's account holds cash bookings open.
- **Sign in with Google**: a system sheet; not driven. There is no Apple sign-in on Android.
- **Push**: `google-services.json` is the placeholder, so no FCM token; the bell feed (`Notifications`) is the substitute.
- **The map**: the placeholder Mapbox token leaves tiles blank; the `Navigate` button is not exercised.

## 6. The phase-2 run on Wednesday 18 November

The rows marked "(phase 2)" in section 4 run on Day 1 (Wed 18 Nov, UTC) on the lane's own `book` run (06:45, started by
the suite runner) and only on that run's accounts, plus R2's applicant for AUTH-049. Never take, cancel or touch an
order that is not the run's: R2's open orders are on every DEV board.

**Build: rebuild the customer app with the owner's Stripe key.** Mike puts `STRIPE_PUBLISHABLE_KEY` in his own
`~/.gradle/gradle.properties` before Day 1, and Gradle reads it itself (`customer-app/build.gradle.kts` takes the
Gradle property before the environment). **Never open, print or grep that file**, never run `./gradlew properties`,
and do not export the key. Build both apps as in section 1 (the partner app also changed in T-0933):

```bash
cd src/cleansia_android
export JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home
export ANDROID_HOME=$HOME/Library/Android/sdk
export MAPBOX_ACCESS_TOKEN=pk.e2e-placeholder-no-map     # fallback only: a MAPBOX_ACCESS_TOKEN in the owner's Gradle home wins
df -h /Users/michael                                      # under 3 GB free: stop
/Users/michael/cleansia-work.noindex/tools/withlock.sh gradle \
  ./gradlew :customer-app:assembleDebug :partner-app:assembleDebug --console=plain --no-daemon
```

Then install both on the lane's emulator (`withlock.sh emulator` around the start, section 1) and look at the booking's
Step 4 of any account that can reach it: **if `Card` / `Pay securely via Stripe` is there, the key is in**. If only
`Cash` is offered, the key is absent: record PAY-012 and the Android halves of PAY-022 and PAY-023 as "not drivable:
no Stripe key in the build" (the API halves of PAY-022 and PAY-023 are the suite's and are unaffected) and drive no card
row. Do the build and this check before 07:30 on Wed 18 (Tue 17 is fine if the key is already in). Section 5's line
about the missing key describes the 14 November build, not this one.

**The assist stage** (FOUNDATION, `e2e/README.md`): from `src/Cleansia.App` with Node 22, never with `E2E_SCRATCH`:

```bash
export PATH=/Users/michael/.codex/scratchpads/cleansia-audit-2026-10-07/tools/web-tooling/node_modules/node/bin:$PATH
cd src/Cleansia.App
env NX_DAEMON=false NX_NO_CLOUD=true E2E_CONFIRM_REF=master E2E_STAGE=assist E2E_RUN_ID=<android run> \
  E2E_ASSIST=<mode> <the extra variables below> \
  /Users/michael/cleansia-work.noindex/tools/withlock.sh heavy node node_modules/nx/dist/bin/nx.js e2e cleansia-platform-e2e
```

| Mode | Extra variables | When | Proves |
|---|---|---|---|
| `confirm` | `E2E_ASSIST_EMAIL=e2e.<run>.androidreg2@example.com` (the one whose line the log must hold) | once, after the customers and `androidpartner` are registered; not between 07:13 and 07:35 (the people stage dispatches the same script; a second dispatch in the same minute can read the other's log) | the DEV-only confirm SQL ran for the accounts registered in the app |
| `address` | `E2E_ASSIST_EMAIL=<androidreg1>`, then `<androidreg2>` (and the run customer if it has none) | after confirm | a saved address in Prague, because the emulator's map cannot place a pin without a real Mapbox token |
| `evidence` | `E2E_ASSIST_EMAIL=<androidreg1>`, `E2E_ASSIST_EXPECT=card`; again with `<androidreg2>` and `nocard` | after each account's card booking (leave `E2E_ASSIST_EXPECT` out when Card is absent: the cards are then only printed) | AUTH-002 (both consents granted and current, the `customer.account.register` audit row) and PAY-022 / PAY-023 |

`photo` is not needed on Android: the emulator camera works.

**Order on Wed 18 Nov (UTC).**

| UTC | Step |
|---|---|
| before 07:30 | build and install (above); partner signed in as the run cleaner (`e2e.<run>.cleaner`); customer signed in as the run customer |
| 07:35, first | First booking cancel (CANCEL-002, 005, 008): its 60-minute window closes about 07:50 |
| then | Register twice (AUTH-002), `assist confirm`, `assist address`, the phone step; level step, picker range, then express (BOOK-011, BOOK-019, BOOK-015) once the cancel has freed a cash slot |
| about 08:00 | the two card bookings and `assist evidence` (PAY-012, PAY-022, PAY-023), only with the key; cancel both paid orders |
| after the people stage reports AUTH-049 passed | Locks and rejection (AUTH-034, AUTH-049) |
| 08:50 to about 10:00 | Customer did not pay (EXEC-029) on `cashToCancel`, last: the customer owes afterwards; hand the run id to the suite runner, who passes it as `E2E_ANDROID_RUN_ID` to Day 2's payroll for the write-off |
| any time | Translated refusal in five locales on both apps (XCUT-019), with `e2e.<run>.nobody@example.com` (this lane's run id, never registered), not the run customer |

Shut the emulator down at the end (`adb emu kill`) and delete `customer-app/build partner-app/build core/build build`.
