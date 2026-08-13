---
id: T-0457
title: S6 — GET /api/User/GetCurrent writes the caller's email, name, phone and birth date to Information-level logs on all five hosts
status: done
size: S
owner: backend
created: 2026-07-30
updated: 2026-08-05
depends_on: [T-0446]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

Filed from the **T-0446 security gate** (finding **SEC-2**). Full write-up:
`agents/archive/2026-08/backlog/security/user-profile-avatar.md`.

**This is a pre-existing S6 violation. It is not caused by T-0446 and must not block it.** It is
filed now, and sequenced **pre-demo**, for one reason: **the demo will be logging real people's
data**, and DEV is already live with the owner's iPhone pointed at it.

`RequestLoggingMiddleware` writes the first **500 bytes** (`ResponseBodyLimit`) of every response body
at **Information** level. For `GET /api/User/GetCurrent` the response is a `MyProfileDto`, whose PII
block — `email`, `firstName`, `lastName`, `phoneNumber`, `birthDate` — **closes at index ~264–302**
(PM's own measurement across three representative profiles: a 7-char email/short name, a typical
name, and a long Ukrainian name). That is **entirely inside** the window, on **every** request, on
**all five hosts**.

The suppression list does not cover it. `IsSensitivePath` matches only `/auth/`, `/login`,
`password` and `/order/lookup`:

```csharp
return pathValue.Contains("/auth/") ||
       pathValue.Contains("/login") ||
       pathValue.Contains("password") ||
       pathValue.Contains("/order/lookup");
```

The route is `[HttpGet("GetCurrent")]` on `UserController`
(`src/Cleansia.Web.Customer/Controllers/UserController.cs:17`) → `/api/User/GetCurrent`, matching
**none** of them.

**S6 is verbatim:** *"No email, phone, name, address, payment/Stripe detail, JWT, refresh token, or
confirmation code in logs at Information level or higher."* This is arguably the **largest S6
exposure in the codebase**, because `GetCurrent` is the most-called authenticated endpoint on the
platform — every app calls it on launch, on resume, and after every profile save.

**Sequenced behind T-0446.** Not because of the finding's priority, but because both tickets write
the **same five files** — T-0446's AC9 fixes the truncate/redact ordering in the same methods. See
the lane note below.

## Deliberation

**No-decision.** This is the enforcement of an existing law (S6) on an endpoint that was missed, not
a new behaviour or a new decision. No panel. The one judgement call — *suppress vs. redact* — is
posed as AC1 and belongs to the implementer + reviewer, not to a panel.

## Acceptance criteria

- [ ] **AC1 (the design call, made explicitly)** — Choose and **write down the reasoning for** either
      (a) adding the profile routes to `IsSensitivePath` so the whole body is suppressed, or (b)
      extending `SensitiveFieldRegex` with `email`, `firstName`, `lastName`, `phoneNumber`,
      `birthDate` so the fields are redacted body-wide. Note the trade: (a) is airtight for this route
      but path-matching is a denylist that the next endpoint will miss again; (b) is field-based and
      therefore generalises, but widens a regex that runs on every response. **Whichever is chosen,
      the fix must hold for every host and for the sibling profile routes on the partner and admin
      APIs**, not for `/api/User/GetCurrent` alone. Evidence: the reasoning in the PR body and in this
      ticket's status log.
- [ ] **AC2** — Given a signed-in user calls `GET /api/User/GetCurrent`, When the middleware logs the
      response on **each of the five hosts**, Then the emitted message contains none of the caller's
      `email`, `firstName`, `lastName`, `phoneNumber` or `birthDate` values. Evidence: a `[Theory]`
      over the five middleware types, in the shape of
      `src/Cleansia.Tests/Logging/RequestLogSignedUrlRedactionTests.cs`.
- [ ] **AC3 (Gate 0.5 leg 1 — mutation proof, and this ticket exists BECAUSE a test failed this)** —
      The test payload is a **realistic serialized `MyProfileDto`** of **~758–798 bytes**, not a
      hand-trimmed fixture. It must go **RED against the code as it stands today** and green after the
      fix. **The reviewer names the assertion that flips.** A fixture short enough that the truncation
      hides the PII is the exact failure mode that produced T-0446's AC9 — do not repeat it one ticket
      later.
- [ ] **AC4** — `userId` continues to be logged (S6 explicitly permits it, and it is the only handle
      an operator has). The fix must not blind the log; it must de-identify it. Evidence: an assertion
      that the `User:` field is still populated.
- [ ] **AC5 (Gate 8)** — `dotnet build` + `Cleansia.Tests` green, with real counts. If a suite cannot
      run locally, it is named **DEFERRED-TO-CI / UNVERIFIED-LOCALLY** — never reported as PASS.

## Out of scope

- The `blobUrl` / `base64Content` truncate-before-redact ordering bug — that is **T-0446 AC9**, in
  the same files. **Do not fix it here**; take T-0446's version of these files as your baseline.
- The `RedactQueryString` / `EmailQueryParamRegex` path (query strings already redact email).
- Any change to what the endpoint *returns*. The DTO is correct; the **log** is the defect. A DTO
  change would drag in `nswag-regen` and put this on the demo critical path, which is precisely what
  filing it separately avoids.
- Auditing every other endpoint for S6. Worth doing, but that is an `/audit` sweep, not this ticket.
  If the implementer spots others while here, **list them in the status log** for the PM to file.

## Implementation notes

- **Archetype:** there is no feature archetype here — the canonical reference is the sibling test
  `src/Cleansia.Tests/Logging/RequestLogSignedUrlRedactionTests.cs` (reflection over the five host
  middleware types, a `CapturingLoggerFactory`, `[Theory]` + `[MemberData]`). Mirror it.
- **Five copies, one change.** Each host carries its **own** copy of `RequestLoggingMiddleware`. The
  four non-Customer hosts are offset from `Cleansia.Web.Customer` by 4 lines — `IsSensitivePath` is at
  `:180` on Admin / Mobile.Customer / Mobile.Partner / Partner and `:184` on Customer; the regex
  attribute is at `:199` / `:203`. **Check the line before editing.** All five must change together;
  four-of-five is a hole.
- **Shared-file lane —** `src/Cleansia.Web.{Admin,Customer,Mobile.Customer,Mobile.Partner,Partner}/Middleware/RequestLoggingMiddleware.cs`:
  **T-0446 (AC9) → T-0457.** Do not start until T-0446's middleware change has landed, and never
  `git restore` these files.
- If AC1 lands on option (b), remember `RegexOptions.IgnoreCase` is already set and the replacement
  callback preserves the matched field name — so adding names to the alternation is genuinely the
  whole change.

## Status log
- 2026-07-30 — draft (created by pm from the T-0446 security gate, finding SEC-2; no-decision, no panel needed)
- 2026-07-30 — **not `ready`**: `depends_on: [T-0446]` is unsatisfied (shared-file lane on all five middleware copies). DoR items 2–7 are otherwise met.
- 2026-08-01 — **`draft` → `ready`. `depends_on: [T-0446]` is satisfied** — T-0446 merged `a63b776e`
  (#176), so the **five copies of `RequestLoggingMiddleware.cs` are released** and this ticket is now
  the lane's sole writer. DoR: AC observable ✅ · sized S ✅ · deps `done` ✅ · `manual_steps: []` (it
  touches **no DTO**, so it adds nothing to any owner bundle) ✅ · `security_touching: true` +
  `layers: [backend]` ✅ · archetype = the five-file middleware cluster T-0446 AC9 just moved ✅ ·
  no-decision note already recorded ✅.
- 2026-08-01 — **P1: this is the highest-priority unblocked backend ticket.** DEV is live, the owner's
  iPhone is pointed at it, and `GET /api/User/GetCurrent` — the most-called authenticated endpoint on
  the platform — is writing the caller's email, first name, last name, phone number and birth date
  into Information-level logs on all five hosts on **every** request. It is accruing exposure right
  now.
- 2026-08-01 — **three things the merged T-0446 changed about how you implement this. Read them
  first:**
  1. **The composition is now `TruncateBody(RedactSensitiveFields(...))`** on all five hosts
     (`Middleware/RequestLoggingMiddleware.cs:182` on `Cleansia.Web.Customer`). AC9 already inverted
     it, so **do not re-derive the ordering** — build on it.
  2. **`IsSensitivePath` has grown** and is the precedent for the "suppress" arm of AC1: it now covers
     `/adminauth/`, `/savemydocuments`, `/savephotos`, `/uploadphoto`, `/getphotos`, `/photos/`
     alongside the original four, each with a comment saying **which** free text it is suppressing.
     Match that discipline — a bare path string with no reason is not the house style here.
  3. **You inherit an explicit debt list.** `RedactionUnmaskedFreeTextGuardTests.AcceptedPreExisting`
     names seven members as *"pre-existing exposure, owned by T-0457"*:
     `CreateAdminUser.Command.{FirstName,LastName,PhoneNumber}` and
     `GetMyDocuments.{MyDocumentDto,Response}.{Description,ReviewNotes}`. **Whatever this ticket does
     must let those entries be deleted** — an entry that survives is a claim nobody owns. The T-0446
     reviewer also recorded a boundary worth knowing: beyond a ~155-character document description,
     `ReviewNotes` crosses out of the pre-change window, so "pre-existing" was a typical-case claim,
     not a universal one.

- 2026-08-04 — **implemented (backend). AC1 answered (b)+, with the reasoning below; the headline is that
  the premise of "one endpoint" is wrong by two orders of magnitude.**

  **The mechanism, established first.** Not a structured log call and not a serialized DTO: it is
  `RequestLoggingMiddleware.SafeBody`, one copy per host, which slices every request and response body
  into Information. It is generic over every route, so **every endpoint returning PII has the same
  exposure** and fixing one route is theatre. Measured, not asserted: a wire-DTO walk over the five hosts
  found **152 PII-shaped members on 80+ routes** — the caller's profile, every admin user/employee list
  and detail, every order's customer contact block, the referral lists, the whole subject-access export.
  `GET /api/User/GetCurrent` is 5 of those 152.

  **AC1 — option (b), field-name redaction, but SHAPED rather than enumerated.** New
  `ContactIdentityFieldRegex` on all five hosts: `[A-Za-z]*email | [A-Za-z]*phone[A-Za-z]* |
  [A-Za-z]*firstName | [A-Za-z]*lastName | fullName | birthDate`. Quote-anchored on both sides, so a
  name must match whole — which catches `customerEmail`/`actorEmail`/`recipientEmail` while leaving
  `emailTemplateId` (an id) alone, and is why `*email` takes no suffix while `*phone*` does. Path
  suppression was rejected as the primary tool for the reason the ticket names: it is a denylist the next
  endpoint misses — demonstrated twice here, `/auth/` missing `/api/AdminAuth/…` (T-0446's find) and
  `/gdpr` missing `/api/v1/AdminGdpr/export/{userId}` (found here, see T-0509).

  **What makes it fail closed** — the part the AC did not ask for and the fix is worthless without.
  `src/Cleansia.Tests/Logging/RequestLogPiiSurfaceGuardTests.cs` walks every wire DTO reachable from a
  controller action on the five hosts, reads the token list **out of the live compiled regex** (so guard
  and middleware cannot drift), and fails naming the DTO, member and routes when a PII-shaped member is
  neither redacted, nor on a route suppressed on all five hosts, nor on a reasoned exception list. That
  list has **one** entry (`EmailTemplateId`, an id).

  **A second regex, not a wider one.** Merging contact identity into `SensitiveFieldRegex` made
  `RedactionUnmaskedFreeTextGuardTests` flag essentially every string member of every DTO. The model
  there is "a token collapse frees window", which is true of an unbounded value (base64/SAS/JWT) and
  false of a bounded one — redacting a short name *lengthens* the body. Two named regexes keep that
  guard's calibration honest; `RedactSensitiveFields` runs both passes.

  **Inherited debt: all seven `AcceptedPreExisting` entries deleted, and the list is now empty.**
  `CreateAdminUser.Command.{FirstName,LastName,PhoneNumber}` are redacted by the new pass;
  `GetMyDocuments.{MyDocumentDto,Response}.{Description,ReviewNotes}` are free text no denylist reaches,
  so `/getmydocuments` joined `IsSensitivePath` beside the `/savemydocuments` write side that was already
  there.

  **Also suppressed while in the method (both found by the guards, not by hand):** `gdpr/` replacing
  `/gdpr` (see T-0509) and `/admincompany/` (Cleansia's own account number/IBAN/BIC — not confidential,
  it prints on every receipt, but suppressed rather than excepted, because the alternative is a guard
  entry reading "this bank account is fine to log").

  **Out-of-scope sightings, for the PM to file:** the free-text surface a name-shaped heuristic cannot
  reach (`Notes`, `Description`, `Reason`, `ApprovalNotes`, `MissingFields`, `LegalEntityName`, address
  lines) is untouched by this ticket and is only covered where a route is suppressed. That is the
  `/audit` sweep the ticket anticipated.

- 2026-08-05 — **`ready` → `done` (PM reconciliation pass 4).** **Verified at HEAD, not from the commit
  message.** All five `Cleansia.Web.{Admin,Customer,Mobile.Customer,Mobile.Partner,Partner}/Middleware/RequestLoggingMiddleware.cs`
  carry `ContactIdentityFieldRegex` (2 references each — the declaration and the call in
  `RedactSensitiveFields`); four-of-five would have been the hole and it is not. The DTO-walking guard is
  real and it reads the token list out of the **live compiled regex** rather than restating it:
  `Cleansia.Tests/Logging/WireSurface.cs` `ReadTokens("ContactIdentityFieldRegex")` reflects the private
  static member off the middleware type and parses the alternation out of `regex.ToString()`, so a token
  added to the middleware widens every guard that reads it and no guard can hold a stale copy.
  `RequestLogPiiSurfaceGuardTests` walks the wire surface with an `Assert.InRange(membersWalked, 1000, 20000)`
  anti-vacuity floor and an `Assert.NotEmpty(WireSurface.ReadContactIdentityTokens())`, so it cannot go
  green over an empty scan. Shipped in `b9753e85`. AC evidence and the both-ways mutation proof were
  already written into `## Review` by the implementing lane — the only thing missing was this transition.

## Review
<!-- reviewer + security verdicts here; AC3 must name the assertion that flips -->

**AC3 — the assertion that flips, and it was run both ways.**
`RequestLogProfilePiiRedactionTests.ProfileResponse_ContactIdentity_NeverReachesTheLog`, the
`Assert.DoesNotContain(value, message)` inside `Assert.All`. Fixtures are real `MyProfileDto`s built
through the production mapper with a real SAS, at three name lengths (short / typical Czech / long
Ukrainian), 700–1000 bytes; each case asserts non-vacuity first (body exceeds the 500-byte window AND
the last PII value closes inside it) so truncation cannot be what removes the values.

- **Before the fix** (run first, red): `Failed: 10, Passed: 5`, message
  `String: ···"5 | Body: {"email":"jk@x.cz","firstName":"··· Found: "jk@x.cz"` — on all five hosts.
- **After the fix**: green.
- **Mutation-proven after the fact** by deleting the `ContactIdentityFieldRegex().Replace(...)` line from
  `RedactSensitiveFields` on all five hosts: `Failed: 10, Passed: 111` in the Logging suite, identical
  message. Restored **byte-exact**, sha256 verified on all five files.

**AC4** — `ProfileResponse_StillCarriesTheCallerUserId` asserts `User: {userId}` is still in the message
(the harness gained an optional authenticated-principal parameter for this).

**AC5** — `Cleansia.Tests` **3017 passed / 0 failed** (baseline 2945). `Cleansia.IntegrationTests`
**132/132**, `Cleansia.HostTests` **120/120**, both run locally. `dotnet build` clean.
