# Partner Onboarding

The partner onboarding process ensures that only verified and approved cleaning partners can access the platform. The process involves multiple steps, from account creation to admin approval.

## Onboarding Flow

```
1. Create Account (/register)
   ↓
2. Email Confirmation (/confirm-email)
   ↓
3. Login (/login)
   ↓
4. Profile Completion (/profile)
   ↓
5. Document Upload (/profile)
   ↓
6. Admin Review & Approval (admin-app)
   ↓
7. Full Platform Access
```

## Step 1: Create Account

The partner registration page (`/register`) collects:

- First name
- Last name
- Email address
- Password (with confirmation)
- The market, when more than one is offered (the default market otherwise)
- Agreement to the terms

The phone number is not asked here. The profile asks for it: on the apps, in its first step, Personal
([Step 4](#step-4-profile-completion)). On the apps the sign-up button can be tapped at any time but
while the request is in flight, and a tap names each missing or invalid field under it
([What each step checks before it sends](#step-checks)).

**The password can be the phone's own suggestion** (since the fix plan of 2026-10-25). The apps'
sign-up marks the e-mail as the account's user name and both password fields as a new password —
`.username` and `.newPassword` on iOS, `NewUsername` and `NewPassword` on Android — so iCloud Keychain
or Google Password Manager offers a strong password and saves it, and Sign in fills it back. No password
rules are declared: the default suggestion already meets the rule the list under the field shows, at
least 8 characters with a letter and a digit. A letter of any alphabet counts, on the server and the web
as in the apps (since the same day; until then the server and the web wanted a letter from a to z, so a
password such as *Пароль2024* showed every rule green and was refused). On iOS the suggestion needs the
app's associated domain to be live → [Strong passwords](/mobile-app/overview#webcredentials).

**Registering again before confirming.** A registration of an address nobody has confirmed yet sends a
fresh code. When it brings a password other than the one stored, the account is left with **no
password** (since the same day): whoever confirms the address proves they hold the mailbox, not which
of the two passwords is theirs. Confirmation now replaces the hash with the mailbox holder's chosen
password. A legacy confirmation with no password leaves none and needs *Forgot password* once.
The same re-registration password again is kept only until confirmation replaces it.
→ [Re-registration](/api/authentication#register)

::: info
The registration route is protected by the `guestGuard` -- already authenticated partners are redirected to `/orders`.
:::

After submission, the backend:
1. Creates the user account
2. Creates an associated employee record with `ContractStatus.Pending`
3. E-mails a 6-digit confirmation code

## Step 2: Email Confirmation

After registration, partners are redirected to `/confirm-email`. The confirmation flow:

1. Partner receives an email with a 6-digit confirmation code
2. The confirm-email page presents a 6-digit code input component with individual digit fields that auto-advance to the next field on entry; the Android and iOS apps draw six boxes over one real text field
3. Immediately after registration it reuses the password held in memory. A later arrival, including
   the unconfirmed-login detour, asks for a chosen password and matching confirmation under the
   existing password policy.
4. Code submission waits for the password form when needed. The code, e-mail and chosen password are
   sent to the backend; a code can be pasted on all three clients (below).
5. On success, the chosen password replaces the registration hash and the e-mail is confirmed.
   No password is carried in the confirmation URL or persistent storage.

**A long press on the boxes reaches a real field and its Paste, on partner web, Android and iOS**
(since 2026-10-24). On partner web each box is an input of its own, and a pasted code fills all six.
On the apps the boxes only draw one text field laid over them, so a tap anywhere on them brings the
keyboard back and a long press opens the field's edit menu; until then the boxes sat over a field 1pt
(1dp) wide, so a long press found no field, and on iOS a code could not be pasted at all. Under the
boxes iOS shows the system's Paste button, an icon alone, and Android a *Paste* text button while the
clipboard holds text, because the iPhone keyboard has no clipboard and not every Android keyboard offers
one on a number pad. **A whole code pasted, or put in by AutoFill or a keyboard's clipboard suggestion,
replaces a partial one** instead of being added to it, so `654321` pasted after a typed `12` is sent as
`654321`, not run into `126543`. Only the digits `0`–`9` are kept. What happens to less than a whole
code depends on the gesture and the platform:

- **The apps' Paste button replaces what was typed** with the clipboard's code. A fragment shows as
  itself, so `3456` on the clipboard after a typed `12` shows `3456`, and it is submitted only once the
  boxes hold six digits: the button never completes a code from digits already typed and spends an
  attempt on it. A clipboard with no digits changes nothing.
- **The field's own Paste, from the long press, is editing**: a fragment goes in at the caret, which the
  apps keep after the code, and a whole code replaces.
- **On partner web a fragment is written from the box it lands in**, and the boxes before it keep their
  digits: `12` typed, then `34` pasted into the third box, gives `1234`. The focus then moves to the box
  after the fragment, or stays on the last box, so the digits typed next continue the code. Each box
  takes up to 13 characters, twice a code and one more, so a code spaced as `123 456` and put in beside
  a digit a box already holds keeps all six digits. The page sends one confirm at a time: a code the
  boxes complete again while the first confirm is in flight, as AutoFill can deliver it, is not sent a
  second time.

An old client that confirms with no password leaves the account without one. Updating before
confirmation avoids this fallback; updating afterwards does not restore the hash, so Forgot password
is needed before password sign-in. → [Mailbox confirmation](/flows/auth-and-identity#confirmation-password)

VoiceOver and TalkBack read the apps' field as *Verification code, 6 digits*; the boxes drawn over it
are hidden from them.
→ [Mobile app features](/mobile-app/features#shared-versus-app-owned)

::: warning
Partners cannot log in until their email is confirmed. The login flow checks `isEmailConfirmed` and redirects back to the confirmation page if false.
:::

## Step 3: Login

After email confirmation, the partner logs in with email and password. The authentication flow is similar to the customer app:

1. `authService.login(email, password)` returns a `JwtTokenResponse`
2. Session tokens are stored
3. Partner is redirected to `/orders`

## Step 4: Profile Completion

Once logged in, partners need to complete their profile via the `/profile` page.
`Employee.IsProfileComplete()` is the authoritative list:

- Personal details — first name, last name, email, phone, date of birth. The phone is first asked
  here, not at sign-up, and the apps' Personal step does not move on without it
- Address — street, city, ZIP, country
- **A payout destination** (see below), passport ID, nationality, registration number
- Business identity:
  - Entity type (Natural Person or Legal Entity)
  - Registration number -- mandatory
  - Legal Entity Name -- required only when Entity type is Legal Entity, which only an operator can
    set: a cleaner's own save cannot make them a company (`employee.legal_entity_not_accepted`). Since
    2026-10-24 an administrator's edit that would leave a company without its registered name is
    refused, and a company row stored without one shows the cleaner the locked field and the reason, to
    contact support → [What a cleaner's profile accepts](/product/business-rules#cleaner-profile-fields)

The **label on the registration number comes from the business country**, not from the app's
language. `CountryConfiguration` has carried `RegistrationNumberLabel` since it was seeded — CZ and SK
say "IČO" — but nothing read it, so every client hardcoded the Czech word in its own translation files
and a Polish or Ukrainian partner read "IČO" for a registry that has no such thing. The clients now ask
the country and fall back to a neutral "Registration number" only where the platform holds no
configuration: correct everywhere, precise nowhere, which is exactly what a fallback should be.
Flattening every country to the neutral term would have cost CZ and SK the word their own registries
use. The configuration also carries a `VatNumberLabel`, which `GetCountryFieldLabels` still returns;
no cleaner form has a VAT number, and the employee record has none.

**A Czech IČO is looked up in ARES** (since 2026-10-04). After the format check, the cleaner's save —
the partner web profile and the apps' identification section — is refused when ARES does not hold the
number (`validation.registration_number.not_registered`); when ARES does not answer, the save goes
through. The save asks the register of the cleaner's work country once they are approved, and before
that of their address country, never of the business country the app sends, so it asks the register
approval asked. Approval asks again and is stricter: the business must be live and a trade licence in
force, and an ARES outage refuses it until the admin tries again. **An approved cleaner who changes the
number is held to approval's check on that save** (owner ruling 2026-10-05): an ended business
(`employee.business_ceased`), no trade licence in force (`employee.trade_licence_inactive`) and an ARES
outage (`employee.business_registry_unavailable`) refuse the whole save, which writes nothing — on the
partner web that is the whole profile. Re-saving the same number, or changing it before approval, keeps
the existence check alone. Nothing ARES answers is stored, and no name is matched. The partner web and
the Android and iOS partner apps show all four refusals in five languages, in the cleaner's voice (the
three stricter ones since 2026-10-05). Local runs, the tests and the deployed DEV hosts switch the lookup off (`Ares:Enabled`); only
prod checks ARES.
→ [The business register](/product/business-rules#business-register)

::: info Not part of the completeness check
**Emergency contacts** are optional. **Documents** are handled separately by the registration lock.
**There is no weekly schedule to fill in** — dispatch is a first-come pull board, and the schedule
the employee record used to carry was read by nothing, so on 2026-09-20 it was removed: the admin
detail section, the two update endpoints, the `availability` and `hasSetAvailability` wire members
and the column went together.
:::

::: tip Bank details live in their own record (ADR-0034)
The payout destination is an `EmployeePayoutDetails` row, not a column on the employee. The
completeness gate reads a scalar — `HasPayoutDetails || IBAN` — where `IBAN` is a **legacy** column
kept because there is no backfill for cleaners onboarded before the new record existed; dropping it
would mark them incomplete and lock them out of the partner surface overnight.

Admins see a **masked** view by default; the plaintext is behind a separate, audited reveal action.
:::

**The bank's country decides the form**, on partner web, Android and iOS alike (owner ruling
2026-10-02). The form shows the three boxes or one IBAN field, never both:

| Bank country | The form | Sent with the bank country |
|---|---|---|
| CZ or SK, or one the list cannot name (none picked yet, or the country list failed to load) | the three boxes, *prefix – number / bank code* | the parts only; the server derives the IBAN |
| any other | one IBAN field | the IBAN only |

The clients decide by the country's `isoAlpha2`, because the country list does not carry
`CountryConfiguration.PayoutScheme`. CZ and SK are the two countries of the `CzskDomesticWithIban`
scheme ([ADR-0034](/decisions/adr-0034) D5.2). Until then every cleaner got the Czech boxes with an
optional IBAN field under them, so a German or Austrian bank had no form of its own. That field also
loaded the stored IBAN, which the server had derived from the old parts, and sent it back with edited
parts, so a Czech cleaner changing their account was refused with `validation.payout.iban_mismatch`.

**The IBAN field** shows the IBAN in groups of four and in capitals. It sends it without the spaces,
and stops at 34 characters, the ISO 13616 maximum; on iOS it opens the ASCII keyboard. Its hint reads
*Your full IBAN, starting with your bank's country code* (`iban_helper` on the apps,
`pages.profile.iban_hint` on the web). Before anything is sent, the client checks the IBAN as the
server's `IbanCalculator` does, in the order a cleaner can act on:

1. **The country.** Its two letters must be the bank country's. The message is the copy of
   `validation.payout.iban_country_mismatch`. An entry that does not start with two letters is not an
   IBAN at all (step 3's message).
2. **The length.** The IBAN must have the length that country's IBANs have. The message names that
   length (`iban_wrong_length` on the apps, `pages.profile.iban_wrong_length` on the web). This message
   exists only on the clients, because the server reports a wrong length as `invalid_iban`. A country
   outside the registry takes the generic 15–34. Each client's tests read the registry table from
   `IbanCalculator.RegistryLengths`, entry for entry.
3. **The shape and the check digits.** The ISO 7064 mod-97 check must hold. The message is the copy of
   `validation.payout.invalid_iban`.

On partner web, Save is enabled once the bank country is picked and the IBAN typed; on the apps it can
be tapped at any time but while a save is in flight ([What each step checks](#step-checks)). The first
Save with a problem sends nothing and shows the message under the field, which then follows the edits.
The server checks the IBAN again. One of its
rules has no client twin: a card number typed into the field is refused on the device as not an IBAN,
so the server's more specific `looks_like_card` never comes back.

**A Czech or Slovak account can be pasted whole into any one of its three boxes** — prefix, number or
bank code — on partner web, Android and iOS, and it lands in all three. Each box used to keep its own
digits and clamp, so pasting `12321414/3545` into the number gave the number `1232141435` and an empty
bank code. The clients read three shapes:

| Pasted | Lands as |
|---|---|
| `19-2000145399/0800`, `2000145399/0800` | prefix, number and bank code; a missing prefix clears the old one |
| `19-2000145399` | prefix and number; the bank code already there stays |
| `CZ65 0800 0000 1920 0014 5399`, or an SK IBAN | the same three parts, with the IBAN's zero padding dropped |

Spaces of every kind are ignored (a non-breaking one included) and an en or em dash counts as a hyphen.
**The bank code of a written-out account is exactly four digits**, as every Czech and Slovak bank code
is (since 2026-10-25): a slash followed by one to three digits, or by five or more, is no account, so
`19-2000145399/080` is not split. An IBAN always gives a four-digit bank code.

**One rule decides what an edit of a box does, on partner web, Android and iOS alike** (since
2026-10-25). The prefix holds at most 6 digits, the number 10 and the bank code 4. Steps 1 and 3 read
what the edit put in, which the clients work out differently (below):

1. **An account first.** If the edit put in more than one character and what it put in is one of the
   three shapes, with a bank code, where it has one, of exactly four digits, it is split over the three
   boxes, whichever box it landed in and even where its digits would fit that box, so `12345/0800`
   pasted into the empty number gives the number `12345` and the bank code `0800`. One character is
   never an account.
2. **Otherwise new text that fits the box is kept**, as typed or pasted, and the box then keeps only its
   digits, on the apps by its digit clamp and on partner web by the profile page: `19` pasted into the
   empty prefix box stays the prefix, and `12345/080`, which is no account, pasted into the empty number
   is the number `12345080`.
3. **Otherwise the new text is too long for the box**, and what the edit put in decides:
   - **one character is typing**: the box keeps the first digits of its new text that fit, so a digit at
     the end of a full box changes nothing, and one before or inside it pushes out the box's last digit;
   - **digits that fit the box replace what it held**: `5500` pasted after the bank code `0800` gives
     `5500`, and the number already entered stays;
   - **digits too long for the box, up to the number's ten, are the account number**, and the box keeps
     what it held: `2000145399` pasted into the prefix box becomes the number, and the prefix and bank
     code stay as they are;
   - **anything else leaves the box as it was**, eleven or more digits included, and so does an account
     whose bank code is cut short, which is too long for any box: `19-2000145399/080` or
     `2000145399/080` pasted into an empty one, and on partner web into any box, whatever it holds.

Until 2026-10-24 a bare number pasted into any box became the account number, on the apps from two
digits and on the web from one, so a bank code pasted into its own box replaced the number already
entered, and a real prefix such as `19` pasted into the prefix box went to the number.

**What the edit put in is read from the paste on partner web, and worked out on the apps.**

- **Partner web** reads the pasted text from the paste event, so it knows what went in whatever the box
  held and wherever the caret or the selection was. Typing is the browser's own: each box is held to its
  `maxlength`, so a digit typed into a full box is refused wherever the caret is, and only a
  one-character paste goes through the typing step.
- **The apps** see only the box's text before and after the edit, and typing goes through the box's
  digit clamp. Their *run* is the new text less the longest start and the longest end it shares with the
  old text, the two never overlapping. When the run is longer than one character, step 1 tries four
  readings, in this order, and splits the first one that is an account:
  1. when the new text starts with the old text, the text after it: a paste at the end, or into an
     empty box;
  2. when the new text ends with the old text, the text before it: a paste at the start;
  3. the whole new text: a paste over the whole box, as after Select All;
  4. the run: a paste in the middle.

  Step 3 reads the run alone.

**A run of one character is never read as an account, on both apps**, so a `-` or `/` typed among a
box's digits is dropped by the digit clamp like any other non-digit, and nothing moves between the
boxes: a `-` typed after the `1` of the prefix `12345` leaves the prefix `12345` (rows 25 and 25b
below). The price is a paste whose run is a single character: `123456/0800` pasted over the whole number
`1234560800` differs from it by the `/` alone, so the apps read it as typing and the number stays as it
was, where partner web splits it.

**The three clients pin one case list, row for row**: iOS in `CleansiaBankAccountFieldPasteTests`,
Android in `CleansiaBankAccountInputPasteTest` and partner web in
`cleansia-bank-account.component.spec.ts`. Each row starts with the other two boxes empty. The one
character of rows 13 and 17 is typed on the apps and pasted on the web, where a digit typed into a full
box is refused. Rows 25 and 25b are typing, which partner web leaves to the browser, so only the apps
pin them.

| # | Box | It holds | The edit | The boxes then hold | iOS keyboard |
|---|---|---|---|---|---|
| 1 | bank code | nothing | `0800` pasted | bank code `0800` | stays |
| 2 | bank code | `0800` | `5500` pasted after it | bank code `5500` | closes |
| 3 | bank code | `0800` | `5500` pasted before it | bank code `5500` | stays |
| 4 | bank code | `0800` | `55` pasted over its `08` | bank code `5500` | stays |
| 5 | bank code | `08` | `00` pasted after it | bank code `0800` | stays |
| 6 | bank code | `08` | `0800` pasted after it | bank code `0800` | closes |
| 7 | bank code | `0800` | `2000145399` pasted after it | number `2000145399`, bank code still `0800` | closes |
| 8 | bank code | nothing | `2000145399` pasted | number `2000145399`, bank code still empty | closes |
| 9 | prefix | `12345` | `67` pasted after it | prefix `67` | closes |
| 10 | prefix | nothing | `19` pasted | prefix `19` | stays |
| 11 | number | nothing | `19-2000145399/0800` pasted | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 12 | number | nothing | `CZ65 0800 0000 1920 0014 5399` pasted | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 13 | bank code | `0800` | `5` after it | bank code `0800` | stays |
| 14 | number | `2000145399` | `1234567890` pasted after it | number `1234567890` | closes |
| 15 | number | nothing | `12345/0800` pasted | number `12345`, bank code `0800` | closes |
| 16 | number | nothing | `19-1234567` pasted | prefix `19`, number `1234567`, bank code still empty | closes |
| 17 | bank code | `0800` | `5` before it | bank code `5080` | stays |
| 18 | bank code | `0800`, all of it selected | `19-2000145399/0800` pasted over it | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 19 | number | `1234567890`, all of it selected | `19-2000145399/0801` pasted over it | prefix `19`, number `2000145399`, bank code `0801` | closes |
| 20 | bank code | `0800` | `19-2000145399/0800` pasted after it | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 21 | number | `2000145399`, all of it selected | `2000145399/0800` pasted over it | number `2000145399`, bank code `0800` | closes |
| 22 | number | `19` | `19-2000145399/0800` pasted before it | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 23 | bank code | `0800` | `19-2000145399/0800` pasted after its first `0` | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 24 | bank code | `0`, all of it selected | `19-2000145399/0800` pasted over it | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 25 | prefix | `12345` | `-` typed after its `1` | prefix `12345` | stays |
| 25b | number | `2000145399` | `/` typed after its `200014` | number `2000145399` | stays |
| 26 | prefix | `0` | `19-2000145399/0800` pasted after it | prefix `19`, number `2000145399`, bank code `0800` | closes |
| 27 | prefix | `0` | `1234567890` pasted after it | prefix `0`, number `1234567890` | closes |

**Partner web pins one row more, W3**, a paste the apps misread (below): `19-2000145399/0800` pasted
over the first five digits of the number `1234567890` gives the prefix `19`, the number `2000145399` and
the bank code `0800`. Reading the pasted text, partner web gets rows 23, 24, 26 and 27 right with or
without the four-digit rule; on the apps rows 23 and 24 need it, because a reading that cuts the pasted
bank code to `080` is no account.

**On iOS an edit the box's digit clamp settles keeps the keyboard, and an edit the control writes
itself closes it.** An edit whose result is what the clamp makes of the box's new text, its first digits
up to the box's length, goes through as typed: typing, and rows 1, 3, 4, 5, 10, 13, 17, 25 and 25b. Any
other edit is written by the control, which rebuilds the three boxes with `.id(pasteRevision)`, so that
each draws its new value, and the rebuild ends the edit and closes the keyboard: a split, a number
routed from another box, or digits replacing the box's own (rows 2, 6–9, 11, 12, 14–16, 18–24, 26 and
27). **On Android only a spread closes the keyboard**: an account, or a number routed from another box
(rows 7, 8, 11, 12, 15, 16, 18–24, 26 and 27), rebuilds the boxes with `key(pasteRevision)`, so that the
same paste into the same box a second time is not dropped as a repeat. Every other edit keeps the
keyboard.

**Each box keeps the digits `0`–`9` only**, up to its length, on both apps (`clampSegment`, since
2026-10-02): a keyboard can type Arabic-Indic or full-width digits, which the server refuses, and the
boxes used to keep them. The clients only split text. The server keeps every rule on the parts (the
mod-11 check and the bank-code shape). Partner web reads the same three shapes since 2026-10-02: it
splits a CZ or SK IBAN, ignores whitespace of every kind inside the digits too, and reads an en or em
dash as a hyphen. Before, it split only the two separator shapes, and a bare number pasted there cleared
the prefix.

::: warning The apps can misread a paste beside or over digits a box already holds
On the apps, which see only a box's old and new text, these pastes can still be misread:

- a whole account pasted with the caret inside a box's digits, or over part of them, which can take in
  the digits beside it or lose digits it shares with them;
- a Select All paste over digits that the pasted account begins with: number `2000` selected, then
  `2000145399/0800`, gives number `145399`;
- a bare number pasted before digits that share its first digits: `0100` pasted before `0800` gives
  `1000`.

The web reads all of these correctly, because the paste event carries the pasted text.

The apps compare the box's text before and after the edit, so a digit the paste shares with what the box
held can be read as one the box kept, and the box's own digits beside the caret or a selection can be
read as part of the paste. A bare number pasted over a box's digits can lose the digits it shares with
them the same way. A paste into an empty box is never misread. More examples, each read correctly on
partner web:

| The box holds | Pasted | The apps read | Partner web reads |
|---|---|---|---|
| the number `1234567890`, with `12345` selected (W3) | `19-2000145399/0800` | prefix `9` | prefix `19` |
| the prefix `12345`, with `2345` selected | `19-2000145399/0800` | prefix `119` | prefix `19` |
| the number `2`, all of it selected | `2000145399/0800` | number `000145399` | number `2000145399` |
| the prefix `19`, all of it selected | `2000145399` | number `200014539`, prefix still `19` | number `2000145399`, prefix still `19` |
:::

::: tip Country Configuration
Country-specific labels and validation rules (e.g., field names, format masks) are driven by the `CountryConfiguration` table managed in the admin app.
:::

The employee record has an `isProfileComplete` flag that tracks whether all required fields have been filled.

**On the apps the profile is a chain of four steps, Personal, Address, Identity and Bank, under a
stepper that names every one** (since 2026-10-03, owner remark). Each step is a node over its short
name:

| Step | Node | Name |
|---|---|---|
| Current | larger, filled, with the section's icon | bold, in the brand colour |
| Finished | a check | kept, in full |
| Not done yet | outlined, with the section's icon; the ring is the brand colour when the cleaner may jump to it | muted |

A step the cleaner may jump to is tappable across its whole column (T-0607), and a line joins the
nodes, in the brand colour behind a finished step. VoiceOver and TalkBack read each step as one
element: its place, its name and its state, such as *Step 2 of 4, Address, current step* (or
*completed*, *not started*). The *Step n of 4* header above the stepper is unchanged. Until then only
the current step was named, inside a capsule, and the other three were identical unlabelled discs. At
320pt (320dp) each step's column is 64 wide, and every name fits on one line in all five languages. At
a large text size iOS shrinks a name to fit its column, and Android wraps it onto a second line.

### What each step checks before it sends {#step-checks}

**On the Android and iOS partner apps a form's button can be tapped at any time but while its request
is in flight** (since 2026-10-24): *Sign Up*, and each onboarding step's *Next* or *Save*. A tap checks
every rule the device can check, in one pass, and names each missing or invalid field under it; nothing
is sent until all of them pass, and an error clears as soon as its own field changes. Two also clear
when the country they were judged by changes: a SWIFT refusal when the bank country changes, and a
registration-number refusal when the business country changes. Every Bank save first clears the
server's earlier SWIFT and IBAN refusals, before the device checks run, so a new refusal never shows
beside one answered for an earlier save. A refusal only the server can make comes back under the field
it is about, by its key, and any other refusal is a snackbar. Until then most of these buttons stayed
disabled until their form looked complete, which said nothing about what was missing. On partner web,
*Register* still waits for a valid form, and the bank form's *Save* for the bank country and the
account or the IBAN.

Every field named below is required; the rest is what else it must meet.

| Step | Checked on the device | Named under its field, from the server |
|---|---|---|
| Sign up | first and last name; the e-mail, then its shape; a password that meets the policy; a matching confirmation; the terms ticked | — |
| Personal | first and last name, at most 50 characters each; the birth date, 18 to 120 years before today, counted on today's UTC date, a date more than 120 years back named in the words of the server's `validation.invalid_age` (*Please enter a valid age.*) on both apps; the phone, in international form with its country code, and a possible number for that country | `validation.invalid_phone_number`, and `common.required` on the phone, under the phone |
| Address | a pick with a street of 5–255 characters, a city of 2–100 and a postcode of 3–20, checked as soon as the picker returns; with nothing picked, the card asks for a pick | `common.required` and `address.invalid_length`, on the address card |
| Identification | nationality; the passport or ID number, 5–20 characters once trimmed; the business country; the registration number | the number's format and the business register's answers (`validation.registration_number.invalid_format`, `validation.registration_number.not_registered`, `employee.business_ceased`, `employee.trade_licence_inactive`) under the registration number; `validation.passport_id.invalid_length` under the passport |
| Bank | the bank country; for a CZ or SK bank the account number and a bank code of four digits, for any other the IBAN (above) | the payout refusals under the account, the IBAN, the SWIFT or the bank country |

An incomplete pick is named on the address card itself, with a red border and *This spot has no full
address. Move the pin onto the building, or search for the street and house number.* A company row an
operator made without its registered name shows the legal-name field locked, with *Your company's
registered name is missing. Contact support to add it — it can't be changed here.*, and the step stays
incomplete until an administrator adds the name. The server holds every save to the same numbers, the
partner web's and an administrator's edit too, with one exception: only the apps' Personal save
requires the phone's country code (`validation.invalid_phone_number`), and partner web and an
administrator's edit still accept a number without it. **The phone is the one Personal field the apps
check less than the server does**: the device checks the international form and a length the number's
country allows, and whether the number is valid is the server's to judge, on numbering data neither app
carries (newer than Android's, older than the data iOS bundles). Its refusal is named under the phone
field, not in a snackbar.
→ [The numbers the server holds these fields to](/product/business-rules#cleaner-profile-fields)

**A phone pasted with its country code keeps it** (since 2026-10-24). On the apps the phone field keeps
a `+` that comes before the first digit, wherever it sits, so ` +420 728 089 247`,
`Tel: +420 728 089 247` and `(+420) 728 089 247` all read `+420728089247`; until then a `+` that was not
the very first character was dropped, and the number lost its country code. `(420) 728 089 247` still
reads `420728089247`, which Personal refuses for its missing `+`. On partner web, the phone control
reads a pasted or autofilled number's country from a `+` before its first digit, wherever it sits, as
the apps keep it: `+420 777 111 222`, `Tel: +420 728 089 247`, `(+420) 728 089 247` and
`+ 420 728 089 247` all select +420 and keep the rest as the national number, where it used to save the
code twice (`+420 420777111222`); the spaces, the label, the brackets and the invisible marks a contacts
app puts in front of a copied number are ignored.

## Step 5: Document Upload

Partners must upload identity and work-related documents through the profile page. The upload flow works as follows:

1. A **drag-and-drop upload zone** is presented (no pre-selected document type)
2. Files are staged with no type assigned -- each file gets its own **inline type selector** where the partner picks the document type
3. **Validation:** all staged files must have a type selected before the upload can proceed; files missing a type are highlighted in red with an error message
4. Document cards display file-type colored icons (PDF = red, DOC = blue, JPG = yellow, etc.)
5. Every file is its own document — several of one type are allowed — and a file byte-identical to one
   the cleaner already holds, active and not rejected, is refused (`employee_document.duplicate_file`)
   → [Business rules — the papers a cleaner uploads](/product/business-rules#employee-documents)

**On the Android and iOS apps a document can be photographed on the spot** (since the fix plan of
2026-10-25). *Upload* — on the Documents step and in Profile → My documents — and each document's
*Replace* first ask where it comes from: *Take Photo* (only on a phone with a camera), *Choose from
Library* (*Choose from Gallery* on Android) or *Choose File*. Then the upload asks for the type and the
description, and a replacement for its description, as before. A photo from the camera or the library
goes up as a JPEG of at most 1920 px, so a picture of an identity card no longer has to be saved to
Files first. iOS's file picker offers exactly what the server accepts, PDF, JPEG, PNG, DOC and DOCX;
until then it offered every image type, HEIC included, which the server refuses, and no Word document.
Android's opens the system picker on any file, as before, and re-encodes an image it picks. A source
chosen and then cancelled, at the dialog, the camera or the picker, never turns the next *Upload* into a
replacement. The partner web's file input already let a phone offer the camera, the library and files.

Supported document types:

| Document Type | Description |
|---|---|
| `IdentityCard` | Government-issued ID card |
| `Passport` | Passport |
| `DriversLicense` | Driver's license |
| `WorkPermit` | Work authorization document |
| `Contract` | Signed employment contract |
| `Certificate` | Professional certifications |
| `BankStatement` | Bank account verification |
| `TaxDocument` | Tax registration document |
| `InsuranceDocument` | Insurance documentation |
| `Other` | Other supporting documents |

Each document goes through a review workflow:

```
Uploaded → Pending → Approved / Rejected
```

### What the country asks for {#document-requirements}

The upload screen opens on a **checklist** of the document types the cleaner's country expects, each
row showing whether it is required and what has already been uploaded against it. A cleaner used to
open an empty box that named nothing, so the first step of onboarding was contacting support to ask.

The rows are admin-managed, one per (country, document type), and are what
[admin approval](#admin-approval) gates on. **A country with no rows configured gates nothing** — an
unseeded market behaves exactly as it did before the gate existed, rather than locking every cleaner in
it out of approval.

The checklist keys on the cleaner's **work country**, falling back to their address country. Work
country is the jurisdiction the requirements belong to, but it is only set at approval — and this screen
exists precisely for people who are not approved yet. A wrong guess costs a misleading prompt, not a
refusal; the approval gate itself only ever reads the work country.

An **optional** row is a prompt, not a gate. It is how a document type is offered as
expected-but-not-blocking; removing the row drops it from the screen entirely, clearing its required
flag keeps the prompt and drops the gate.

Seeded today: **CZ and SK** carry `IdentityCard` (required), `WorkPermit` (optional) and
`InsuranceDocument` (optional since 2026-10-04: the liability insurance certificate is recommended and
offered on the checklist, and approval does not wait for it; from 2026-09-28 it was required). The list is
deliberately short and is not a statement about Czech or Slovak employment law — `WorkPermit` is
optional because it applies to non-EU nationals and to nobody else, and a per-country flag cannot say
"required for some of these people".

### Replacing and removing {#document-replace-and-remove}

A cleaner has two doors on a document they already own, and they are deliberately different.

**Replace** supersedes a document with a newer file and needs no admin. The new version is created
*before* the old one is retired, so the document count never dips, `AreDocumentsUploaded` never flips,
and the registration lock never re-engages. The document **type** is carried over from the version being
replaced rather than taken from the caller — otherwise a replacement could satisfy a requirement by
relabelling a document an admin had already approved. The new version lands `Pending`: it is new
evidence and has not been looked at, which is also why replacing cannot be used to dodge review.

**Request removal** asks an admin and changes nothing. The document stays active until the request is
answered, so a request nobody answers leaves the cleaner exactly as they were. A reason is required —
without one an admin is being asked to rule on nothing. One open request per document; an answered one
does not block a new one.

::: warning
**The partner cannot delete a document.** The button that did soft-deleted on the spot, and that flipped
`AreDocumentsUploaded`, which re-engaged the registration lock — one tap, with no confirmation on either
mobile platform, cost a cleaner their access to work. Some of these documents the employer is required
to hold, so the person least placed to judge whether one can go was the only one who could remove it.
Approving a removal request is now the only thing in the platform that removes one.
:::

::: tip
Documents are uploaded as files and stored in Azure Blob Storage. The admin app provides a download/preview interface for reviewing uploaded documents.
:::

## Step 6: Admin Approval {#admin-approval}

After the partner completes their profile and uploads required documents, an admin reviews the application:

1. Admin views the partner's profile in the admin app (Employee Management)
2. Admin reviews each uploaded document (approve/reject individually)
3. Admin can approve or reject the partner overall

**Approval criteria:**
- Profile is complete (`isProfileComplete === true`)
- Contract status is `Pending` or `Rejected` — a cleaner rejected earlier can be approved again, and
  nothing returns them to `Pending` by itself → [Business rules](/product/business-rules#employee-documents)
- Every document type the **work country** marks required is present **and** `Approved` (the insurance
  certificate is optional since 2026-10-04)
- For a Czech cleaner, ARES holds the IČO, the business has not ended and a trade licence is in force;
  an ARES outage refuses until the admin tries again (since 2026-10-04)
  → [The business register](/product/business-rules#business-register)
- Every partner document in force for that market — the framework contract, the self-billing
  agreement, the data-processing agreement — is accepted at its current version
  (`employee.legal_documents_not_accepted`). All three are in force since 2026-09-29, so the gate is
  live, and a new version — the framework agreement `2026-10-27` — is accepted again before approval
  → [A cleaner's own documents](/product/business-rules#cleaner-documents)

That last line is enforced, not advisory. Approval used to consult `isProfileComplete()` alone, which
excludes documents deliberately — so an admin could approve a cleaner who had uploaded nothing, or whose
every document had been rejected, and "Approved" meant only that somebody had pressed the button. It now
means the paperwork exists and was accepted. See [document requirements](#document-requirements) for
where the required list comes from, and note that editing those rows never reaches back and re-judges
anyone already approved.

**Approval actions:**
- `approveEmployee()` -- Sets `ContractStatus` to `Approved`, granting full access
- `rejectEmployee(reason)` -- Sets `ContractStatus` to `Rejected` with a reason, which the cleaner
  reads word for word on their lock screen

Either decision is pushed to the cleaner — see the [lock screen](#registration-lock-screen) below.

::: warning
Until approved, the partner can log in and access their profile, but their ability to take and manage orders may be restricted. The `contractStatus` field determines the partner's access level.
:::

### Registration Lock Screen

Partners who have not yet been approved see a registration lock screen that displays a **progress
bar** and the requirements still between them and work — three rows on the web, and on the Android
and iOS apps a fourth, **Contract documents**:

1. **Profile Information** -- lists the names of any missing required fields (translated to the partner's language)
2. **Required Documents** -- whether at least one active (uploaded) document exists. The
   documents screen behind it lists what the country actually asks for, per
   [document requirements](#document-requirements)
3. **Contract documents** (mobile) -- shown only while a partner document is in force for the market,
   and done once every one is accepted at its current version; until one is in force, approval does
   not wait on it
4. **Admin Approval** -- shows one of the following distinct states:
   - _"Complete profile first"_ -- profile is not yet complete
   - _"Awaiting review"_ -- profile is complete and pending admin decision
   - _"Rejected: {reason}"_ -- admin has rejected the application with a reason
   - _"Approved"_ -- admin has approved the partner

**On the apps each row's status sits beside its step's name, not beside its details** (since
2026-10-03, owner remark). However many detail lines follow (the missing fields, a rejection reason),
they hang below at the title's indent, and the status and chevron stay at the top beside the name.
The two apps centre them on different lines. iOS centres its status icon, *Done* and chevron on the
title's line, with *Contact support* below the details. Android centres its halo, status icon and
chevron on the row's header: the title and the line under it, which is *Done*, the call to action
(*Contact support* on a rejected application) or the awaiting-review note. Until then iOS pinned
its status icon, *Done* and chevron to the top of the row, above the title's centre, and Android
centred its halo, status icon and chevron on the whole row, beside the middle of the details. The
two apps still draw the row differently. iOS leads with the status icon and trails *Done* and the
chevron. Android leads with the step's category halo, says *Done* under the title, and trails a
status icon and the chevron.

**On the mobile apps a finished row still opens its section, until approval** (owner ruling
2026-10-01). The lock replaces the whole app until an admin approves, so a row that went inert at
*Done* left a cleaner who had filled everything in with no way back to correct it — and
*Documents: Done* means one active document, not every type the country requires, so a cleaner who had
uploaded only an ID could not get back to add the insurance certificate approval then needed. A *Done* row
keeps its *Done* label and gains a chevron: Profile opens Personal at the start of the onboarding
chain, from which every section is one tap away on its stepper; Documents opens the documents screen; Contract
documents opens the documents to read and accept. This holds for a **rejected** cleaner too, and **an
edit does not resubmit anything** — the application stays where the admin left it. With a complete
profile the Personal, Address and Identification buttons read **Save** rather than *Next*, because
saving returns to the lock instead of moving on. Partner web already left the profile open behind the lock (see
*Excluded Routes* below), so this brings the apps to parity.

**A rejection says why, and offers a way out.** On both apps the rejected Approval row is drawn as an
error, shows the admin's reason under its line — **verbatim and untranslated**, as partner web shows
it, trimmed, and nothing when the admin left it blank — and offers **Contact support**, which opens an
e-mail to `support@cleansia.cz` with the subject *"Cleansia partner — application rejected"* in the
app's language. Partner web offers the same link under the reason since 2026-10-02, with the subject in
the UI language, and only while the application is rejected. The reason never travels in the push (below); the lock reads it from the
registration status.

**The decision reaches the cleaner without a pull.** An admin's approval or rejection pushes the cleaner
`employee.registration_approved` or `employee.registration_rejected` — push-only, with no feed row and
no deep link, because a tap opens the app and for this cleaner the app *is* the lock
([event catalogue](/architecture/push-notifications#registration-decided)). The lock re-checks:

| When | Android | iOS |
|---|---|---|
| The lock comes back on screen — the app returns to the foreground, or a section pops back | on `ON_RESUME`, once a 15-second stale window has passed | every time (`scenePhase` → `.active`, and `onAppear`) |
| A decision push arrives while the lock is on screen | at once, inside the stale window too | at once |
| Pull to refresh | yes | yes |

An approval found by that re-check unlocks the app; a rejection redraws the Approval row.

::: warning A document the admin rejected still reads *Done*
*Required Documents* counts any active document, whatever its review status, so a cleaner whose
required document an admin rejected still sees *Documents: Done* and *Awaiting review*. Reading the
per-type statuses the documents screen already loads (`GetMyDocumentRequirements`) would fix it on the
client; that is filed as T-0801 and not built.
:::

Signing out from this screen is confirmed on both mobile platforms. It is the one destructive thing
the screen offers and the control sat one tap away from it.

::: info Excluded Routes
The following pages are accessible even when the registration lock is active and are **not** blocked by the lock screen: Profile, GDPR, 404, Login, Register, Confirm Email, Forgot Password.
:::

## Step 7: Full Platform Access

Once approved, the partner has full access to:
- Browse and take available orders
- Start and complete assigned orders
- Upload before/after photos
- View earnings dashboard
- Access invoices and download PDFs

## Password Reset

Partners who forget their password can use the `/forgot-password` flow:

1. Enter email address
2. Receive reset link via email
3. Click link and set new password
4. Redirect to `/login`
