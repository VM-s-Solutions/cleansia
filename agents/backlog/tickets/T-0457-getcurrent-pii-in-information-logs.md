---
id: T-0457
title: S6 — GET /api/User/GetCurrent writes the caller's email, name, phone and birth date to Information-level logs on all five hosts
status: ready
size: S
owner: backend
created: 2026-07-30
updated: 2026-08-01
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
`agents/backlog/security/user-profile-avatar.md`.

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

## Review
<!-- reviewer + security verdicts here; AC3 must name the assertion that flips -->
