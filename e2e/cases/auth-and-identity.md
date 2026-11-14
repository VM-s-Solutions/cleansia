# Auth and identity: cases

How each role registers, signs in, keeps a session and loses it, and how a cleaner is onboarded and approved. Test accounts are `e2e.<run>.customer@example.com` and `e2e.<run>.cleaner@example.com`; the DEV-only `sql-scripts/dev-e2e-confirm-accounts.sql` confirms them, because no case can read an e-mail. A strong test password is, for example, `E2e-Pass-2026` (a letter, a digit, 8 or more characters).

## Customer registration

### E2E-AUTH-001 — A customer registers by e-mail and the server records the consent
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — a sign-up with `termsAccepted: true` creates the account, the two consent rows and the registration audit row in one commit
- **Given** no account for the address, the customer web `/register`. **When** the customer submits first name, last name, a new address, a valid password, the site language and the terms tick (API: `POST api/Auth/Register` on :5003). **Then** the answer is 200 `true`; the account exists unconfirmed; the customer's consents (profile page, or `GET api/v1/Gdpr/consents`) hold `TermsOfService` and `PrivacyPolicy`, each with the version equal to the effective date of the document in force for the market (read it from the legal-text read, never hard-code it) and covering the current text; the admin Customer actions segment shows one `customer.account.register` row naming the new user, with the method, the language, no referral code, the tick and both versions; a sign-in before confirmation opens no session (E2E-AUTH-010).
- **DEV:** creates the run's customer; the same account serves the BOOK, PAY and CANCEL flows. Confirm it with the SQL helper (E2E-AUTH-009).

### E2E-AUTH-002 — A customer registers in the native app
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — the iOS and Android customer apps send the tick on the registration itself, and nothing is parked on the phone to be flushed later
- **Given** a signed-out phone with the customer app, which opens on the sign-in screen. **When** the customer taps Register, fills the form, ticks the terms and submits (API: `POST api/Auth/Register` on :5004). **Then** the app moves to the confirmation screen; the same consent rows and the same `customer.account.register` row exist as on web; submitting without the tick is refused client-side first.
- **DEV:** one account per platform, with its own address. The password field offers the platform's strong password only on a device with AutoFill; the simulator may not.

### E2E-AUTH-003 — A registration without the terms tick is refused and recorded
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — a missing or `false` tick is refused as `consent.terms_not_accepted` before any account exists, and the refusal is a row with the caller's IP
- **Given** a new address. **When** `POST api/Auth/Register` is sent once with `termsAccepted: false` and once with the member left out. **Then** both answers are 400 with `consent.terms_not_accepted` on `termsAccepted`; no user exists for the address; the Customer actions segment shows two failed `customer.account.register` rows with that key, no user, no address and the caller's IP.
- **DEV:** creates no account.

### E2E-AUTH-004 — A registered address cannot register again
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /api/authentication#register — an address that already belongs to a Cleansia account is refused `user.existing_email`
- **Given** the run's customer, confirmed. **When** the same address is registered again, then once with its letters in another case. **Then** each answer is 400 `user.existing_email` on `email`; the existing account is unchanged; the customer web shows the error under the e-mail field, not a generic message.
- **DEV:** if a second serviced market exists, repeat with its `countryId` and expect the same key; if none exists, say so.

### E2E-AUTH-005 — The password rule: eight characters, a letter and a digit, any alphabet
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /api/authentication#register — a password has at least 8 characters with a letter and a digit, and a letter of any alphabet counts
- **Given** new addresses. **When** `Register` is sent with `Ab1`, `abcdefgh`, `12345678` and `Пароль2024`. **Then** the first three are refused `auth.invalid_password_format` on `password` and create nothing; the fourth is accepted. The web form lists the rule under the field and keeps the button disabled while it fails.
- **DEV:** the fourth creates a throw-away unconfirmed account; leave it.

### E2E-AUTH-006 — The other fields are validated with their own keys
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — a registration is judged field by field before any account exists
- **Given** otherwise valid input. **When** `Register` is sent with a malformed e-mail, an empty first name, a last name of 51 characters, and a language code that is not seeded. **Then** the answers are `email.invalid_format` on `email`, `common.required` on `firstName`, `common.max_length` on `lastName` and `language.not_supported` on `language`; nothing is created.
- **DEV:** creates nothing. The 50-character limit is read from the shared name validator; confirm it against the code in 1b.

### E2E-AUTH-007 — Registering an unconfirmed address again with another password withdraws the stored one
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — registering an unconfirmed address again with another password leaves the account with no password
- **Given** an unconfirmed account registered with password A. **When** the same address is registered again in the same market with password B, then a third time with B. **Then** the second and third answers are 200; a password sign-in with A and with B both fail `validation.invalid_password` (an account with no password is refused like a wrong one); the same address registered again with the *same* password as the stored one keeps it.
- **DEV:** use a throw-away address, not the run's customer, because the account is left without a password (only Forgot password or a confirmation restores one).

### E2E-AUTH-008 — The partner hosts register no customers
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — `POST api/Auth/Register` is routed on the two customer hosts only
- **Given** the DEV hosts. **When** `POST api/Auth/Register` is sent to Partner :5000 and Partner Mobile :5002. **Then** both answer 404 or 405 (no such route) and create no user; `RegisterEmployee` is the cleaner route (E2E-AUTH-033).
- **DEV:** creates nothing.

## E-mail confirmation

### E2E-AUTH-009 — A confirmed customer signs in and a session opens
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /customer-app/authentication#email-confirmation — confirmation signs the customer in and takes them to their orders
- **Given** the run's customer registered and the SQL helper run for the address. **When** the customer signs in with the registration password. **Then** the answer carries `isEmailConfirmed: true` and a session (web: HttpOnly access and refresh cookies and a CSRF token; mobile: tokens in the body); the web lands on the orders page; a profile read (`GET api/User/GetCurrent`) returns the registered names and language.
- **DEV:** the helper confirms the address without replacing the password hash, so the registration password still works; a real confirmation would have replaced it with the confirmer's choice (not testable here, below).

### E2E-AUTH-010 — A sign-in before confirmation opens no session
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#session-rows — a right password on an unconfirmed address is a success that opens no session, and the row says so
- **Given** a registered, unconfirmed customer. **When** the customer signs in with the right password. **Then** the answer is 200 with `isEmailConfirmed: false` and an empty token and no cookies; the web redirects to the confirmation page with the address in the query; the apps show the code screen; the Customer actions segment shows a successful `customer.session.login` row with the confirmed flag `false`.
- **DEV:** run before the SQL helper.

### E2E-AUTH-011 — A wrong confirmation code is refused and recorded against the account
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#session-rows — a bad confirmation code on an account that exists is a failure row that names the account
- **Given** an unconfirmed customer. **When** `PUT api/Auth/ConfirmUserEmail` is sent with the address and a wrong six-digit code. **Then** the answer is 400 `auth.invalid_confirmation_code` on `code`; the account stays unconfirmed; the Customer actions segment shows a failed row with that key whose subject is the account id and which holds neither the address nor the code.
- **DEV:** the real code is unreadable, so a wrong one is any six digits.

### E2E-AUTH-012 — Guessing the confirmation code is capped per issued code
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /api/authentication#confirm-user-email — a code is bounded by an attempt budget and a 15-minute expiry
- **Given** an unconfirmed throw-away customer with one issued code. **When** six wrong six-digit codes are sent in a row, each within the `auth` rate window of 10 requests per minute. **Then** the early attempts answer `auth.invalid_confirmation_code`; once the budget (5 per issued code) is spent every further attempt answers `auth.too_many_attempts`, and a resend (E2E-AUTH-013) restores the budget.
- **DEV:** whether the fifth or the sixth attempt is the first refused is read from `HasAttemptBudgetAsync` in 1b. Too many requests in one minute answer 429 instead; pace the calls.

### E2E-AUTH-013 — Resend confirmation answers by account state
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /api/authentication#resend-confirmation-email — the resend works for an unconfirmed existing address and for no other
- **Given** an unconfirmed customer, a confirmed one and an address nobody holds. **When** `POST api/Auth/ResendConfirmationEmail` is sent for each. **Then** the unconfirmed one answers 200 `true`; the confirmed one 400 `user.email_confirmed`; the unknown one 400 `user.not_existing_email`; on the web page *Resend code* is disabled for 30 seconds after a press.
- **DEV:** the e-mail itself is never read.

## Customer sign-in

### E2E-AUTH-014 — A customer signs in on web and the session lives in HttpOnly cookies
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /customer-app/authentication#session-tokens — the web customer app never holds a token in JavaScript; both tokens are HttpOnly cookies and a CSRF token echoes on state-changing calls
- **Given** the confirmed run customer. **When** the customer signs in on `/login` (API: `POST api/Auth/Login` on :5003, `rememberMe` true), then reloads. **Then** the page script can read no access or refresh token (`document.cookie` shows neither); a state-changing call without the `X-CSRF-Token` header is refused; the session survives the reload; the Customer actions segment shows a `customer.session.login` row with method password, remember-me true, the web client family and the confirmed flag true.
- **DEV:** reuse this session for the later customer cases.

### E2E-AUTH-015 — A customer signs in on iOS and Android and the tokens travel in the body
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /customer-app/authentication#session-tokens — mobile is the other shape: no cookies, bearer authorization, no CSRF token
- **Given** the confirmed run customer and a phone with the app. **When** the customer signs in (API: `POST api/Auth/Login` on :5004). **Then** the app opens its home; the answer carries access and refresh tokens in the body; the Customer actions segment shows a `customer.session.login` row with the mobile client family; after an app restart the session is kept.
- **DEV:** one sign-in per platform. The mobile customer refresh lifetime is 90 days with remember-me; it is not waited out.

### E2E-AUTH-016 — A wrong password or an unknown address is refused with one key
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#session-rows — a refusal on an account that exists names the account; an unknown address names nobody
- **Given** the confirmed run customer. **When** a sign-in is sent with the wrong password, then one with an address nobody holds. **Then** both answers are 400 `validation.invalid_password` on `password` (the caller learns nothing new); the Customer actions segment shows a failed `customer.session.login`-family row for the first whose subject and resource are the account id, and one for the second with no user, no resource and no payload; neither row contains the typed address.
- **DEV:** an empty password answers `common.required`; an empty or malformed address answers `common.required` or `email.invalid_format`.

### E2E-AUTH-017 — Repeated wrong passwords lock the account for a while
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#session-rows — a lockout is a refusal on a known account that names the account
- **Given** a throw-away confirmed customer, not the run's. **When** six wrong passwords are sent in a row, then the right password. **Then** after the failures the answer is 400 `auth.account_locked` even for the right password, and a failure row naming the account is written; a device that still holds a live refresh token of that account passes the lock check and the password rule decides; after the lock lapses the right password signs in.
- **DEV:** the figures (5 failed attempts, 15 minutes) come from `User.MaxFailedLoginAttempts` and `User.FailedLoginLockout`, and no docs page states them (finding). Waiting out 15 minutes is the cost of the last step; the account is throw-away so the run's customer is never locked.

### E2E-AUTH-018 — A customer account cannot sign in on a partner host
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#registration-requires-the-tick-writes-the-consent-and-the-record-of-it — a Customer-profile account is refused `auth.insufficient_privileges` on a partner host and no cleaner account is created
- **Given** the confirmed run customer. **When** the same credentials are sent to `POST api/Auth/Login` on Partner :5000 and on Partner Mobile :5002, and entered on the partner web and the partner iOS and Android apps. **Then** both answers are 400 `auth.insufficient_privileges` and each client shows that refusal; no token or session is issued; no `Employee` exists for the user afterwards; the Customer actions segment gains no row from either call (a partner host writes none).
- **DEV:** `docs/api/authentication.md` still says such a login "auto-upgrades" the profile to Employee; the flow page and the code refuse it (finding). The case pins the refusal.

### E2E-AUTH-019 — The native customer apps need an account
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** iOS · Android
- **Rule:** → /flows/auth-and-identity#native-account-only — a signed-out person goes from the splash screen to sign-in, which offers the password, Forgot password, Google (and Apple on iOS) and Register, and nothing else
- **Given** a freshly installed or signed-out customer app. **When** the app is opened. **Then** the sign-in screen shows; it offers no *Find a guest booking*; no booking screen is reachable without signing in.
- **DEV:** screenshots are the evidence.

## Password reset

### E2E-AUTH-020 — A customer asks for a password reset
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /customer-app/authentication#password-reset — the customer enters an e-mail address on forgot password and the backend sends a code
- **Given** the confirmed run customer and an address nobody holds. **When** `PUT api/User/RequestPasswordChange` (web, :5003; the mobile host also has `POST api/Auth/ForgotPassword`) is sent for each address. **Then** the known address answers 200; the unknown address answers 400 `user.not_existing_email` on `email`; the Customer actions segment shows a `customer.password.reset_requested` row for the first and a row with no user and no address for the second; the Forgot password screens of web, iOS and Android reach their "sent" state.
- **DEV:** the e-mail is not read.

### E2E-AUTH-021 — Completing a reset is refused for a bad code, a weak password and a reset that changes nothing
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#edge-cases — a password reset revokes every session, committed before the password changes, and a bad code is a failure row naming the account
- **Given** the run customer with a reset requested. **When** `PUT api/User/ChangePassword` is sent with a wrong code and a strong password, then with a wrong code and `abc`, then six more wrong codes. **Then** the first answers 400 `auth.invalid_reset_token` on `code`; the second is refused `auth.invalid_password_format` on `newPassword`; once the budget is spent every attempt answers `auth.too_many_attempts`; the customer's existing sessions stay valid because nothing was reset.
- **DEV:** `auth.same_reset_password` needs a valid code and cannot be reached here.

### E2E-AUTH-022 — A valid reset code sets the password and ends every session
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#edge-cases — while a thief holds a session, a reset revokes all sessions and the revoke is committed before the password change
- **Given** the customer signed in on two clients (web and a second API session) and a reset requested with a code the test knows. **When** the reset is completed with a new password. **Then** the answer is 200; both old refresh tokens answer `auth.invalid_refresh_token`; the old password fails and the new one signs in; the Customer actions segment shows `customer.password.reset_requested` and `customer.password.reset_completed` rows.
- **DEV:** blocked unless 1b plants a known code hash on the user row (a six-digit code is stored as the SHA-256 hex of its digits, expiring in 15 minutes); otherwise it is listed under Not testable. Use a throw-away customer.

### E2E-AUTH-023 — A cleaner resets a forgotten password on partner web and in the apps
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/onboarding#password-reset — a partner enters the address, receives a code and sets a new password, then returns to sign-in
- **Given** the confirmed run cleaner. **When** the cleaner asks for a reset on partner web (`PUT api/User/RequestPasswordChange` on :5000) and in each app (`POST api/Auth/ForgotPassword` on :5002). **Then** each request answers 200 and the screen reaches its "sent" state; a cleaner's request writes no audit row anywhere (a partner host writes none).
- **DEV:** unsure which host the partner apps call to complete a reset: the partner mobile `UserController` lists no `ChangePassword` route (finding). Completion needs the planted code of E2E-AUTH-022.

## Sessions: lifetime, rotation, revocation

### E2E-AUTH-024 — The access token lifetime is set per host
- **Priority:** P2
- **Role:** Customer · Cleaner · Admin
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#sessions — the access token lasts 15 minutes on the admin host and 30 on the partner web, customer web and both mobile hosts
- **Given** a sign-in on each of the five hosts. **When** the access token's `exp` and `iat` claims are read (web: from the cookie value the API client holds). **Then** `exp - iat` is 900 seconds on Admin :5001 and 1800 seconds on :5000, :5002, :5003 and :5004.
- **DEV:** no waiting; the claims are read once.

### E2E-AUTH-025 — A web session outlives its access token through a silent refresh
- **Priority:** P2
- **Role:** Customer · Cleaner · Admin
- **Surfaces:** Web
- **Rule:** → /flows/auth-and-identity#sessions — both web clients refresh silently on a `401` and replay the request
- **Given** a signed-in customer, a cleaner and an admin on their web apps, each with remember-me. **When** the page is left open until the access token has expired (31 minutes for customer and partner web, 16 for admin) and then a navigation triggers an own-API call. **Then** the call is answered `401`, one refresh follows, the request is replayed and the page shows its data; the user is never sent to the sign-in page; the refresh cookie has changed.
- **DEV:** costs a 31-minute wait per run, parallel across the three apps.

### E2E-AUTH-026 — A refresh rotates the token
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#rotation-detects-theft — a refresh token is rotated on every use and only a hash is stored
- **Given** a mobile-host sign-in giving refresh token R1. **When** `POST api/Auth/RefreshToken` is sent with R1, then with the returned R2. **Then** each answer carries a new access token and a new refresh token that differs from the one presented; R2 works once; no audit row is written for either refresh.
- **DEV:** mobile hosts take the token in the body, the web hosts from the cookie; run the body shape here.

### E2E-AUTH-027 — Presenting an already-rotated refresh token ends the whole chain
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#rotation-detects-theft — an already-rotated token is a theft signal: the entire chain is revoked and the revocation commits itself
- **Given** R1 was rotated into R2, then R2 into R3. **When** R1 is presented again, then R3. **Then** the first answer is `auth.refresh_token_reused` (the flow page says 401); the second answer is `auth.invalid_refresh_token`; the access token minted from R3 is refused once it expires; a further retry of R1 is still refused (the revoke was not rolled back by the failing response).
- **DEV:** use a throw-away session, not the one the run is using.

### E2E-AUTH-028 — A refresh without a usable token is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#rotation-detects-theft — a revoked or expired token is refused, never rotated
- **Given** a token that was signed out, a random string and an empty body. **When** `POST api/Auth/RefreshToken` is sent with each. **Then** the first two answer `auth.invalid_refresh_token`; the empty one answers `common.required` on `token`; no token is issued.
- **DEV:** expiry itself (1 day without remember-me, 30 or 90 with) is not waited out.

### E2E-AUTH-029 — Signing out ends the session and leaves one audit row
- **Priority:** P2
- **Role:** Customer · Admin
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#session-rows — sign-out writes `customer.session.logout`, and an administrator's sign-out writes `admin.session.logout`, not the customer label
- **Given** a signed-in customer and a signed-in admin. **When** each signs out (`POST api/Auth/Logout`, `POST api/AdminAuth/Logout`). **Then** the web cookies are cleared; the refresh token each held is then refused `auth.invalid_refresh_token`; the Customer actions segment shows a `customer.session.logout` row recording that a token was present; the admin audit log shows an `admin.session.logout` row for the admin and no customer row.
- **DEV:** the admin used here is a created test admin (E2E-AUTH-055), not the published one, so the run keeps one admin session.

### E2E-AUTH-030 — Logout is idempotent and session-scoped
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#edge-cases — a logout with an unknown token silently succeeds, and a logout presenting an already-rotated token kills only the live descendant
- **Given** two sessions of one customer (S1 on one client, S2 on another) and S1 rotated once. **When** logout is sent with a random token, then with S1's pre-rotation token. **Then** the first answers success and changes nothing; the second succeeds and S1's live successor is dead, but S2 still refreshes.
- **DEV:** the answer for a random token must not differ from a real one.

### E2E-AUTH-031 — A device revoked on a mobile host ends only that device's tokens
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/auth-and-identity#edge-cases — a revoked device ends only tokens carrying its device id; a token with no device id survives to natural expiry
- **Given** one customer signed in from device D1, from D2 and once with no device id. **When** D1 is unregistered (`Device/Unregister` on :5004). **Then** D1's refresh token and, after the revoked-device directory next polls, its access token are refused; D2's and the no-device session still work.
- **DEV:** the device header and the directory's poll interval are read from `DeviceHeadersInterceptor` and the directory in 1b; the wait is that interval.

### E2E-AUTH-032 — A reset or a role change cuts a mobile session before the token expires
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/auth-and-identity#immediate-cutoff-beyond-the-token-ttl — mobile hosts consult polled directories of revoked devices and users, so a password reset takes effect without waiting for the token to expire
- **Given** a mobile session with an access token minutes from expiry. **When** the account's sessions are revoked (E2E-AUTH-022 or an erasure, E2E-GDPR-006). **Then** the access token is refused on the next poll, well inside its 30 minutes; the web hosts have no such directory, which is why their lifetime is short.
- **DEV:** depends on E2E-AUTH-022 or an erasure; the check is the time between the revoke and the first refusal.

## Cleaner registration and onboarding

### E2E-AUTH-033 — A cleaner registers and gets a pending contract
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/onboarding#step-1-create-account — registration creates the user, an Employee with `ContractStatus.Pending`, and e-mails a six-digit code
- **Given** no account for the address, partner web `/register` (API: `POST api/Auth/RegisterEmployee` on :5000, or :5002 for the apps). **When** the cleaner submits names, address, password, language and the market. **Then** the answer is 200 `true`; a user and an Employee exist with the contract `Pending` and the profile incomplete; the cleaner's consent list holds no customer terms or privacy row; the registration needed no terms tick (employee registration is not gated); the Customer actions segment shows nothing.
- **DEV:** creates the run's cleaner. The language is stamped on the account (E2E-AUTH-064).

### E2E-AUTH-034 — A cleaner confirms the address and lands on the registration lock
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/onboarding#registration-lock-screen — a partner who is not approved sees the lock with a progress bar and the requirements still between them and work
- **Given** the cleaner registered, unconfirmed. **When** the cleaner signs in before the SQL helper, then after it. **Then** before: 200 with `isEmailConfirmed: false` and no session, and the web redirects to confirmation; after: a session opens and the lock screen shows Profile Information, Required Documents, (apps) Contract documents and Admin Approval, with Admin Approval reading *Complete profile first*.
- **DEV:** the helper confirms the address; the confirmer-chooses-password path is not testable (below).

### E2E-AUTH-035 — A cleaner completes the profile
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/onboarding#step-4-profile-completion — the profile is complete when personal details, address, payout destination, passport ID, nationality, registration number and the business identity are saved
- **Given** a confirmed, signed-in cleaner with an incomplete profile. **When** the cleaner saves Personal (name, phone in international form, a birth date 18 or more years back), Address (street, city, postcode, country), Identification (passport ID, nationality, a Czech registration number) and the bank details (web: `PUT api/Employee/UpdateEmployee`, `UpdateBankDetails`; apps: the stepper). **Then** each save answers 200; `GET api/Employee/GetCurrentEmployee` shows the profile complete; the lock's Profile Information row is Done and Admin Approval reads *Awaiting review* once the documents row is also done; the registration number is accepted on its format alone.
- **DEV:** the register lookup is switched off on DEV (`Ares:Enabled`), so only the number's format is judged; use a format-valid Czech registration number.

### E2E-AUTH-036 — The profile refuses what the server forbids
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cleaner-profile-fields — the server's rules for each profile field and the key that refuses each
- **Given** a signed-in cleaner. **When** saves are sent with a name of 51 characters, a birth date in the future, one 17 years back, one 121 years back, a phone `123` on the apps' Personal save, a street of 4 characters, a passport ID of 4 characters, a CZ bank code of `12a4`, and an entity type of legal entity. **Then** the refusals are `common.max_length`, `validation.date_must_be_in_past`, `validation.invalid_age` (twice), `validation.invalid_phone_number`, `address.invalid_length`, `validation.passport_id.invalid_length`, `validation.payout.invalid_bank_code` and `employee.legal_entity_not_accepted`; the apps name each failing field under it before sending, except the phone's validity.
- **DEV:** nothing is saved by a refusal. The apps' phone check judges only the form and a possible length.

### E2E-AUTH-037 — A cleaner uploads documents against the country's checklist
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/onboarding#document-requirements — the upload screen opens on a checklist of the document types the cleaner's country expects, each with whether it is required
- **Given** a cleaner whose address is in Czechia. **When** the cleaner opens the documents screen and uploads a PDF or JPEG of an identity card typed `IdentityCard` (web: `POST api/Employee/SaveMyDocuments`; apps: camera or library). **Then** the checklist (`GetMyDocumentRequirements`) lists `IdentityCard` as required and `WorkPermit` and `InsuranceDocument` as optional; the upload creates one document, version 1, `Pending`; the checklist marks `IdentityCard` as uploaded; the lock's Required Documents row is Done.
- **DEV:** a database seeded before 2026-10-04 may still mark the insurance row required; read the checklist, do not assume.

### E2E-AUTH-038 — The same file twice is refused, and a rejected document never blocks
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#employee-documents — a file whose bytes equal an active, non-rejected document of the same cleaner is refused `employee_document.duplicate_file`
- **Given** a cleaner holding a `Pending` identity card. **When** the identical file is uploaded again under another type, then twice inside one upload, then after an admin rejects the first. **Then** the first two are refused `employee_document.duplicate_file` and add no document; after the rejection the same file is accepted as a new document.
- **DEV:** needs one admin rejection (E2E-AUTH-045).

### E2E-AUTH-039 — Replacing a document writes version two and retires the first
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#employee-documents — a new version exists only through Replace: version n+1 of the same type, `Pending`, linked to the document it supersedes
- **Given** a cleaner with a document. **When** the cleaner replaces it with a different file (`PUT api/Employee/ReplaceMyDocument/{documentId}`). **Then** the new document is version 2, `Pending`, the same type; the old one is no longer active; the count of active documents never dipped; the admin document queue shows the new version and the version history shows both.
- **DEV:** none.

### E2E-AUTH-040 — A cleaner cannot delete a document, only ask
- **Priority:** P2
- **Role:** Cleaner · Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/user-management#deletion-requests-—-the-only-thing-that-removes-a-document — a partner asks to remove a document and nothing changes until an admin answers; rejecting needs notes
- **Given** a cleaner with a document. **When** the cleaner asks twice (`POST api/Employee/RequestMyDocumentDeletion/{documentId}`), then an admin rejects the request without notes, with notes, and approves a second request. **Then** the second ask is refused `employee_document.deletion_already_requested`; the document stays active meanwhile; the rejection without notes is refused and with notes accepted; answering an already answered request is refused `employee_document.deletion_already_resolved`; the approval removes the document.
- **DEV:** use a spare document; the identity card the approval depends on stays. Whether the notes refusal key is `common.required` is read in 1b.

### E2E-AUTH-041 — A cleaner accepts the three documents of their own
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cleaner-documents — a cleaner accepts the framework contract, the self-billing agreement and the data-processing agreement in the app, versioned, and approval and the take enforce it
- **Given** a signed-in cleaner who has accepted nothing. **When** the cleaner lists the documents (`GET api/Employee/GetMyLegalDocuments`) and accepts each by its text id (`POST api/Employee/AcceptLegalDocument`), then accepts one again, then sends a text id that is not in force. **Then** the list shows three documents with version, date and an accepted flag; each acceptance flips the flag; the repeat succeeds and writes nothing; the foreign id is refused `legal.document_not_in_force`; the cleaner's export (E2E-GDPR-003) lists the acceptances; the lock's Contract documents row (apps) is Done.
- **DEV:** none.

### E2E-AUTH-042 — A cleaner who has not accepted the documents is not approved and takes no job
- **Priority:** P1
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cleaner-documents — approval, the take and an administrator's placement refuse `employee.legal_documents_not_accepted` while any document in force is not accepted at its current version
- **Given** a cleaner with a complete profile and approved documents who has accepted two of the three. **When** an admin approves the cleaner. **Then** the approval is refused `employee.legal_documents_not_accepted` and the contract stays `Pending`; after the third acceptance the same approval passes this gate.
- **DEV:** runs inside the onboarding sequence, before E2E-AUTH-046.

### E2E-AUTH-043 — An admin finds the pending cleaner and reads the application
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/user-management#approval-criteria — Approve and Reject are offered when the profile is complete and the role's policy allows
- **Given** a cleaner with a complete profile, an uploaded identity card and the three acceptances. **When** an admin opens Employee Management, filters for the new cleaner and opens the detail (`GET api/AdminEmployee/get-paged`, `details/{employeeId}`). **Then** the list shows the cleaner `Pending`; the detail shows the profile, the document with its status and type, and *Approve* (and *Reject*) enabled; a Manager and a Support see the same, an Accountant sees the cleaner list but not the documents.
- **DEV:** the admin is the published DEV admin.

### E2E-AUTH-044 — An admin approves a document
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/user-management#document-actions — each uploaded document is approved or rejected on its own
- **Given** the cleaner's `Pending` identity card. **When** the admin previews or downloads it, then approves it (`POST api/AdminEmployeeDocument/{documentId}/approve`). **Then** the document is `Approved`; the cleaner's checklist shows the type as approved; the admin queue no longer lists it as pending; the approval is an admin audit row naming the document.
- **DEV:** none.

### E2E-AUTH-045 — An admin rejects a document with a reason
- **Priority:** P1
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web
- **Rule:** → /admin-app/user-management#document-actions — a rejected document carries the admin's reason
- **Given** a second `Pending` document of the cleaner (a spare certificate). **When** the admin rejects it with a note (`POST api/AdminEmployeeDocument/{documentId}/reject`). **Then** the document is `Rejected` with the note; the cleaner sees it rejected with the reason and may upload a corrected file; the approved identity card is unaffected; an unknown document id is refused `general.not_found`.
- **DEV:** the spare document is created for this case only.

### E2E-AUTH-046 — An admin approves the cleaner and the lock lifts
- **Priority:** P1
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /admin-app/user-management#approve-employee — approval sets the contract to `Approved` and the work country, pushes the cleaner, and lifts the registration lock on its next load
- **Given** the profile complete, the required document approved, the three acceptances done, and an active pay rate for every active service and package in the work country's currency. **When** the admin approves with the Czech work country (`POST api/AdminEmployee/{employeeId}/approve`). **Then** the contract is `Approved` and the work country is Czechia; an admin audit row records the contract before and after; the cleaner is pushed `employee.registration_approved`; on its next load the lock is gone and the board, orders and earnings open; a second approval is refused `employee.already_approved`.
- **DEV:** the push is observable on the phone only; on web the next page load is the evidence. The run's cleaner exits this case approved, for TAKE and EXEC.

### E2E-AUTH-047 — Approval is refused until the paperwork is complete
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/user-management#approval-criteria — the server requires a complete profile and every required document type present and `Approved`
- **Given** a second cleaner (not the run's main one). **When** the admin approves it with an incomplete profile, then with a complete profile and no document, then with the identity card still `Pending`, then with it rejected. **Then** the refusals are `employee.profile_incomplete`, `employee.documents_not_approved` three times; the contract stays `Pending`; the console hides Approve until the profile is complete.
- **DEV:** this cleaner exists only for the refusals and can serve the deletion cases (E2E-GDPR-020 onward).

### E2E-AUTH-048 — Approval needs a serviced country and a pay rate for the whole catalogue
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/user-management#approve-employee — the work country must exist and be serviced, and every active service and package needs a pay config in its currency
- **Given** a cleaner otherwise ready. **When** the admin approves with an unknown country id, with a country that is not serviced, and with a country whose currency has no pay rate for one active service. **Then** the refusals are `country.not_found`, `country.not_serviced` and `employee.pay_config_missing` once per uncovered catalogue entry, each named by the entry; the contract stays `Pending`.
- **DEV:** the last needs a currency without rates or a catalogue entry created for the case; skip it if neither exists. The operator-mismatch refusal needs a second company and is not testable (below).

### E2E-AUTH-049 — An admin rejects a cleaner and the cleaner reads the reason
- **Priority:** P1
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /admin-app/user-management#reject-employee — rejection stores the reason, which the cleaner reads word for word on the lock screen beside a Contact support action, and the push never carries it
- **Given** a `Pending` cleaner with a complete profile (the second cleaner). **When** the admin rejects with the reason "Photo of the identity card is unreadable" (`POST api/AdminEmployee/{employeeId}/reject`). **Then** the contract is `Rejected` with the reason; the cleaner is pushed `employee.registration_rejected` with no reason in it; the lock's Admin Approval row reads *Rejected* with the reason verbatim, untranslated, and offers Contact support; the cleaner can still sign in and edit the profile; a second rejection is refused `employee.already_rejected`; an over-long reason (501 characters) is refused `common.max_length`.
- **DEV:** rejecting an already approved cleaner is allowed by the server though the console offers Reject on `Pending` only; leave that to a separate decision.

### E2E-AUTH-050 — A rejected cleaner can be approved again
- **Priority:** P2
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#employee-documents — a rejected cleaner is approved from `Rejected` once the usual gates pass, approval clears the reason, and nothing returns them to `Pending` by itself
- **Given** the rejected second cleaner who then uploads a corrected document and completes the profile. **When** the cleaner has done so, then the admin approves. **Then** after the uploads the contract is still `Rejected`; the console offers Approve on it and Reject does not appear; the approval succeeds when the gates pass and the reason is cleared.
- **DEV:** uses the second cleaner; afterwards it is approved and has no work.

### E2E-AUTH-051 — A cleaner who is not approved cannot work
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/onboarding#admin-approval — until approved, a partner can sign in and edit the profile but cannot take or manage orders
- **Given** a `Pending` cleaner (and a rejected one), and a bookable order on the board. **When** each sends a take of the order (`TakeOrder` on :5000 and :5002), and opens the orders pages. **Then** both takes are refused `employee.not_approved`; the order gains no seat; the apps show the lock instead of the board; Profile, GDPR, Login, Register, Confirm e-mail and Forgot password stay reachable on web while the lock is on.
- **DEV:** needs one order from the BOOK flow; use it before the approved cleaner takes it.

## Admin sign-in and roles

### E2E-AUTH-052 — The admin signs in on the admin web
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/overview#internal-login — only a user with the Administrator profile can sign in, and the role decides what the console then opens
- **Given** the published DEV admin. **When** the admin signs in on `/login` (API: `POST api/AdminAuth/Login` on :5001). **Then** the answer carries `hasAdminAccess: true` and `adminRole`; the access token lasts 15 minutes; the sidebar opens with the entries the role may use; the admin audit log shows a successful `admin.session.login` row for the admin.
- **DEV:** the deployed console sits behind an Azure Static Web Apps gate that needs a Microsoft sign-in with the `admin_console` role before Cleansia's own login shows; a browser run of the admin web needs that identity, an API run does not. Create the three role accounts of E2E-AUTH-055 here once.

### E2E-AUTH-053 — The admin login refuses what it should
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#session-rows — an administrator's refused sign-in is an `admin.session.login` row that names a known account, and an unknown address names nobody
- **Given** the admin host. **When** the sign-in is sent with the admin's address and a wrong password, with an address nobody holds, and with the run customer's right credentials. **Then** the first two answer `validation.invalid_password`; the third answers `auth.insufficient_privileges` (a customer is not an administrator) and no token; the audit log shows failed `admin.session.login` rows, the first naming the admin, the second naming nobody and holding no address, the third naming the customer.
- **DEV:** wrong passwords count toward the admin's lock (E2E-AUTH-017); send one, not six.

### E2E-AUTH-054 — An admin changes their own password
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#edge-cases — a password change revokes the other sessions and spares the one performing it; an administrator's password is at least 12 characters
- **Given** a created test admin signed in on two clients. **When** the admin sends `POST api/AdminAuth/ChangePassword` with a wrong current password, a new one of 11 characters, a new one equal to the current, and finally a valid new one of 12 or more. **Then** the refusals are `auth.current_password_invalid`, `auth.admin_password_too_short` and `auth.same_reset_password`; the valid change succeeds; the other client's refresh token is then refused and the performing client still works; the old password fails and the new one signs in.
- **DEV:** never the published admin. Repeated wrong current passwords lock the account (`auth.account_locked`); send one.

### E2E-AUTH-055 — An Administrator creates a Manager, a Support and an Accountant
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /domain/roles/admin-role-gate#the-four-roles-and-the-five-sets — an administrator account carries one of four roles, and role assignment belongs to the Administrator alone
- **Given** the published admin (role Administrator). **When** the admin creates three accounts, one per role (`POST api/AdminUser/create`), one with an existing address, and each created admin signs in. **Then** each sign-in carries its `adminRole`; the duplicate is refused `admin_user.email_exists`; a created admin whose password is under 12 characters is refused `auth.admin_password_too_short`; the new accounts show in the Administrators list.
- **DEV:** the three accounts are named `e2e.<run>.manager@example.com`, `.support@` and `.accountant@`; deactivate them at the end of the run (E2E-AUTH-058).

### E2E-AUTH-056 — Each administrator role is served and refused by the server
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19 — the server is the gate: every admin-host route resolves its permission to a set of roles and answers 403 to a role outside it
- **Given** an Administrator, a Manager, a Support and an Accountant, and a subject customer. **When** each role calls: the Administrators list, `AdminUser/create`, `AdminEmployee/{id}/approve`, `AdminGdpr/export/{userId}`, `AdminGdpr/delete-account/{userId}` (against a throw-away customer with a live order, so a pass is refused by the order and not executed), `AdminAuditLog/get-paged`, and `AdminPayPeriod` list. **Then** the Administrators list: Administrator and Manager 200, Support and Accountant 403; create: Administrator only; approve, export and audit log: Administrator, Manager and Support 200, Accountant 403; delete-account: Administrator and Manager reach the handler (refused by the live order), Support and Accountant 403; pay periods: Administrator, Manager and Accountant 200, Support 403.
- **DEV:** the expectations come from `PolicyBuilder.Map`; a role change takes effect on a token only after its next refresh (below).

### E2E-AUTH-057 — The admin web shows each role only what the server allows
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web
- **Rule:** → /admin-app/overview#admin-roles — the app hides what the role lacks and a guarded route sends the role to the unauthorized page; the server remains the gate
- **Given** a signed-in Support and a signed-in Accountant. **When** each opens the sidebar and types the URL of a page outside their set (the Support the pay periods page, the Accountant the audit log). **Then** the sidebar omits the entries outside the set; the typed URL lands on `/unauthorized`; a forced API call for the same data is 403 (E2E-AUTH-056).
- **DEV:** the console gate of E2E-AUTH-052 applies.

### E2E-AUTH-058 — A deactivated administrator cannot sign in, and the self-guards hold
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19 — role assignment is audited and refuses the caller's own row; the last Administrator cannot be removed
- **Given** the Administrator and the created Support admin. **When** the Administrator deactivates the Support admin (`POST api/AdminUser/{userId}/deactivate`), the Support admin signs in, the Administrator activates it again, and the Administrator tries to deactivate itself and to change its own role (`POST api/AdminUser/{userId}/role`). **Then** the deactivated admin's sign-in fails `validation.invalid_password`; after activation it signs in; the self deactivation is refused `admin_user.cannot_deactivate_self` and the own-role change `admin_user.cannot_change_own_role`; a role change of another admin writes an `admin.user.set_role` audit row with the role before and after.
- **DEV:** the last-Administrator refusals (`admin_user.cannot_deactivate_last_admin`, `admin_user.cannot_demote_last_administrator`) are not run: with a second Administrator present a wrong call would land on a real account (below). Deactivate the three role accounts at the end of the run.

### E2E-AUTH-059 — A role change reaches a session within one access-token lifetime
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/overview#admin-roles — a role changed on the server reaches the session at the next refresh, within fifteen minutes, without a re-login
- **Given** the created Support admin signed in, holding a token with the role Support. **When** the Administrator promotes it to Manager. **Then** a call needing Manager (the Administrators list) is still 403 on the old token; after the token is refreshed it is 200 and the web stores the new `adminRole`; no bulk revocation happened.
- **DEV:** costs a refresh, not a 15-minute wait, if the test forces the refresh; waiting the 15 minutes proves the staleness bound.

## What the session leaves in the trail

### E2E-AUTH-060 — Customer sign-in, sign-out, confirmation and reset leave customer rows only
- **Priority:** P2
- **Role:** Customer · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#session-rows — every session act on a customer host is a `CustomerActionAudits` row; refresh rotations write none
- **Given** the run customer after its sign-ins, a refresh and a sign-out. **When** the admin filters the Customer actions segment, and reads the customer's timeline (`GET api/CustomerAudit/timeline?userId=`). **Then** the rows `customer.account.register`, `customer.session.login`, `customer.session.logout`, `customer.password.reset_requested` appear newest first with the act's evidence (method, client family, remember-me), the request's IP and device; no row exists for the refresh; the rows hold no name, e-mail, phone or token.
- **DEV:** the confirmation row (`customer.account.email_confirmed`) is absent because the SQL helper confirmed the address.

### E2E-AUTH-061 — A cleaner's sign-in leaves no audit row anywhere
- **Priority:** P2
- **Role:** Cleaner · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#session-rows — on a partner host none of the session rows is written, because the same commands serve cleaners whose session history belongs in no table
- **Given** the run cleaner after a failed and a successful sign-in on partner web and on the partner mobile host. **When** the admin searches the Customer actions, the admin audit log and the timeline by the cleaner's user id. **Then** no row records a sign-in, a sign-out or a refusal for the cleaner.
- **DEV:** the cleaner's admin-side acts (document and contract decisions) are rows of the admin table, which this case does not forbid.

### E2E-AUTH-062 — An admin's sign-in and sign-out are admin rows under the admin's company
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/auth-and-identity#session-rows — an administrator's sign-in is an `admin.session.login` row, success or failure, and the sign-out `admin.session.logout`; never a customer row
- **Given** a created test admin that signs in, signs out and signs in with a wrong password. **When** the published admin reads the admin audit log filtered by that actor. **Then** the log shows `admin.session.login` (success), `admin.session.logout` and `admin.session.login` (failure, naming the account), each stamped with the admin's role and company; no `customer.session.*` row exists for that user; a refresh adds none.
- **DEV:** the audit log needs a role of Support or above to read.

### E2E-AUTH-063 — The auth endpoints share a rate window of ten requests a minute
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /product/business-rules#customer-record — anonymous refusals are bounded by the `auth` rate-limit window of 10 requests per minute per real client IP
- **Given** one client IP. **When** twelve wrong sign-ins for an unknown address are sent within one minute. **Then** the first ten answer `validation.invalid_password` and the later ones are answered 429; after the minute the endpoint answers normally; the audit rows are bounded by the same window.
- **DEV:** the test runner's own traffic shares the IP; run this last in the file and let the minute pass. Behind a proxy the "real client IP" is the forwarded one; say which was observed.

## Language and profile of the account

### E2E-AUTH-064 — The account's language follows the app and survives a session start
- **Priority:** P2
- **Role:** Customer · Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#account-language — the registration stamps the language the app shows, the picker sends the language the app now resolves to, and a session start re-states only a language the user chose
- **Given** a customer registered with `cs` and a cleaner registered with `en`. **When** `GET api/User/GetCurrent` is read, the customer picks German in the app, signs out and in, and a user on "System" starts a session. **Then** the profiles carry `cs` and `en`; the picker change is on the server after the pick (`de`); a session start by a user on "System" does not overwrite a language chosen on another client; the order e-mails later follow the language of each order, not this one.
- **DEV:** German must be a seeded language on DEV.

### E2E-AUTH-065 — A customer with no phone can save a profile, and a new phone must be valid
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#account-language — both customer apps send a missing phone as `""`, and a phone that differs from the stored one has to be valid in international form
- **Given** the run customer with no phone. **When** `PUT api/User/UpdateCurrentUser` saves the names with the phone `""`, then with `0601234567`, then with `+420601234567`. **Then** the first succeeds and the phone stays empty; the second is refused `validation.invalid_phone_number`; the third is saved; a later save that sends the stored phone back unchanged succeeds whatever its form.
- **DEV:** use a valid Czech mobile number written as `+420` and nine digits.

### E2E-AUTH-066 — A password account never reports a placeholder name
- **Priority:** P2
- **Role:** Customer · Cleaner
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#apple-account-name — `isNamePlaceholder` is false for every Google and password account whatever its name, and always false on a partner host
- **Given** a customer registered as *Test Customer* and a cleaner. **When** `GET api/User/GetCurrent` is read on :5003, :5004, :5000 and :5002. **Then** every answer carries `isNamePlaceholder: false`; saving the profile with other names keeps it false.
- **DEV:** an Apple account cannot be created here, so the `true` rows of the table are not testable.

## Not testable on DEV

- **Google and Apple sign-in and sign-up.** They need a real identity-provider token; `auth.invalid_google_token`, `auth.invalid_apple_token`, `auth.social_account_not_found`, the Apple name rules and placeholder repair, the account-type refusals (`auth.google_type_error`, `auth.apple_type_error`, `auth.internal_type_error`, `auth.external_type_error`) and the `en` language of a social account cannot be reached.
- **Reading any e-mail.** `@example.com` never receives mail, so the confirmation code, the reset code and every notification e-mail are unreadable. Confirmation is done by the SQL helper, which means the *confirmer chooses the password* path (replacing the registration hash, the legacy no-password fallback, the auto-submit wait on the password form) and the 15-minute code expiry are not exercised; E2E-AUTH-022 and a correct-code confirmation run only if 1b plants a known code hash.
- **A deactivated company** (`auth.company_deactivated`) and the **operator mismatch** at approval (`employee.work_country_operator_mismatch`). DEV holds one operating company; deactivating it would refuse every cleaner of the run, and no second company exists to mismatch.
- **The business register.** The lookup is switched off on DEV, so `validation.registration_number.not_registered`, `employee.business_ceased`, `employee.trade_licence_inactive` and `employee.business_registry_unavailable` cannot occur.
- **The last Administrator guard** (`admin_user.cannot_demote_last_administrator`, `admin_user.cannot_deactivate_last_admin`): a correct test needs a company with one Administrator, and DEV's Administrators are real accounts.
- **Waiting out lifetimes.** The 1-, 30- and 90-day refresh lifetimes and the 24-hour-to-30-minute web change are checked only through the claims; nobody waits for them.
- **Device-level behaviour.** Strong-password suggestion from iCloud Keychain or Google Password Manager (it needs the live associated domain and a signed-in device), push delivery of the approval and rejection decisions to a phone, and the shared-phone push registration rule need real devices or push services and are not agent-driven in Phase 1.
- **Load.** The rate window is probed once; nothing here measures capacity.
