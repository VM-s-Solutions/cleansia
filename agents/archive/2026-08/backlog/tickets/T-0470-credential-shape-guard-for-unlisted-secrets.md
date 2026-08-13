---
id: T-0470
title: Credential-shape guard — catch a secret whose field name was never in the redaction token list
status: done
size: S
owner: backend
created: 2026-08-01
updated: 2026-08-06
depends_on: [T-0446]
blocks: []
stories: []
adrs: []
layers: [backend, architect]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

T-0446 closed two classes of log-redaction defect and **left one explicitly open.** This ticket is
that one, filed at close-out so it does not evaporate with the ticket that found it.

### What T-0446 closed

1. **A field whose name IS in the token list is now redacted in behaviour, not just in the list.**
   AC9 swapped the composition to `TruncateBody(RedactSensitiveFields(...))` on all five hosts
   (`src/Cleansia.Web.Customer/Middleware/RequestLoggingMiddleware.cs:182`; the other four hosts are
   the same call four lines earlier). Before that, the middleware truncated *before* it redacted and
   the regex needs a complete quoted string — so on a profile response the redaction fired **0% of the
   time**.
2. **A DTO whose redaction UNMASKS free text has its route suppressed.**
   `src/Cleansia.Tests/Logging/RedactionUnmaskedFreeTextGuardTests.cs` walks the wire DTOs of every
   route, reads the token list **from the live regex** rather than restating it, and fails when
   redacting a token frees window space that pulls narrative PII into view.

### What it did NOT close, in the T-0446 reviewer's own words

> **A secret whose field name was never in the redaction token list is caught by nothing.**

Nothing in the middleware, the guard suite, or any reviewer checklist looks at a *value* and asks
whether it is shaped like a credential. The only thing standing between a new secret-bearing DTO
member and Information-level logs is that somebody thought to add its name to
`SensitiveFieldRegex()` (`RequestLoggingMiddleware.cs:244`, ×5 hosts).

### Why this is worth an S, and the evidence is unusually strong

**Both live Stripe credentials found this sprint were in exactly this class**, and both are now in the
token list precisely *because* they were found:

| Credential | How it was found |
|---|---|
| `setupIntentClientSecret` | found in round 5 of the T-0446 review — the exact body that logged a raw setup-intent secret |
| `ephemeralKey` | **found by luck.** It happened to sit behind an **already-redacted** field, so the unmasking guard surfaced it while looking for something else. Nothing was pointed at it |

The second one is the argument. A guard that catches a live payment credential **as a side effect of
looking for something else** is telling you it had no coverage. And the T-0446 reviewer recorded the
generalisation itself: *"every time a judgement was replaced with a measurement, the measurement found
something the judgement had missed."*

**The cost is low because the hard half already exists.** This is a **sibling guard over the same
wire-DTO walk** — the route→DTO enumeration, the recursive member flattening, the collection
unwrapping, the `IsAppServicesDto` boundary, the host-assembly scan and the anti-vacuity self-check
are all already written in `RedactionUnmaskedFreeTextGuardTests.cs` and already run in CI
(`Cleansia.Tests`, `backend-ci.yml`). The new part is a predicate and a curated exception list.

**The existing guard found two live credentials within minutes of existing.** That is the expected
value here, not a hypothetical.

## Acceptance criteria

- [ ] **AC1 (the guard, name-shaped)** — Given the same route→wire-DTO walk the existing guard
      performs, When a string member's **name** matches the credential-shape heuristic (`*Secret*`,
      `*Token*`, `*Key*`, `*Password*` — case-insensitive, whole-word-ish so `Keyword`/`Tokenizer`
      don't false-fire), Then the member must be **either** in the live `SensitiveFieldRegex()` token
      list **or** on a curated exception list with a written reason. Otherwise the test **fails**,
      naming the DTO, the member and every route that emits it.
- [ ] **AC2 (the guard, value-shaped)** — The same walk also fails on a member whose **example/default
      or documented value shape** is a known credential prefix — `sk_`, `ek_`, `seti_`, `pi_`. State
      plainly in the test's doc comment **what this leg can and cannot see**: a static type walk sees
      names and types, not runtime values, so this leg is only meaningful where a shape is discoverable
      statically (a constant, an attribute, a default). **If it turns out not to be discoverable, say
      so under Gate 0.5 leg 3 and drop the leg** — do not ship a test that asserts nothing in order to
      satisfy an AC. `pi_` in particular is a *Stripe object id, not a secret* — `PaymentIntentId` is
      already on the sibling guard's `StructuralMembers` list for exactly that reason — so if `pi_` is
      kept it must be justified separately from the three that are secrets.
- [ ] **AC3 (same exception discipline as the sibling guard — this is the load-bearing AC)** — The
      exception list follows the discipline already established at
      `RedactionUnmaskedFreeTextGuardTests.cs:58-89` and `:91-117`: **short, per-entry reasoned, and
      every entry is a claim a human can argue with.** Reuse the sibling's two-list split if it fits —
      *structurally-not-a-secret* (a Stripe object id, a language code) versus
      *accepted-pre-existing-and-owned-by-ticket-X*. **A newly-added member may NEVER be silenced by
      adding it to the list** — that is the whole point of the guard, and the sibling's comment already
      says so in those words. Evidence: the list, and the reasoning next to each entry.
- [ ] **AC4 (Gate 6.5 / Gate 0.5 leg 1 — mutation-proven, and the mutation must be REAL)** — The guard
      goes **RED** when a plausible new secret-bearing member is added to a wire DTO without a token,
      and **GREEN** again when the token is added. The reviewer **names that test** and records both
      counts. **The honest mutation is `ephemeralKey`**: temporarily remove it from
      `SensitiveFieldRegex()` on the five hosts and confirm the guard names it — i.e. prove the guard
      would have caught, deliberately, the credential that was found by accident. Restore byte-exact
      (sha256 both sides, `git diff` clean).
- [ ] **AC5 (anti-vacuity — the sibling already has this and it is not optional)** —
      `RedactionUnmaskedFreeTextGuardTests` carries `Guard_ActuallyInspectsTheWireSurface()` because a
      reflection-driven guard that discovers **zero** routes passes silently. This guard needs the
      equivalent: assert a non-trivial lower bound on routes and members walked, so a future refactor
      that breaks discovery reddens instead of going quiet. A guard that inspects nothing is a
      **non-run**, not a pass (Gate 0.5 leg 2).
- [ ] **AC6 (five-host consistency)** — The token list lives in **five copies** of
      `RequestLoggingMiddleware.cs`. Whatever this guard reads, it must fail if the five diverge —
      four-of-five is a hole. If the sibling guard already asserts this, cite it and do not duplicate;
      if it does not, add it here.
- [ ] **AC7 (Gate 8)** — `dotnet build` + all three suites green **with real counts**:
      `Cleansia.Tests`, `Cleansia.IntegrationTests`, `Cleansia.HostTests`. All three ran locally in
      ~5m30s this sprint (sprint-14 §2.9), so **"DEFERRED-TO-CI" is not available by default** — it may
      be claimed only after actually attempting the run and finding Docker down **in this environment**,
      and must say so in those words.

## Out of scope

- **Adding tokens to `SensitiveFieldRegex()` speculatively.** This ticket builds the detector. If it
  finds a real unlisted credential, fix that in the same change and say so loudly; do not pre-emptively
  widen the regex against members the guard does not flag.
- **The PII-in-logs problem** — that is **T-0457** (`GetCurrent` writes email/name/phone/birth date at
  Information level on all five hosts). Different failure: T-0457 is *narrative PII nobody redacts*,
  this is *a credential nobody named*. They share the five middleware files, so **serialize** — see
  Implementation notes.
- **Secret scanning of the repository / CI logs / git history.** Different tool, different scope.
- **Changing the truncate/redact composition.** T-0446 AC9 settled it; build on it.
- **`Cache-Control` / content-type on blob reads** — T-0464 / T-0465.

## Implementation notes

- **Archetype — copy it, do not re-invent it:** `src/Cleansia.Tests/Logging/RedactionUnmaskedFreeTextGuardTests.cs`
  (373 lines). Reuse `RoutesWithTheirWireTypes()`, `FlattenedMembers()`, `UnwrapCollection()`,
  `IsAppServicesDto()`, `HostAssemblies()`, `ReadRedactionTokens()`. **`ReadRedactionTokens()` is the
  key one** — it parses the live regex so a token added to the middleware automatically widens the
  guard. Do the same here rather than restating the list; a guard with its own copy of the list drifts.
- **Where it goes:** a sibling file in `src/Cleansia.Tests/Logging/`. Whether it is a second test class
  or a second `[Fact]` on the existing one is the implementer's call — **prefer a separate class**, so
  a failure names *which* property broke.
- **The current token list, for grounding** (`RequestLoggingMiddleware.cs:244`, identical on all five
  hosts): `password`, `currentPassword`, `newPassword`, `confirmPassword`, `token`, `refreshToken`,
  `accessToken`, `clientSecret`, `setupIntentClientSecret`, `apiKey`, `base64Content`, `fileData`,
  `fileBase64`, `blobUrl`, `ephemeralKey`.
- **Expect false positives on the first run, and treat that as data.** `*Key*` will hit things like
  `LanguageKey`/`CacheKey`, `*Token*` will hit anything push-related. **If the exception list needs
  more than ~15 entries, stop and tell the PM** — that is the signal the heuristic is wrong-shaped, not
  that the list should grow. It is also the point at which this stops being an `S`.
- **⚠️ SHARED-FILE LANE — the five `RequestLoggingMiddleware.cs` copies.** Lane order:
  **T-0446 ✅ → T-0457 → T-0470**. This ticket only *reads* the five files if it reads the token list
  from the assembly rather than the source — **prefer reading from the compiled regex**, which removes
  it from the lane entirely and is what the sibling guard already does. **If it must edit them (AC4's
  mutation, or a real find), serialize behind T-0457** — all five must move together; four-of-five is
  a hole, and the line offsets are not uniform (`Cleansia.Web.Customer` is +4 lines vs the other four).
- **No-decision note (skips the deliberation panel):** no new behaviour and no open architectural
  decision. It applies an existing law (**S6**) with an existing mechanism (the wire-DTO walk) to a
  gap the T-0446 security gate named explicitly. The one judgement call — the exact heuristic and its
  exception list — is AC1/AC3 and belongs to the implementer plus the reviewer. **Escalate to an
  architect panel only if AC1's false-positive rate forces a different detection strategy** (e.g.
  attribute-based marking of secret-bearing members, which *would* be a new pattern).

## Status log
- 2026-08-01 — draft (created by pm at the sprint-14 close-out, from the class T-0446 closed **around**
  and deliberately left open. Filed rather than folded into T-0446: that ticket is `done`, merged as
  `a63b776e` (#176), and re-opening a closed demo-path ticket to add a new guard is the compression
  this backlog exists to prevent.)
- 2026-08-01 — **not `ready`** by one item only: DoR 1 (dedup) ✅ — searched `INDEX.md` and
  `backlog/audits/`; the nearest neighbours are T-0457 (PII, not credentials) and the shipped
  `RedactionUnmaskedFreeTextGuardTests` (unmasking, not naming), and this overlaps neither. AC ✅ ·
  sized S ✅ · `depends_on: [T-0446]` **satisfied** ✅ · `manual_steps: []` ✅ · `security_touching: true`
  + layers ✅ · archetype identified ✅ · no-decision note recorded ✅.
  **Sequencing: post-demo, and behind T-0457 on the middleware lane.** Not pre-demo, because — unlike
  T-0457 — there is **no known live exposure**: both credentials that were in this class are now in
  the token list. This ticket buys the **next** one, which is exactly the argument that put T-0439 and
  T-0454 behind the waves they guard.

- 2026-08-04 — **cross-note from T-0457 + T-0509. Three things changed under this ticket; none of them
  close it, and one of them makes it cheaper.**
  1. **The archetype moved and grew.** The wire walk this ticket was told to copy —
     `RoutesWithTheirWireTypes()` / `FlattenedMembers()` / `UnwrapCollection()` / `IsAppServicesDto()` /
     `HostAssemblies()` / `ReadRedactionTokens()` — is now extracted into
     `src/Cleansia.Tests/Logging/WireSurface.cs` and shared by three guards. **Do not fork it; call it.**
     Two sibling guards to mirror for structure and exception discipline:
     `RequestLogPiiSurfaceGuardTests` (name-shaped, one exception entry) and
     `RequestLogPayoutPathSuppressionTests.EveryRouteCarryingAPayoutIdentifier_IsSuppressedOnEveryHost`
     (route-shaped, zero exceptions).
  2. **`ReadRedactionTokens()` no longer means "the token list".** There are now TWO regexes on each host
     — `SensitiveFieldRegex` (credentials/payloads, literal names, unbounded values) and
     `ContactIdentityFieldRegex` (PII, shaped). `ReadRedactionTokens()` returns only the first;
     `WireSurface.IsRedacted(name)` answers "would the middleware redact this?" across both, and treats a
     token as a **regex fragment** rather than a literal, since the PII family is shaped. **This ticket
     wants `IsRedacted`, not `ReadRedactionTokens`** — comparing by equality would report a shaped-covered
     member as unprotected.
  3. **AC4's honest mutation still works exactly as written** (`ephemeralKey` is still a literal token in
     `SensitiveFieldRegex`), and the five middleware copies are still byte-identical from
     `RedactSensitiveFields` down — verified by sha256 during T-0457.

  **The residue is unchanged and is still real.** T-0509 swept the payout family, so `Iban` /
  `AccountNumber` / `HolderName` / `Swift` are covered by a *derived route guard* rather than by a name
  list — they are no longer an example of this ticket's gap. **A credential whose field name was never in
  the token list is still caught by nothing.** T-0457's PII guard does not overlap it: that one detects
  members whose name says what they hold, and the whole point here is a name that does not.

- 2026-08-05 — **implemented. The guard found a live credential on its first run, which is the outcome
  the ticket predicted.** `src/Cleansia.Tests/Logging/RequestLogCredentialShapeGuardTests.cs` (28 tests).

  **The find:** `RegisterDevice.Command.DeviceToken` — the raw FCM/APNs push token — reached an
  Information-level request-body log on **every device registration**, on both
  `/api/Device/Register` route shapes, on all five hosts. `token` is a literal in a quote-anchored
  alternation, so it never matched `deviceToken`. The same class of value is already redacted when it
  is called `Token` (`RegisterLiveActivityToken.Command`) and suppressed when it is called
  `TrustedDeviceToken` (it rides `/Auth/Login`) — the arbitrariness of a name list, measured.
  **Fixed in the same change:** `deviceToken` added to `SensitiveFieldRegex` on all five hosts
  (five copies verified byte-identical from `RedactSensitiveFields` down, sha256, before and after).
  The literal was chosen over a shaped `[A-Za-z]*token`: shaped would also redact `IdempotencyToken`,
  which the guard classifies as structurally-not-a-secret and which is the support correlation handle
  for a disputed subscribe — and "do not widen speculatively" is this ticket's own out-of-scope line.
  The guard is now what catches the next `pushToken`.

  **Knock-on, and it is the cross-wiring working.** Making `deviceToken` a token made it
  window-freeing for `RedactionUnmaskedFreeTextGuardTests`, which immediately flagged
  `RegisterDevice.Command.Platform` behind it. `Platform` is admitted only as `"android"` or `"ios"` by
  the command's own validator, so it went on `StructuralMembers` with that reason.

## AC-by-AC

- **AC1 ✅** — name-shaped, and **whole-word by PascalCase split**, not substring: a member is flagged
  when one of its words IS `secret`/`token`/`key`/`password` (plural allowed). `Keyword`, `Tokenizer`,
  `Monkey`, `Turnkey`, `Keychain`, `PasswordlessLogin`, `Base64Content` are pinned as non-matches in
  `TheCredentialShape_CoversTheseNamesAndStopsAtThose`, because false positives are the whole risk here
  — a guard whose first run is mostly noise gets switched off. Coverage is asked of
  `WireSurface.IsRedacted` (both regexes), not `ReadRedactionTokens`, per the 2026-08-04 cross-note.
- **AC2 ⛔ dropped after measurement, exactly as the AC permits — see Gate 0.5 leg 3.** The value-shaped
  leg has nothing statically discoverable to read: the AppServices assembly carries **0**
  `[DefaultValue]`, **0** Swagger example attributes, **no** generated XML documentation file
  (`GenerateDocumentationFile` appears in no `.csproj`) and **0** `sk_`/`ek_`/`seti_` literals. A test
  over an empty corpus is unfalsifiable, and the ticket says in terms not to ship one to satisfy an AC.
  Recorded in the guard's doc comment as a limit, not omitted silently. (`pi_` would not have belonged
  regardless — a payment-intent id is a Stripe object id.)
- **AC3 ✅** — **five** exception entries, each verified against its producer rather than assumed from
  the name: `EventKey` (a notification loc-key from `NotificationEventCatalog`), `FiscalProviderKey`
  (**printed on the customer's receipt PDF**, `DefaultReceiptLayoutBuilder.cs:104`), `IdempotencyToken`
  (a replay-dedup nonce hashed into the Stripe attempt id), `Key` (the placeholder *name* in
  `EmailTemplateKeyValueDto(Key, Value)`), `LabelKey` (an i18n key). Well under the ~15 stop-and-tell-
  the-PM bound. **One list, not two** — mirroring the closer sibling `RequestLogPiiSurfaceGuardTests`:
  there is no "accepted pre-existing" list because the one real credential was **fixed, not excepted**.
- **AC4 ✅** — see the mutation table. The honest mutation is #1.
- **AC5 ✅** — `Guard_ActuallySeesTheCredentialBearingDtos`: route count in `[400,1000]`,
  credential-shaped member count in `[20,500]`, and — stronger than either — it demands the walk still
  reach `EphemeralKey`, `SetupIntentClientSecret` and `DeviceToken` by name. Mutation #4 proves it fails
  on a broken walk.
- **AC6 ✅ — the gap was real and no sibling covered it.** `WireSurface.ReadTokens` reads the regex off
  `AllHostMiddleware[0]` (Customer) **only**, so every guard built on it inherits one host's list;
  `RequestLogPayoutPathSuppressionTests` iterates all five but only for `IsSensitivePath`.
  `TheRedactionRegex_IsIdenticalOnAllFiveHosts` now compares pattern **and `RegexOptions`** across the
  five for both `SensitiveFieldRegex` and `ContactIdentityFieldRegex` — options too, because dropping
  `IgnoreCase` on one host is a divergence the pattern text alone would not show. Mutation #3 proves
  the main guard stays **green** on a four-of-five divergence and only this test reddens.
- **AC7 ✅** — run locally, Docker up. `dotnet build Cleansia.Api.sln --no-incremental` exit 0
  (25.0s elapsed — executed, not "up-to-date"). `Cleansia.Tests` **3179/3179** exit 0,
  `Cleansia.IntegrationTests` **144/144** exit 0, `Cleansia.HostTests` **135/135** exit 0.
  No DEFERRED-TO-CI claimed.

**Beyond the ACs:** `DeviceRegistration_PushToken_NeverReachesTheLog` pins the fix as *behaviour* on all
five hosts, with a non-vacuity assertion that the token closes inside the 1000-byte request window. The
static guard proves the **name is listed**; T-0446 AC9 is the proof that those are different facts —
every listed name was redacted 0% of the time while the middleware truncated before it redacted.

## Gate 0.5 — mutation table

| # | Target | Mutation | Result | Restore |
|---|---|---|---|---|
| 1 | the guard (**AC4's honest mutation**) | remove `ephemeralKey` from `SensitiveFieldRegex` ×5 | **RED** — names `EphemeralKey` on `ConfirmRecurringOrder.Response`, `CreateMembershipSubscription.Response`, `CreatePaymentIntent.Response`. The guard would have caught *deliberately* the credential that was found by accident | sha256 ✅ 12/12 |
| 2 | the `deviceToken` fix | remove `deviceToken` ×5 | **RED, 6 tests** — static guard names `RegisterDevice.Command.DeviceToken`; the behavioural theory fails on all five hosts showing `"deviceToken":"fMEQ4t2vSGa1nQ9pXKzYb"` in the log | sha256 ✅ |
| 3 | five-host consistency (AC6) | remove `deviceToken` from **`Cleansia.Web.Admin` only** (a non-reference host) | **RED, 2 tests** — `TheRedactionRegex_IsIdenticalOnAllFiveHosts` names Admin; the main static guard **passes**, which is precisely the hole AC6 closes | sha256 ✅ |
| 4 | anti-vacuity (AC5) | `WireSurface.HostAssemblies()` → `.Take(1)` | **RED** — `Guard_ActuallySeesTheCredentialBearingDtos` `Assert.InRange` failure | sha256 ✅ |

All restores verified byte-exact against a checksum manifest of all 12 touched files; final `shasum -c`
reported 12/12 OK. No `git restore`/`checkout`/`reset` was used on any file.

## Catalog edit — routing (ADR-0033), claimed **inline**

`docs/architecture/security-rules.md` §S6 said in terms *"**Still open, and nothing detects it:** a
credential whose field name was never in the token list (T-0470)."* That sentence is now false and was
replaced; the third guard was added to the "the guard, not the list" enumeration.

1. **Does it put shipped code in violation?** No. **Sweep:** the guard itself is the sweep — it walks
   every wire DTO on 400+ routes across the five hosts; after the `deviceToken` fix it reports **0**
   findings, and the full suite is 3179/3179. Zero baseline by construction.
2. **Does it narrow open latitude?** No — it **widens** an existing rule rather than carving an
   exception out of one. **Terms searched** in `security-rules.md`, `patterns-backend.md` and
   `consistency.md`: `credential`, `SensitiveFieldRegex`, `token list`, `redact`. The governing
   sentence is `security-rules.md:162-167` (*"What makes a denylist admissible at all is the guard, not
   the list … A new PII- or payout-shaped member that is neither redacted, nor on a suppressed route,
   nor excepted in writing fails the build"*). Extending its enumeration to a third shape is a
   clarification **inside that rule's scope** (limb 4), not an exception carved out of it — and it
   obliges no shipped call site. The other candidate, `:169-170`, is retired **by its own terms**: it
   asserts a status that this ticket changed.
3. **Prescriptive about a stack not built and run?** No — backend, all three suites executed locally.

→ limb 4, **inline**. **Enforced by:** `RequestLogCredentialShapeGuardTests` (+ its two siblings) in
`Cleansia.Tests`, a named step of `backend-ci.yml:69-71` — **T1-CI**.

## Review
<!-- reviewer + security verdicts here; AC4 must name the mutation-proving test -->
**AC4's mutation-proving test:** `RequestLogCredentialShapeGuardTests.EveryCredentialShapedWireMember_IsRedactedOrItsRouteIsSuppressed`
(mutation #1, `ephemeralKey` removed ×5: **RED**, naming three DTOs; restored, **GREEN** at 28/28).
