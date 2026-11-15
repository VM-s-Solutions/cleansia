# iOS runbook: the customer and partner apps on DEV

How an agent builds `CleansiaCustomer` and `CleansiaPartner` against DEV, installs them on its own simulator and drives
the P1 mobile steps with AXe. Every step marked **proven** was run once on an iPhone 16 simulator (iOS 18.6, Xcode
26.3, AXe 1.8.0) against DEV on 2026-11-14. The cases a step covers are in `e2e/cases/`.

Never touch a simulator you did not create. Never run any of this against PRO: both `project.yml` files carry the DEV
`API_BASE_URL` (`api-cleansia-customer-mobile-weu-dev…`, `api-cleansia-partner-mobile-weu-dev…`), which is what you want.

## 1. Build and install

```bash
cd src/cleansia_ios
(cd CleansiaCustomer && xcodegen generate)
(cd CleansiaPartner  && xcodegen generate)

E=<evidence folder>                       # DerivedData and the .app files go here, never under the repo
UDID=$(xcrun simctl create e2e-ios "iPhone 16" com.apple.CoreSimulator.SimRuntime.iOS-18-6)
df -h /Users/michael                      # under 3 GB free: stop. Two builds leave about 5 GB of DerivedData.

for A in CleansiaCustomer CleansiaPartner; do
  /Users/michael/cleansia-work.noindex/tools/withlock.sh xcode \
    xcodebuild -project $A/$A.xcodeproj -scheme $A -configuration Debug \
    -destination "id=$UDID" -derivedDataPath $E/dd-$A build
done

xcrun simctl boot $UDID
xcrun simctl install $UDID $E/dd-CleansiaCustomer/Build/Products/Debug-iphonesimulator/CleansiaCustomer.app
xcrun simctl install $UDID $E/dd-CleansiaPartner/Build/Products/Debug-iphonesimulator/CleansiaPartner.app
xcrun simctl launch  $UDID cz.cleansia.customer      # partner: cz.cleansia.partner
```

- **Do not pass `CODE_SIGNING_ALLOWED=NO`.** An unsigned build has no keychain entitlement, so the token store silently
  saves nothing: sign-in succeeds, the next authenticated call is a 401, and the app drops back to Sign In with "Your
  session has expired" (seen on both apps). The default ad-hoc simulator signing works with `DEVELOPMENT_TEAM` empty.
- `Config/Local.xcconfig` (gitignored, never read it) supplies the Stripe publishable key. With it the booking screen
  offers Card; without it the card option is hidden and only cash is left.
- `xcodegen generate` and the first resolve rewrite `CleansiaCore/Package.resolved`; never commit that.
- Free the disk when done: keep the two `.app` folders, delete `dd-*` (`SourcePackages` alone is 1.5 GB per app), then
  `xcrun simctl delete $UDID`.

## 2. Driving with AXe

The apps carry **no accessibility identifiers**. A tap targets the accessibility **label** (a button's label is the
whole sentence, for example `Eco-Friendly Cleaning, Green cleaning using only eco-friendly and non-toxic products, from
600 Kč, + 180 Kč/room`) or a coordinate read from `describe-ui`. Prefer the label; use a coordinate for text fields,
which have none until they hold text.

```bash
ui() {  # on-screen elements: type|label|value|id|centre-x,centre-y
  axe describe-ui --udid $UDID | jq -r '[.. | objects | select(has("AXFrame")) | select(.type!="Slider" and .type!="Image")
    | select((.frame.x+.frame.width/2)>=0 and (.frame.x+.frame.width/2)<=393 and (.frame.y+.frame.height/2)>=40 and (.frame.y+.frame.height/2)<=852)
    | select((.AXLabel // "" | gsub(" ";"")) != "" or (.AXValue // "") != "")
    | "\(.type)|\(.AXLabel // "")|\(.AXValue // "")|\(.AXUniqueId // "")|\(.frame.x+.frame.width/2|floor),\(.frame.y+.frame.height/2|floor)"] | .[]'; }
shot() { xcrun simctl io $UDID screenshot "$1"; }
tap()  { axe tap --label "$1" --udid $UDID; }            # exact full label
tapxy(){ axe tap -x "$1" -y "$2" --udid $UDID; }
typ()  { printf '%s' "$1" | axe type --stdin --udid $UDID; }   # stdin, never argv
typpw(){ tr -d '\n' < "$PW_FILE" | axe type --stdin --udid $UDID; }   # PW_FILE: the run folder's password file
slide(){ axe swipe --start-x 48 --start-y 778 --end-x 372 --end-y 778 --duration 0.6 --udid $UDID; }
```

Rules learned the hard way:

- **After tapping a field, wait one second before `typ`.** Typed straight away, the first character is lost
  (`e2e.…` arrived as `2e.…`). Clear a wrong value with `axe key-sequence --keycodes 42,42,… --delay 0.03` (42 is Backspace).
- **Do not start a list swipe on a card.** A swipe that began at (196,650) on the service list selected the Upholstery
  card under it, so the quote read 750 Kč (Bathroom 300 + Upholstery 450) instead of one service. Scroll from a gap
  or check the footer price against the one service you tapped.
- **Describe again after every tap that focuses a field or opens a sheet.** Floating labels and the keyboard move the
  layout by 10 to 60 pt; a tap at the old coordinate lands on the wrong field and the typed text goes into it.
- Off-screen elements stay in the tree; judge visibility from the coordinates (the `ui` filter drops them) or a screenshot.
- Duplicates are normal: a time chip appears as two `Button` rows, a card as a `Button` plus its `StaticText` children.
- The display is 393 x 852 pt. A long list needs `axe swipe --start-x 196 --start-y 650 --end-x 196 --end-y 150`.
- `Don’t Allow` has a typographic apostrophe (U+2019); copy it exactly.
- Never print a password, in argv or in a screenshot: the sign-in field shows plain text until it is focused away.
- System dialogs live in another process but `axe tap --label` reaches them. **Save Password** has `Not Now`, which has
  no label in the tree: tap the coordinate (196, 790).
- The Mac clock is the simulator clock (Prague time on this Mac). A time chip reads in the market's zone, so a 11:00
  chip is 10:00 UTC in winter.

### First launch of either app

| Dialog | Tap |
|---|---|
| `“Cleansia” Would Like to Send You Notifications` | `Don’t Allow` |
| Partner only: onboarding carousel (`Welcome to Cleansia Partner`) | `Skip` |
| Customer, in the address map: `Allow “Cleansia” to use your location?` | `Don’t Allow` |

## 3. Accounts

Customer `e2e.<run>.customer@example.com`, cleaner `e2e.<run>.cleaner@example.com`, one password per run in the run
folder. Create them through the APIs (see the Running section of `e2e/README.md`); a registered account opens no session
until its e-mail is confirmed, and `@example.com` never receives the code, so run `sql-scripts/dev-e2e-confirm-accounts.sql`
through `execute-sql.yml` (the project's confirm script does it). A cleaner also needs the profile, an identity document
and the admin approval before the board shows work.

For a step that needs an approved cleaner or a customer with a saved card, use the accounts of a book-stage run: its
`e2e.<run>.cleaner` is approved and its `e2e.<run>.customer` has a saved card, and `.run/<run id>/state.json` holds
their password and the ids of their orders. Never use an account outside `e2e.%@example.com`. The steps below were
proven with `e2e.ios1.customer@example.com` (registered through the customer API, confirmed by that script) and, for
the cleaner, with an older approved account that was not an e2e one.

## 4. The P1 steps

Customer app, coordinates are iPhone 16 (393 x 852).

| Step | Cases | Recipe | Proven |
|---|---|---|---|
| Register in the app | AUTH-002, AUTH-010 | Sign In screen: `Register`. Fields by coordinate (First Name 79,220; Last Name 257,220; Email 196,284; Password 150,348; Confirm Password 150,486), type each. Tick `Accept terms and privacy policy`. `Sign Up`. The app lands on `Verify your email` (`Verification code, 6 digits`, `Verify code`, `Resend code`). No session exists yet. After the confirm script has run, `Back` to Sign In and sign in. | yes |
| Sign in | AUTH-009, AUTH-015 | Email field (196,268), `typ`, then Password (150,332), `typpw`, then `tap "Sign In"`. Home shows `Cleaning at, Select an address` and the tab bar `Home`, `Orders`, `Book`, `Rewards`, `Profile`. A fresh account first sees `Welcome aboard, <name>!` with the phone step: Country code `+420`, field `Phone number`, then `Save and continue` (or `Skip for now`). | yes |
| Quote | BOOK-001, BOOK-011 | `Book` (196,769) opens a four step sheet. Step 1 `What do you need?`: tap one service button by its label; the footer shows `Continue · 960 Kč`, the live server quote. Step 2 `How clean is your home?`: Continue stays disabled until one of `Normal` (196,190), `Increased` (+15%), `Heavy` (+30%) is picked; the price in the footer moves. | yes |
| Address | BOOK-065 | Step 3 `When & where`: a signed-in customer with a saved address gets it pre-filled. Without one: `Select address`, `Add address`, the map opens on Prague, `Use this address`, then `Save and continue` on `Confirm address`. | yes (map and confirm shown; the pre-filled path completed the booking) |
| Date and time | BOOK-019 | Date chips are labelled `Mon, 16`; `Today`, then a row of days. Part-of-day chips `Morning, 08:00–11:45`, `Afternoon`, `Evening`; a time chip is a button labelled `11:00`, tap by coordinate (a label hits two rows). `Continue · 960 Kč`. | yes |
| Book cash | BOOK-031, BOOK-033, TAKE-002 | Step 4 `Confirm booking`. Scroll to `How do you want to pay?`, tap `Cash, Pay on delivery`. Scroll to the very bottom and tick `Ask for the cleaning to start within the withdrawal period` (BOOK-048). Then `slide` the `Slide to pay · 960 Kč` handle from (48,778) to (372,778). Result sheet `Booking received` with `ORD-xxxxxxxx`, `Please pay in cash to the cleaner on the day.`, buttons `Back to home` and `View order`. Cash needs a saved card on the account, else the Cash row is refused. | yes (ORD-EA8E7B19, Mon 16 Nov 10:00 UTC, 960 Kč) |
| Orders list | | Tab `Orders`: segments `All`, `Upcoming`, `Completed`, `Cancelled`; each order is one `Button` labelled `#ORD-…, <status>, <date>, <address>, <service>, <price>`. | yes |
| Review prompt | | A completed order the account has not rated opens `How was your clean?` at launch. `Not now` closes it. | yes |
| Register twice, consents (phase 2) | AUTH-002 | The row above, twice, on the run's own two accounts `e2e.<run>.iosreg1@example.com` and `e2e.<run>.iosreg2@example.com` with the run's password (`typpw`). Screenshot `Sign Up` before the tick (dimmed; a tap leaves the form where it is), tick `Accept terms and privacy policy`, `Sign Up`: `Verify your email`. When both exist, `assist confirm`; later `assist evidence` for each (section 6). | no |
| Card, save ticked or not (phase 2) | PAY-022, PAY-023 | First sign-in of each account: the phone step (`Phone number`, nine digits built from the run id and unique per account: another account holds `+420777123456`), then `Add address`, `Use this address`, `Save and continue` (the iOS map works). Book Bathroom, tomorrow, Normal. Step 4: `Card, Pay securely via Stripe`; **iosreg1 ticks `Save this card for my next bookings`, iosreg2 leaves it empty**; tick the withdrawal box; `slide`. PaymentSheet: `Card number` `4242424242424242`, `1234`, `123`, `Pay …`; screenshot the sheet before paying (no Stripe "save" box on either). Result `Booking received`, `Paid by card`. `Profile`, `Payments`, section `Saved card`: iosreg1 `Visa •••• 4242`, iosreg2 `No saved card. Add one for cash bookings.` Then `assist evidence` with `E2E_ASSIST_EXPECT=card` (iosreg1) or `nocard` (iosreg2), and cancel the paid order in the app (`Free cancellation — no cleaner has taken this booking yet.`) so no card order stays open on the board. | no |
| Level step, picker range (phase 2) | BOOK-011, BOOK-019 | Run customer, `Book`, any service, `Continue`. Step 2 `How clean is your home?`: no row selected and `Continue` disabled (screenshot). Tap `Increased` (`+15%`): the footer `Continue · …` rises by 15 % of the service subtotal and the picker prints `Increased dirtiness surcharge (+15%)`; `Heavy` (`+30%`) prints `Heavy dirtiness surcharge (+30%)`; pick `Normal` again. Step 3 `When & where`, a day-ahead chip: `Morning` shows `08:00` as its first chip, `Evening` ends at `19:45` (screenshot each). Close the sheet without booking. | no |
| Express booking (phase 2) | BOOK-015 | First open the run's `cashToCancel` order and cancel it (`Cancel order`, `Booked by mistake`, `Confirm cancellation`), because the run customer holds two cash bookings and a third is refused. Then `Book`, Bathroom, Normal, `Today`, a time chip that reads `Express +20%`, step 4 line `Express surcharge (+20%)`, `Cash, Pay on delivery`, withdrawal box, `slide`: `Booking received`. Runs only 04:00 to 15:45 UTC. Keep this order: the next two rows use it. | no |
| Detail after a take (phase 2) | F-038 | `View order` on the express order: status `New` (screenshot). On the partner app, signed in as the run cleaner, take it with the contract (partner table below). Back in the customer app wait 40 s without leaving the detail, screenshot, then go back to `Orders`, reopen it, screenshot. The code predicts `New` until the reopen and `Confirmed` after; record what the screenshots show. | no |
| Cancel sheet, cleaner told (phase 2) | CANCEL-005, CANCEL-008 | The same order, inside 15 minutes of booking (it is not the run customer's first booking): `Cancel order`. The sheet `Cancel this cleaning?` prints `This booking is within the cancellation grace period.`, `Cancelling within 15 minutes of booking is free, even after a cleaner has accepted.` and `No cancellation fee.` (screenshot). `Booked by mistake`, `Confirm cancellation`: `Cancelled`. Partner app: `Orders`, `Active` no longer lists it, and the `Notifications` bell shows `Job cancelled`, `A job you accepted was cancelled: #ORD-…`. If 15 minutes passed before the cancel, the sheet prints a fee instead: record CANCEL-005 as missed and read the cleaner row anyway. | no |
| Rating prompt, once (phase 2) | F-021 | After the suite runner's 08:05 execute has completed the run's cash job (its log says so): `xcrun simctl terminate $UDID cz.cleansia.customer`, launch, sign in as the run customer if asked. `How was your clean?` opens (screenshot); `Not now`. Terminate and launch twice more: no prompt either time. | no |
| Translated refusal, five locales (phase 2) | XCUT-019 | Per locale on a fresh install: `xcrun simctl uninstall $UDID cz.cleansia.customer`, `install`, then `xcrun simctl launch $UDID cz.cleansia.customer -AppleLanguages "(<tag>)" -AppleLocale <tag>_<CC>` with `en` `en_GB`, `cs` `cs_CZ`, `sk` `sk_SK`, `uk` `uk_UA`, `ru` `ru_RU` (the in-app language, once chosen, overrides these). Dismiss the first-launch dialogs, sign in as `e2e.<run>.nobody@example.com` (this lane's run id; never registered) with the wrong password `Wrong-pass-1`. The message equals the catalog value of `error.validation.invalid_password`: `Incorrect password.` / `Nesprávné heslo.` / `Nesprávne heslo.` / `Невірний пароль.` / `Неверный пароль.` Never "Something went wrong" and never the key. Leave 7 s or more between attempts. The partner app does the same with `cz.cleansia.partner` (same five values). | no |
| Log out | | `Profile`, swipe up twice, `Log out`, confirm `Log out` in the alert (264,502). | yes |
| Book by card | BOOK-028, PAY-011 | Same sheet; tap `Card, Pay securely via Stripe`, tick the withdrawal box, `slide`. The Stripe PaymentSheet (badge `TEST`) opens and is in the tree: tap `Card number` (128,490), `typ 4242424242424242`, then `typ 1234` (the expiry field takes focus by itself and reads `12/34`), then `typ 123` (CVC), then `Pay 960,00 CZK` (196,746; read it from `describe-ui`). The result sheet says `Booking received` and `Paid by card`. | yes (ORD-C6520164, 960 Kč) |
| Cancel | CANCEL-001, -003, -006, -007 | `Orders`, open the order (wait for the spinner to finish), `Cancel order`. The sheet `Cancel this cleaning?` shows the fee for now (free inside 15 minutes of booking or while no cleaner has taken it; 480 Kč of 960 Kč inside 4 hours); pick a reason (`Booked by mistake`), `Confirm cancellation`; toast `Order cancelled.`, status `Cancelled`. `Keep my booking` closes the sheet and cancels nothing. The order sheet also has `Report issue`. | yes (free cancel of an untaken cash job; fee quote read and declined on a taken card job) |

Partner app (cleaner).

| Step | Cases | Recipe | Proven |
|---|---|---|---|
| Sign in | AUTH-009 | Sign In screen fields: Email (196,361), Password (150,425), `tap "Sign In"`. Dashboard `Good morning, <name>`, tab bar `Dashboard`, `Orders`, `Invoices`, `Profile`. A card `Which new jobs should we tell you about?` offers `Keep every job` or `Pick a distance`. | yes |
| Board | TAKE-001, TAKE-002, TAKE-004 | `tap "Orders"`. Segments `Available`, `Active`, `History`. A job is one `Button` labelled `Monday, November 16, 11:00–11:02, you earn 300 Kč. Praha 1, under 3 km from home. Eco-Friendly Cleaning, 1 room, 1 bath. Cash: collect 960 Kč for the company. Opens the job.` The board shows the area band only; the street arrives with the take. | yes |
| Open a job | TAKE-004 | Tap the card. Detail shows `#ORD-…`, status `New`, `Exact address after you take the job`, `Property & scope`, `Shown after you take it`, and the pinned `Take this job`. | yes |
| Take with the contract | TAKE-010, TAKE-013 | `tap "Take this job"`. The sheet `Contract for Work` shows the reward and the frozen facts. `slide` the `Swipe to accept the contract for work` handle from (44,778) to (372,778). The button reads `Accepting…` for about 15 s on DEV; then `Job taken` (`Open job`, `Back to jobs`) and the order is `Confirmed`, step `Setting off`, with `Navigate`, `Call`, `SMS` and the customer's name and phone. | yes |
| Set off | EXEC-001, EXEC-003 | Detail, pinned action `Swipe to mark on the way`; `slide 44` (the handle starts at x=44). Order `On the way`, step 2 / 4; the customer's list shows `On the way`. Before the window opens the bar reads `You can set off from <time>`. | yes |
| Start | EXEC-002 | `Slide to start cleaning`, `slide 44`. Order `In Progress`, step 3 / 4, action `Take the after photo`. The window opens 60 minutes before the slot. A cleaner can have **one** order in progress: starting a second shows the toast `You already have another order in progress. Finish it before starting a new one.`. | yes (one job per cleaner; the second start was refused as above) |
| Photos | EXEC-014, EXEC-015 | `Take the after photo`. | no (see below) |
| Cash | EXEC-026, EXEC-036, PAY-034 | `Mark cash collected`, then `Yes, cash collected`. Order: after photo, cash, then `Slide to complete`. `Customer did not pay` is EXEC-029. | no |
| Complete | EXEC-036, EXEC-037, PAYOUT-001 | `Slide to complete`, toast `Order marked as completed`. | no |
| Rejected lock (phase 2) | AUTH-049 | Sign in as R2's applicant `e2e.<R2 run>.applicant@example.com` with R2's password (its `state.json`), after the people stage reports AUTH-049 passed. The lock `Finish setup to start earning` shows the row `Approval` with `Your application was rejected. Contact support for details.`, the admin's reason `Photo of the identity card is unreadable` verbatim, and the button `Contact support` (it opens the mail composer, which a simulator may lack: record the screen only). | no |

## 5. What could not be driven, and why

- **Cash, complete (EXEC-026, -036, -037)**: they need the after photo first, and the simulator cannot take one, so a
  started job stays `In Progress`. Because a cleaner may have only one order in progress, that also stops every other
  taken job of the run from starting: start the job you can finish and treat the others as not startable. A started job that is never completed
  stays `In Progress` past its slot (still so 45 minutes after it), and the lock with it.
- **Photos on the simulator**: the partner app takes order photos from the camera only
  (`PhotosSection.openCamera`). On the iOS 18.6 simulator `Take the after photo` asks for camera access (`Allow`), then
  opens a grey viewfinder whose `Take Picture` button adds no photo; `Cancel` returns. There is no "camera unavailable"
  alert. An agent uploads the photo through the partner mobile API instead (`POST /api/Order/UploadPhoto`, body
  `orderId`, `photoType` 1 before or 2 after, `fileName`, `contentType`, `fileData` base64), then continues in the
  app; it was not made on 2026-11-15. Document photos at registration have a library path and are not affected.
- **Sign in with Apple and Google**: system sheets; not driven.
- **Push, Live Activity**: no APNs token on a simulator ("No APNS token specified before fetching FCM Token" is logged
  and harmless); the feed (`Notifications` bell) is the substitute.

## 6. The phase-2 run on Wednesday 18 November

The rows marked "(phase 2)" in section 4 run on Day 1 (Wed 18 Nov, UTC) on the lane's own `book` run (06:40, the suite
runner starts it; its id is `202611180640` if it starts on time) and only on that run's accounts, plus R2's applicant
for AUTH-049. Never take, cancel or touch an order that is not the run's: R2's open orders are on every DEV board.

**Builds: reuse phase 1, build nothing.** The two apps built for phase 1 are still on disk, and both carry the Stripe
key from `Local.xcconfig` (the phase-1 run booked by card with them):
`/Users/michael/cleansia-work.noindex/e2e-phase1/evidence/ios/apps/CleansiaCustomer.app` and `…/CleansiaPartner.app`.
Create the lane's own simulator (section 1), boot it, `xcrun simctl install $UDID <that .app>` for both, and skip the
`xcodebuild` loop. They predate T-0933's partner job-detail change; no row here reads that screen. Rebuild (section 1)
only if a folder is gone, and never read `Local.xcconfig`. Keep nothing new on disk: screenshots go to the lane's own
evidence folder, outside the repo.

**The assist stage** (FOUNDATION, `e2e/README.md`): `assist` does what the app cannot, for the lane's run, from
`src/Cleansia.App` with Node 22, never with `E2E_SCRATCH`:

```bash
export PATH=/Users/michael/.codex/scratchpads/cleansia-audit-2026-10-07/tools/web-tooling/node_modules/node/bin:$PATH
cd src/Cleansia.App
env NX_DAEMON=false NX_NO_CLOUD=true E2E_CONFIRM_REF=master E2E_STAGE=assist E2E_RUN_ID=<ios run> \
  E2E_ASSIST=<mode> <the extra variables below> \
  /Users/michael/cleansia-work.noindex/tools/withlock.sh heavy node node_modules/nx/dist/bin/nx.js e2e cleansia-platform-e2e
```

| Mode | Extra variables | When | Proves |
|---|---|---|---|
| `confirm` | `E2E_ASSIST_EMAIL=e2e.<run>.iosreg2@example.com` (the one whose line the log must hold) | once, after both registrations; not between 07:13 and 07:35 (the people stage dispatches the same script and a second dispatch in the same minute can read the other's log) | the DEV-only confirm SQL ran for the accounts registered in the app; then a sign-in of each works |
| `evidence` | `E2E_ASSIST_EMAIL=<iosreg1>`, `E2E_ASSIST_EXPECT=card`; again with `<iosreg2>` and `nocard` | after each account's card booking | AUTH-002 (both consents granted and current, the `customer.account.register` audit row) and PAY-022 (Visa 4242) / PAY-023 (none) |
| `photo` | `E2E_ASSIST_ORDER=<order id>` | optional, not planned: only if a row needs a started iOS job to be completed | the after photo the simulator camera cannot take |

`address` is not needed on iOS: the map works.

**Order on Wed 18 Nov (UTC).** The suite runner owns every other time in `e2e/README.md`; the lane's rows are:

| UTC | Step |
|---|---|
| before 07:35 | simulator created, both apps installed; partner signed in as the run cleaner (`e2e.<run>.cleaner`, the run's password from `state.json`); customer signed in as the run customer |
| 07:35 | Register twice (AUTH-002), then `assist confirm`; level step and picker range (BOOK-011, BOOK-019) |
| about 07:50 | iosreg1 and iosreg2 card bookings with `assist evidence` (PAY-022, PAY-023); cancel both paid orders |
| after the people stage reports AUTH-049 passed | Rejected lock (AUTH-049) |
| from 07:50, before 15:45 | cancel `cashToCancel`, then Express booking, Detail after a take, Cancel sheet (BOOK-015, F-038, CANCEL-005, CANCEL-008) in one 15-minute stretch |
| after 08:05 | Rating prompt (F-021) once the suite runner's execute of the run's cash job is done |
| any time | Translated refusal in five locales on both apps (XCUT-019), 7 s or more between attempts |

