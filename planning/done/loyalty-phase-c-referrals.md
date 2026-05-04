# Loyalty Phase C — Referrals

Wolt-style "invite a friend" loop. Each customer gets an auto-generated referral code; sharing the code grants both inviter + invitee +150 tier-points after the invitee's first completed order.

## Decisions (locked)

| Item | Value |
|---|---|
| Generation | Auto-issued at signup; one code per user, lifetime |
| Code format | 6-char alphanumeric uppercase (`X3K9P2`) — short, shareable, type-safe (no I/0/O) |
| Reward — inviter | +150 tier-points on invitee's first completed order |
| Reward — invitee | +150 tier-points on their own first completed order |
| Trigger | Invitee's FIRST completed order (not signup, not first booking) |
| Expiry | Code never expires; the referral relationship has a 90-day "qualifying window" from acceptance |
| Stacking with promo codes | Referrals don't discount orders — they grant tier-points. So they DON'T conflict with promo codes (those discount). Customer can use a referral AND a promo on their first order. |
| Self-referral | Forbidden — invitee userId can't equal inviter userId |
| Re-referral | A user who's already in the system as a referrer can't be referred by someone else later (anti-abuse) |
| Acceptance flow | Code can be entered at signup (preferred) OR at first booking (fallback) |

## Domain model

### `ReferralCode` — one per user, lifetime

```csharp
class ReferralCode : Auditable, ITenantEntity
{
    string UserId { get; private set; }      // unique
    User User { get; private set; }
    string Code { get; private set; }        // unique per tenant, 6 char uppercase
    int TimesUsed { get; private set; }      // denormalized — count of qualified referrals
    bool IsActive { get; private set; } = true;

    public static ReferralCode Generate(string userId, Func<string> codeGenerator) { ... }
    public void RecordUse() => TimesUsed++;
}
```

Generation: cryptographic random with retry on collision. Alphabet excludes I/1, O/0, no vowels (avoids accidental words). Codespace at 6 chars from alphabet of 26 = 308M codes — tons of headroom.

### `Referral` — one per (inviter, invitee) pair

```csharp
class Referral : Auditable, ITenantEntity
{
    string ReferrerUserId { get; private set; }
    User Referrer { get; private set; }
    string ReferredUserId { get; private set; }    // populated when invitee signs up
    User Referred { get; private set; }
    string ReferralCodeId { get; private set; }    // FK to inviter's code (snapshot — code never changes but FK is cleaner)
    ReferralStatus Status { get; private set; }
    DateTimeOffset AcceptedOn { get; private set; }    // when invitee redeemed the code
    DateTimeOffset? FirstQualifyingOrderOn { get; private set; }  // when invitee's first order completed
    string? FirstQualifyingOrderId { get; private set; }
    int? PointsAwardedToReferrer { get; private set; }
    int? PointsAwardedToReferred { get; private set; }
    DateTimeOffset? PointsAwardedOn { get; private set; }
}

[SwaggerEnumAsInt]
public enum ReferralStatus {
    Accepted = 1,    // invitee redeemed code, no qualifying order yet
    Qualified = 2,   // invitee completed first order, points granted
    Expired = 3,     // 90-day window passed without qualifying order
}
```

Index on `ReferrerUserId`, `ReferredUserId` (unique — one inviter per invitee), `Status`, `AcceptedOn`.

## Service layer

```csharp
public interface IReferralService
{
    /// <summary>Get-or-create the user's lifetime referral code. Idempotent.</summary>
    Task<ReferralCode> EnsureCodeForUserAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Validate a referral code at acceptance time. Returns the inviter's user id
    /// or an error code (NotFound / SelfReferral / AlreadyReferred).
    /// </summary>
    Task<ReferralValidateResult> ValidateAsync(string code, string acceptingUserId, CancellationToken ct);

    /// <summary>
    /// Record acceptance: invitee just signed up (or entered at first booking)
    /// with a valid code. Creates the Referral row in Accepted state.
    /// </summary>
    Task<ReferralAcceptResult> AcceptAsync(string code, string acceptingUserId, CancellationToken ct);

    /// <summary>
    /// Called from CompleteOrder.Handler. Checks if this is the invitee's first
    /// completed order; if so, grants points to both sides via LoyaltyService and
    /// flips Referral.Status to Qualified.
    /// </summary>
    Task ProcessOrderCompletedAsync(string orderId, string userId, CancellationToken ct);

    /// <summary>Background job: expire Referrals past the 90-day window.</summary>
    Task ExpireStaleReferralsAsync(CancellationToken ct);
}

public record ReferralValidateResult(bool IsValid, string? ReferrerUserId, ReferralValidationError? Error);
public enum ReferralValidationError { NotFound, SelfReferral, AlreadyReferred, Inactive }
```

`ProcessOrderCompletedAsync` semantics:
- Look up Referral where `ReferredUserId == userId && Status == Accepted`
- If found:
  - Check whether this is the FIRST `OrderStatus.Completed` for this user (count completed orders for the user, including this one == 1)
  - If yes: grant +150 to referrer's LoyaltyAccount AND +150 to referred's LoyaltyAccount, flip Referral to Qualified, set FirstQualifyingOrder*

This is **idempotent** — if `ProcessOrderCompletedAsync` runs twice for the same orderId, the second pass sees Status = Qualified and no-ops.

Constant: `IReferralPolicy.PointsPerSide = 150`, `WindowDays = 90`. Lives in a new `ReferralPolicy.cs` next to `BookingPolicy.cs`.

## Integration into existing handlers

### `CompleteOrder.Handler`
Add a single line after the existing loyalty grant call:

```csharp
await loyaltyService.GrantForCompletedOrderAsync(order.Id, ct);
await referralService.ProcessOrderCompletedAsync(order.Id, order.UserId, ct);  // NEW
```

The order matters slightly — loyalty grant runs first so the new account exists before referral grants pile on. (Actually `ProcessOrderCompletedAsync` calls `LoyaltyService.GrantPoints` internally which is idempotent on account creation, so order doesn't strictly matter — but read order makes intent clear.)

### Auth signup handler
Existing customer registration handler (find via grep `RegisterCustomerCommand` or similar) accepts a new optional `ReferralCode` field. After successful user creation:

```csharp
if (!string.IsNullOrEmpty(command.ReferralCode))
{
    await referralService.AcceptAsync(command.ReferralCode, newUserId, ct);
}
```

Acceptance failures (invalid code, etc.) **don't fail the registration** — log a warning and proceed. User can enter the code later at first booking.

### `CreateOrder.Handler`
Optional referral code path: if `command.ReferralCode` is supplied AND the user has no existing Referral row, treat as late-acceptance and call `referralService.AcceptAsync(...)`. This covers the "user forgot to enter at signup" case.

## Customer API endpoints

New `ReferralController`:

```csharp
public class ReferralController(IMediator mediator) : CustomerApiController(mediator)
{
    /// <summary>Get my referral code + stats (times used, qualified count).</summary>
    [HttpGet("GetMy")]
    [Permission(Policy.CanViewMyReferral)]

    /// <summary>List my referrals (who I invited + status).</summary>
    [HttpGet("GetMyReferrals")]
    [Permission(Policy.CanViewMyReferral)]

    /// <summary>Validate a code before submitting it. Returns inviter info if valid.</summary>
    [HttpPost("Validate")]
    [Permission(Policy.CanRedeemReferralCode)]
}
```

Three new permissions in PolicyBuilder (`CanViewMyReferral`, `CanRedeemReferralCode`, plus admin-side `CanViewAllReferrals` for L4).

## Mobile UI

### New section on the Rewards tab
Add an "Invite friends" card between the existing "Tier ladder" and "Recent activity" sections (Phase A's M2). Layout:

- Section title: "Invite friends — earn 150 points each"
- Code badge: large monospaced display ("X3K9P2") with copy + share icons
- Stats row: "X friends joined · Y waiting on first booking"
- "Share" primary button → triggers Android share intent with deep link

### Share intent
URL format: `https://cleansia.cz/r/X3K9P2` — public landing page that:
- Anonymous: shows the inviter's name + "Sign up to claim your bonus" CTA → registration form pre-fills the code field
- Authenticated: shows "You're already a customer — codes are for new sign-ups"

The deep link parsing on mobile is out of scope for L3 phase 1 (that's a deeper "deep linking + universal links" feature). For now the share text is a copy-paste link and a fallback message:

> "Get 150 bonus points on your first Cleansia cleaning! Use my code X3K9P2 at signup: https://cleansia.cz/r/X3K9P2"

### Signup screen — "I have a referral code" field
Optional collapsed input under the password fields. Tap → expands. Validates on blur via the new endpoint. Bad code shows inline error but doesn't block submission.

### `BookingViewModel` extension
On the wizard's first step (or a dedicated "First booking?" check during ConfirmStep), if the user has no `Referral` record yet, show a small "Have a referral code?" link. Same input, same validation, same fallback (bad code → snackbar, doesn't block submit).

### Mobile data layer
- `core/referral/ReferralApi.kt` — 3 endpoints
- `core/referral/ReferralDtos.kt` — Code response, Referral list item, Validate request/response
- `core/referral/ReferralRepository.kt` — `@Singleton`, exposes `code: StateFlow<ReferralCodeDto?>`, `referrals: StateFlow<List<ReferralListItemDto>>`. Lazy-fetched when Rewards tab opens.
- `core/referral/ReferralModule.kt`
- `core/referral/ReferralRepositoryEntryPoint.kt`
- Sign-out plumbing in 4 places (mirror Phase A pattern)

## Web UI parity
- Rewards page gets the same "Invite friends" card.
- Signup form gets the optional referral input.
- Booking wizard's first step gets the optional referral input.
- Mobile-equivalent share intent → web's `navigator.share()` with same URL format, fallback to copy-to-clipboard.

## Background job

`ExpireStaleReferralsAsync` runs daily (use existing background job infra — find via grep for `IHostedService` or scheduled jobs). Updates Referral rows where `Status = Accepted AND AcceptedOn + 90 days < now()` to `Status = Expired`. Doesn't grant any points — just stops the qualifying check.

If no scheduled job framework exists today, defer this to a manual cleanup endpoint or skip entirely (Referrals just stay in Accepted forever; no functional harm, just data hygiene).

## Migration

Two new tables:
- `ReferralCodes` (one per user, unique on `UserId`)
- `Referrals` (one per pair, unique on `(ReferrerUserId, ReferredUserId)` AND on `ReferredUserId` since each user can only be referred once)

No order column changes — referrals don't discount orders.

## i18n keys (mobile EN + CS, web all 5)

**Mobile** (under `loyalty_referral_*`):
- `loyalty_referral_section_title` — "Invite friends — earn 150 points each"
- `loyalty_referral_code_label` — "Your code"
- `loyalty_referral_share_button` — "Share"
- `loyalty_referral_copy_button` — "Copy"
- `loyalty_referral_copied_toast` — "Code copied"
- `loyalty_referral_share_text` — "Get 150 bonus points on your first Cleansia cleaning! Use my code %1$s at signup: %2$s"
- `loyalty_referral_stats_joined` — "%1$d friend joined"
- `loyalty_referral_stats_qualified` — "%1$d qualified"
- `loyalty_referral_stats_waiting` — "%1$d waiting"
- `loyalty_referral_stats_empty` — "No referrals yet"
- `referral_code_field_label` — "Referral code (optional)"
- `referral_code_field_collapsed` — "I have a referral code"
- `referral_code_valid` — "Code from %1$s — you'll get 150 bonus points"
- `error_referral_not_found` — "Referral code not found"
- `error_referral_self_referral` — "You can't refer yourself"
- `error_referral_already_referred` — "You've already accepted a referral"
- `error_referral_inactive` — "This referral code is no longer active"

**Web** (under `pages.rewards.referral.*`).

## Out of scope for L3
- Deep linking / universal link routing (separate feature)
- Public landing page at `/r/{code}` — Phase 2 polish
- Referral analytics for admin (Phase L4)
- Multi-tier referral programs ("invite 5 friends, get bonus") — overcomplication
- Email/SMS-based invites — only share-intent + copy

## Phasing within L3
1. **L3-B1 — Backend foundation**: entities + service + 3 endpoints + integration into CompleteOrder + Register handlers + migration. ~half day.
2. **MANUAL_STEP**: migration + NSwag regen.
3. **L3-M1 — Mobile data layer + Rewards tab card + signup field + booking field**. ~half day.
4. **L3-W1 — Web parity**. ~half day.

M1 + W1 in parallel after B1.
