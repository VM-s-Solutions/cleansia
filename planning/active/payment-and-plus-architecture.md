# Payment system + Cleansia Plus — architecture plan

**Status:** Ready for execution
**Created:** 2026-05-01
**Decision:** Build Stripe-backed payments on mobile NOW, with the data model and pricing pipeline pre-shaped for a Cleansia Plus subscription that can launch later without refactoring. Subscription product itself ships when product is ready; this plan only commits to the *foundation*.

---

## Goals

1. **Mobile payments parity with web.** Customer Android app can pay by card (currently cash-only on mobile). Match the web's Stripe Checkout flow, but use the native Stripe `PaymentSheet` SDK for a better UX.
2. **App Store / Play Store compliant.** Stripe is the correct payment processor for physical cleaning services. No Apple IAP / Google Play Billing required.
3. **Pre-shape the architecture for Cleansia Plus** so adding a subscription later is *additive*, not a refactor:
   - Each `User` has a persistent `StripeCustomerId` (not just per-order anonymous sessions).
   - Pricing pipeline accepts a "membership discount" alongside loyalty tier + promo code, with best-wins precedence extended to 3 sources.
   - Cancellation policy is membership-aware (`getCancellationPolicy(user)` returns different windows based on Plus status — Plus members get longer free-cancel windows).
   - Same-cleaner request infrastructure (`preferredEmployeeId` on order + scoring boost in matching algorithm) — Plus's headline "request your favorite cleaner" perk.
   - Recurring booking infrastructure (`RecurringBookingTemplate` entity + materializer job) — Plus's "every other Tuesday" perk.
4. **Don't ship Plus features yet.** Backend tables, fields, and policies exist; UI and subscription product gating come later when product is ready. The architecture supports turning Plus on with mostly UI work + a Stripe Product/Price registration.

## Non-goals (explicit cuts)

- ❌ Apple IAP / Google Play Billing integration. Cleansia is a physical service marketplace.
- ❌ Cleansia Plus subscription PRODUCT launch. This plan ships the foundations; Plus rollout is a separate spec when product is ready.
- ❌ Tipping flow. Separate, smaller spec.
- ❌ Refund policy redesign. Existing refund handling stays as-is.
- ❌ Cleaner-side payouts via Stripe Connect. Cleaners are paid via existing invoicing pipeline; no change.
- ❌ iOS Stripe integration. Android only for now (matches mobile scope).
- ❌ Admin UI for managing subscriptions. Stripe Dashboard is the management surface for the few subscribers we'll have early. Admin UI is a future spec.

---

## Decisions in scope for this spec

1. **Stripe is the only payment processor.** Both card payments and (future) subscriptions go through Stripe. No second processor.
2. **`StripeCustomerId` lives on `User`, populated lazily on first card payment.** Cash-paying users never get a Stripe customer. Card-paying users get one created on their first card booking and reused forever after.
3. **Mobile uses Stripe `PaymentSheet`, web stays on Checkout Session.** Different payment surfaces are fine; both backed by the same Stripe customer + payment intent infrastructure. This avoids a web rewrite.
4. **PaymentIntent flow for mobile, Checkout Session flow for web.** PaymentIntent gives mobile fine-grained control for PaymentSheet; Checkout Session is simpler for web's redirect-based flow. Both create the same `Payment` records server-side.
5. **Three discount sources — best-wins among membership/tier/promo.** Today the pricing pipeline picks max(tier, promo). Extend to max(tier, promo, membership). Stacking is still forbidden — pick one.
6. **Membership discount is a flat percentage stored on the membership plan.** Initially this is just data sitting in DB; no plan exists. When Plus launches, we add a row to `MembershipPlan` saying "5% off, stacks-via-best-wins with tier and promo".
7. **Cancellation policy returns a per-user window.** A `CancellationPolicyResolver` service takes a `User` + `Order` and returns the policy. Today everyone gets the same window; tomorrow Plus members get a longer free-cancel window via the same resolver.
8. **`PreferredEmployeeId` on `Order`** as a request, not a guarantee. Cleansia matching algorithm gets a tuning hook to boost the preferred cleaner's score; if they decline / are busy, the order falls back to normal matching with no error to the customer.
9. **Recurring bookings use a "template + materializer" pattern.** A `RecurringBookingTemplate` entity defines the schedule + booking parameters. A daily Azure Function reads templates and creates concrete `Order` records 7 days ahead. Cancellation of one occurrence does not affect future ones.
10. **No backwards-compat hacks.** App is pre-launch. Migrations can be additive but don't need data backfill — there's no production data to migrate.

---

## Phase 1 — Backend data model + pricing pipeline

### TASK-PA1: Add `StripeCustomerId` to User

```yaml
task: User entity gains StripeCustomerId field; backend creates Stripe customer lazily on first card payment
id: TASK-PA1
type: feature
priority: high
specialist: backend
estimated_complexity: small

context: |
  Today's flow creates an anonymous Stripe Checkout Session per order.
  That works for one-off payments but blocks subscriptions and saved cards.
  Stripe subscriptions REQUIRE a Customer object; saved-card flows in
  PaymentSheet also work better with one. Add StripeCustomerId to User.

  Lazy creation: don't create on signup. Create on first card payment.
  Cash-paying users never accumulate Stripe customers we don't need.

files_to_modify:
  - path: src/Cleansia.Core.Domain/Users/User.cs
    change: |
      Add field:
        [MaxLength(64)]
        public string? StripeCustomerId { get; private set; }

      Add method:
        public User AssignStripeCustomerId(string id) {
            StripeCustomerId = id;
            return this;
        }

      Add to Anonymize() so GDPR deletion clears it:
        StripeCustomerId = null;

  - path: src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs
    change: |
      Add:
        Task<string> CreateOrGetCustomerAsync(
            string email, string fullName, string? phone, CancellationToken ct);
        // Returns Stripe customer id. Idempotent: call with same email +
        // existing user → if our User has StripeCustomerId, the caller
        // should reuse it instead of calling this. This method is for
        // first-time creation only.

  - path: src/Cleansia.Infra.Clients/Stripe/StripeClient.cs
    change: |
      Implement CreateOrGetCustomerAsync using Stripe SDK's CustomerService:
        var options = new CustomerCreateOptions {
            Email = email, Name = fullName, Phone = phone,
            Metadata = new() { ["source"] = "cleansia" }
        };
        var customer = await _customerService.CreateAsync(options, cancellationToken: ct);
        return customer.Id;

      Note: we don't need Stripe-side dedup here — our User table is the
      source of truth. If StripeCustomerId is null on User, create.
      Otherwise reuse.

files_to_create: []

dependencies: []

verification:
  - dotnet build Cleansia.Api.sln
  - Existing Stripe-using tests pass (CreateOrder card flow)
  - MANUAL_STEP: migration for StripeCustomerId field
```

### TASK-PA2: MembershipPlan + UserMembership entities (data only, no UI)

```yaml
task: Add MembershipPlan + UserMembership domain entities; ship empty (no rows) but ready for Plus
id: TASK-PA2
type: feature
priority: high
specialist: backend
estimated_complexity: medium

context: |
  Plus needs:
   - A catalog of membership plans (initially: just "Plus" with 5% discount)
   - Per-user enrollment records (with Stripe subscription id, status, period)
  Build the model now. No rows exist initially. When Plus launches we INSERT
  one row in MembershipPlan and start populating UserMembership.

files_to_create:
  - path: src/Cleansia.Core.Domain/Memberships/MembershipPlan.cs
    change: |
      public class MembershipPlan : Auditable, ITenantEntity {
          [Required] [MaxLength(50)] public string Code { get; private set; }
              // e.g. "PLUS_MONTHLY" — stable identifier used in code

          [Required] [MaxLength(100)] public string Name { get; private set; }
              // e.g. "Cleansia Plus"

          public decimal MonthlyPriceCzk { get; private set; }
              // Display price; canonical price lives in Stripe. We mirror
              // here for billing-period preview without a Stripe round-trip.

          [Required] [MaxLength(64)] public string StripePriceId { get; private set; }
              // The Stripe Price (not Product) id. One per plan.

          public decimal DiscountPercentage { get; private set; }
              // e.g. 5.0 = 5% off every cleaning

          public int FreeCancellationWindowHours { get; private set; }
              // e.g. 4 = free cancellation up to 4h before
              // Non-members today have 12h paid / 24h free per BookingPolicy

          public bool AllowsExpressUpgrade { get; private set; }
              // "1 free express upgrade per month" — tracked separately
              // via UserMembershipBenefitUsage (out of scope this spec)

          public bool IsActive { get; private set; } = true;

          public static MembershipPlan Create(...) { ... }
      }

  - path: src/Cleansia.Core.Domain/Memberships/UserMembership.cs
    change: |
      public class UserMembership : Auditable, ITenantEntity {
          [Required] public string UserId { get; private set; }
          public User User { get; private set; }

          [Required] public string MembershipPlanId { get; private set; }
          public MembershipPlan MembershipPlan { get; private set; }

          [Required] [MaxLength(64)] public string StripeSubscriptionId { get; private set; }

          public MembershipStatus Status { get; private set; }
              // Active, PastDue, Cancelled, Paused

          public DateTime CurrentPeriodStart { get; private set; }
          public DateTime CurrentPeriodEnd { get; private set; }
              // Mirrors Stripe's subscription period; used for "free
              // cancellation" / "free express upgrade" usage tracking.

          public DateTime? CancelledAt { get; private set; }
              // Set when user requests cancellation. Subscription continues
              // through CurrentPeriodEnd then transitions to Cancelled.

          public bool IsActive => Status == MembershipStatus.Active
              && DateTime.UtcNow < CurrentPeriodEnd;

          public static UserMembership Create(...) { ... }
          public UserMembership UpdateFromStripeWebhook(...) { ... }
          public UserMembership MarkCancelledAtPeriodEnd() { ... }
      }

  - path: src/Cleansia.Core.Domain/Memberships/MembershipStatus.cs
    change: |
      public enum MembershipStatus {
          Active = 1,
          PastDue = 2,
          Cancelled = 3,
          Paused = 4,
      }

  - path: src/Cleansia.Infra.Database/EntityConfigurations/MembershipPlanEntityConfiguration.cs
    change: |
      Standard EF config: indexes on Code (unique), IsActive.

  - path: src/Cleansia.Infra.Database/EntityConfigurations/UserMembershipEntityConfiguration.cs
    change: |
      Indexes on (UserId, Status), StripeSubscriptionId (unique).
      One active membership per user is a SOFT constraint enforced in
      handler code (not a unique index, since cancelled+new is allowed).

files_to_modify:
  - path: src/Cleansia.Infra.Database/CleansiaDbContext.cs
    change: |
      Add DbSet<MembershipPlan> + DbSet<UserMembership>.

  - path: src/Cleansia.Core.Domain/Users/User.cs
    change: |
      Add navigation:
        private ICollection<UserMembership> _memberships = [];
        public virtual IReadOnlyCollection<UserMembership> Memberships =>
            _memberships.ToList().AsReadOnly();

      And convenience method:
        public UserMembership? ActiveMembership =>
            _memberships.FirstOrDefault(m => m.IsActive);

dependencies: [TASK-PA1]

verification:
  - dotnet build Cleansia.Api.sln
  - MANUAL_STEP: migration creates MembershipPlans + UserMemberships tables
  - Inserting a test row in MembershipPlan via SQL → readable via EF
```

### TASK-PA3: PreferredEmployeeId on Order + matching boost

```yaml
task: Order can carry a customer-requested cleaner; matching algorithm boosts that cleaner's score
id: TASK-PA3
type: feature
priority: high
specialist: backend
estimated_complexity: medium

context: |
  Plus's headline operational perk: "request your favorite cleaner".
  Foundation: orders carry a PreferredEmployeeId (nullable, anyone can
  set it — it's a request, not a Plus-only feature at the data layer;
  we gate the UI to Plus members later).

  Matching algorithm: when computing cleaner scores, add a +X boost
  for cleaners whose Id matches PreferredEmployeeId. They still need
  to accept the order; if they decline / are busy, the order falls
  back to normal matching. The customer is not told "your preferred
  cleaner declined" — it's silent fallback.

files_to_modify:
  - path: src/Cleansia.Core.Domain/Orders/Order.cs
    change: |
      Add field:
        [MaxLength(36)]
        public string? PreferredEmployeeId { get; private set; }
            // Customer's request for a specific cleaner. Used as a
            // matching hint. Honored if the cleaner is available; falls
            // back to normal matching if not. Not exposed to the cleaner
            // (avoids "they didn't pick me" awkwardness).

      Add to Create() factory accepting nullable preferredEmployeeId.

  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    change: |
      Command record: add `string? PreferredEmployeeId = null` field.
      Validator: When(x => x.PreferredEmployeeId != null) → must exist
      AND must be an Employee the user has had a completed order with
      previously. (Prevents random employee-id guessing as a recon vector.)
      Handler: pass through to Order.Create.

  - path: src/Cleansia.Core.AppServices/Services/IOrderMatchingService.cs
    # If matching service exists; otherwise this hook lives in whatever
    # scoring code partner-side picks orders by.
    change: |
      Add a parameter to scoring or a multiplier:
        if (order.PreferredEmployeeId == cleaner.Id) score *= 1.5;
      Document the multiplier choice in a comment.

files_to_create: []

dependencies: []

verification:
  - dotnet build Cleansia.Api.sln
  - Unit test: CreateOrder with valid PreferredEmployeeId → persisted
  - Unit test: CreateOrder with PreferredEmployeeId of an employee the
    user has never been served by → ValidationException
  - MANUAL_STEP: migration adds PreferredEmployeeId column
```

### TASK-PA4: Pricing pipeline accepts membership discount

```yaml
task: Extend best-wins discount logic in CreateOrder.cs to evaluate membership alongside tier + promo
id: TASK-PA4
type: refactor
priority: high
specialist: backend
estimated_complexity: small

context: |
  Today CreateOrder.cs handler picks max(tier, promo). Make it
  max(tier, promo, membership). When no UserMembership.IsActive exists
  for the user, membership discount is 0 → behavior identical to today.
  When Plus launches, the same code path applies the membership discount
  if it's the largest of the three.

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs
    change: |
      Around the existing tier vs promo best-wins block (lines ~344-365):

        decimal membershipDiscount = 0m;
        string? membershipPlanId = null;
        var activeMembership = await userRepository
            .GetActiveMembershipAsync(command.UserId, cancellationToken);
        if (activeMembership != null) {
            membershipDiscount = finalTotalPrice
                * (activeMembership.MembershipPlan.DiscountPercentage / 100m);
            membershipPlanId = activeMembership.MembershipPlanId;
        }

        // Best-wins: pick whichever is largest. Stacking still forbidden.
        var best = new[] {
            (Source: "tier", Amount: tierDiscount, Id: (string?)null),
            (Source: "promo", Amount: promoDiscount, Id: promoCodeId),
            (Source: "membership", Amount: membershipDiscount, Id: membershipPlanId),
        }.OrderByDescending(x => x.Amount).First();

        if (best.Amount > 0m) {
            finalTotalPrice -= best.Amount;
            switch (best.Source) {
                case "tier":
                    appliedTierDiscount = best.Amount;
                    break;
                case "promo":
                    appliedPromoDiscount = best.Amount;
                    appliedPromoCodeId = best.Id;
                    break;
                case "membership":
                    appliedMembershipDiscount = best.Amount;
                    appliedMembershipPlanId = best.Id;
                    break;
            }
        }

  - path: src/Cleansia.Core.Domain/Orders/Order.cs
    change: |
      Add fields (mirror existing TierDiscountAmount + PromoDiscountAmount):
        public decimal? MembershipDiscountAmount { get; private set; }
        [MaxLength(36)] public string? MembershipPlanIdAtPurchase { get; private set; }
            // Snapshot for receipts/refunds — like TierAtPurchase.

  - path: src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs
    change: |
      Apply the same membership discount in the quote handler so the
      live quote reflects what the user will actually pay. Without this,
      a Plus member would see the non-discounted total in the wizard
      and a different (lower) total at submit.

files_to_create: []

dependencies: [TASK-PA2]

verification:
  - dotnet build
  - Unit test: order create with no UserMembership → behavior unchanged
  - Unit test: order create with active membership (5% discount) and
    no tier/promo → membership discount applies
  - Unit test: order create with membership 5% AND tier 10% → tier wins
    (best-wins precedence)
  - MANUAL_STEP: migration adds Order.MembershipDiscountAmount +
    MembershipPlanIdAtPurchase columns
```

### TASK-PA5: CancellationPolicyResolver service

```yaml
task: Cancellation windows + fees become a function of (user, order) instead of constants
id: TASK-PA5
type: refactor
priority: high
specialist: backend
estimated_complexity: small

context: |
  Today cancellation policy is constants in BookingPolicy.cs (free up
  to 24h, partial fee 12-24h, full fee under 12h). Plus members get a
  longer free-cancel window. Refactor to a service that returns the
  policy for a given (user, order) so the rest of the codebase doesn't
  need to know about Plus.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Services/ICancellationPolicyResolver.cs
    change: |
      public interface ICancellationPolicyResolver {
          CancellationPolicy ResolveFor(User user, Order order);
      }

      public record CancellationPolicy(
          int FreeCancellationHours,        // free if cancelled this many hours+ before
          int PartialFeeHours,              // partial fee window
          decimal PartialFeePercentage);    // % of order charged

  - path: src/Cleansia.Core.AppServices/Services/CancellationPolicyResolver.cs
    change: |
      public class CancellationPolicyResolver : ICancellationPolicyResolver {
          public CancellationPolicy ResolveFor(User user, Order order) {
              var membership = user.ActiveMembership;
              if (membership != null && membership.MembershipPlan.FreeCancellationWindowHours > 0) {
                  return new CancellationPolicy(
                      FreeCancellationHours: membership.MembershipPlan.FreeCancellationWindowHours,
                      PartialFeeHours: BookingPolicy.PartialFeeHours,
                      PartialFeePercentage: BookingPolicy.PartialFeePercentage);
              }
              return new CancellationPolicy(
                  FreeCancellationHours: BookingPolicy.FreeCancellationHours,
                  PartialFeeHours: BookingPolicy.PartialFeeHours,
                  PartialFeePercentage: BookingPolicy.PartialFeePercentage);
          }
      }

files_to_modify:
  - path: src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs
    change: |
      Replace direct BookingPolicy constant reads with:
        var policy = _cancellationPolicyResolver.ResolveFor(user, order);
        // ... use policy.FreeCancellationHours etc.

      Inject ICancellationPolicyResolver into the handler.

  - path: src/Cleansia.Config/StartupBase.cs (or wherever services register)
    change: |
      services.AddScoped<ICancellationPolicyResolver, CancellationPolicyResolver>();

dependencies: [TASK-PA2]

verification:
  - Unit test: non-Plus user → returns BookingPolicy defaults
  - Unit test: Plus user with FreeCancellationWindowHours=4 → returns 4
  - Existing CancelOrder tests still pass
```

---

## Phase 2 — Stripe payments on mobile (the actually-shipping part)

### TASK-PA6: Backend `CreatePaymentIntent` endpoint

```yaml
task: Backend handler that creates a Stripe PaymentIntent for an order and returns clientSecret
id: TASK-PA6
type: feature
priority: high
specialist: backend
estimated_complexity: medium

context: |
  Mobile uses PaymentSheet, which needs a PaymentIntent client_secret.
  Web uses Checkout Session (existing). New endpoint:

    POST /api/Order/CreatePaymentIntent
    Body: { orderId: string }
    Response: { clientSecret: string, paymentIntentId: string,
                stripeCustomerId: string, ephemeralKey: string }

  EphemeralKey lets PaymentSheet show saved cards. Required when the
  payment is attached to a Customer.

  Server flow:
    1. Load order, verify caller owns it, verify status == New/Pending,
       verify PaymentType == Card.
    2. If user.StripeCustomerId is null, create a Stripe customer and
       persist. Reuse otherwise.
    3. Create an ephemeral key for that customer (Stripe API).
    4. Create PaymentIntent:
         amount = order.TotalPrice (after all discounts already applied
                  during CreateOrder; we re-read from DB)
         currency = order.Currency.Code
         customer = stripeCustomerId
         setup_future_usage = "off_session"  # save card by default
         automatic_payment_methods = { enabled = true }  # SCA/3DS
         metadata = { orderId, displayOrderNumber }
    5. Persist PaymentIntent.id on Order (replace StripeSessionId usage
       for card flow — keep StripeSessionId nullable for now to support
       both flows).
    6. Return clientSecret + ephemeralKey + customerId to client.

  Webhook handling: existing Stripe webhook endpoint must handle
  payment_intent.succeeded → mark order Paid, payment_intent.payment_failed
  → keep order in Pending and let mobile retry.

files_to_create:
  - path: src/Cleansia.Core.AppServices/Features/Orders/CreatePaymentIntent.cs
    change: |
      Standard CQRS handler. Command(orderId), Validator (order exists,
      caller is owner, status is New/Pending, payment type is Card),
      Handler creates the intent + ephemeral key + returns Response with
      clientSecret. Use existing IStripeClient — extend it.

files_to_modify:
  - path: src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs
    change: |
      Add:
        Task<PaymentIntentResult> CreatePaymentIntentAsync(
            decimal amount, string currency, string stripeCustomerId,
            string orderId, string displayOrderNumber, CancellationToken ct);

        Task<string> CreateEphemeralKeyAsync(
            string stripeCustomerId, CancellationToken ct);

      public record PaymentIntentResult(string Id, string ClientSecret);

  - path: src/Cleansia.Infra.Clients/Stripe/StripeClient.cs
    change: |
      Implement both methods using Stripe.PaymentIntentService and
      Stripe.EphemeralKeyService. EphemeralKey API version must match
      what the mobile SDK expects — use Stripe SDK's recommended version.

  - path: src/Cleansia.Web.Customer/Controllers/OrderController.cs
    change: |
      Add:
        [HttpPost("CreatePaymentIntent")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentIntent(
            [FromBody] CreatePaymentIntent.Command command,
            CancellationToken ct)
            => HandleResult(await Mediator.Send(command, ct));

  - path: src/Cleansia.Functions/Functions/StripeWebhook.cs
    # Or wherever Stripe webhooks are processed
    change: |
      Add handlers for:
        - payment_intent.succeeded → Order.MarkPaid + record charge id
        - payment_intent.payment_failed → log, keep order Pending
        - payment_intent.canceled → log, mark order Cancelled if still Pending

      Existing checkout.session.completed handling stays for web flow.

dependencies: [TASK-PA1, TASK-PA4]

verification:
  - Integration test against Stripe test mode:
    - Create order via API
    - POST /CreatePaymentIntent → 200 with clientSecret
    - Confirm intent via Stripe test card 4242...
    - Webhook fires → order moves to Paid
  - Test card 4000 0027 6000 3184 (3DS required) → SCA challenge fires
  - Test card 4000 0000 0000 9995 (insufficient funds) → failure path
```

### TASK-PA7: Mobile Stripe SDK + PaymentSheet integration

```yaml
task: Add stripe-android, wire PaymentSheet into BookingViewModel.submit()
id: TASK-PA7
type: feature
priority: high
specialist: mobile
estimated_complexity: medium

context: |
  When the user picks Card payment and submits, we need to:
    1. Submit the order with PaymentType=Card → backend creates the
       order in Pending status, returns orderId + confirmationCode.
    2. Call POST /CreatePaymentIntent with that orderId → get clientSecret
       + ephemeralKey + customerId.
    3. Open Stripe PaymentSheet with those.
    4. User confirms → Stripe SDK handles 3DS, saved cards, Google Pay.
    5. On success → poll order status (or wait for webhook) → navigate
       to BookingSuccessScreen.
    6. On failure → snackbar + keep user on the booking screen for retry.

  Mobile already submits successfully for cash flow. Card path adds
  the PaymentSheet step between submit and navigate.

files_to_modify:
  - path: src/cleansia_customer_android/app/build.gradle.kts
    change: |
      dependencies {
        implementation("com.stripe:stripe-android:21.0.0")  // or current LTS
      }

  - path: src/cleansia_customer_android/gradle/libs.versions.toml
    change: |
      Add stripe = "21.0.0" entry, then:
        stripe-android = { module = "com.stripe:stripe-android", version.ref = "stripe" }

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/CleansiaApp.kt
    change: |
      onCreate() {
        super.onCreate()
        // ...existing init
        PaymentConfiguration.init(this, BuildConfig.STRIPE_PUBLISHABLE_KEY)
      }

      Add buildConfigField for STRIPE_PUBLISHABLE_KEY in build.gradle.kts
      (read from gradle.properties / env, NOT hardcoded).

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/orders/OrderApi.kt
    change: |
      Add:
        @POST("api/Order/CreatePaymentIntent")
        suspend fun createPaymentIntent(
            @Body body: CreatePaymentIntentRequest
        ): Response<PaymentIntentResponse>

      data class PaymentIntentResponse(
          val clientSecret: String,
          val paymentIntentId: String,
          val stripeCustomerId: String,
          val ephemeralKey: String,
      )

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    change: |
      Refactor submit() to return a richer outcome for the card path:

        sealed interface BookingSubmitOutcome {
            data class CashSuccess(val response: CreateOrderResponse) : BookingSubmitOutcome
            data class CardPending(
                val response: CreateOrderResponse,
                val paymentSheet: PaymentSheetParams,
            ) : BookingSubmitOutcome
            data object ProfileIncomplete : BookingSubmitOutcome
            data object Failed : BookingSubmitOutcome
        }

        data class PaymentSheetParams(
            val clientSecret: String,
            val ephemeralKey: String,
            val customerId: String,
        )

      In submit():
        val createResp = orderRepository.create(...)
        if (createResp == null) return Failed
        if (paymentMethod == "card") {
            val intent = orderRepository.createPaymentIntent(createResp.id)
            if (intent == null) return Failed
            return CardPending(createResp, PaymentSheetParams(...))
        }
        return CashSuccess(createResp)

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt
    change: |
      In the swipe-to-confirm onConfirmed callback, handle the new outcomes:

        is BookingSubmitOutcome.CashSuccess -> {
            bookingVm.reset()
            onComplete(outcome.response.confirmationCode, outcome.response.id)
        }
        is BookingSubmitOutcome.CardPending -> {
            // Launch PaymentSheet
            paymentSheet.presentWithPaymentIntent(
                paymentIntentClientSecret = outcome.paymentSheet.clientSecret,
                configuration = PaymentSheet.Configuration(
                    merchantDisplayName = "Cleansia",
                    customer = PaymentSheet.CustomerConfiguration(
                        id = outcome.paymentSheet.customerId,
                        ephemeralKeySecret = outcome.paymentSheet.ephemeralKey,
                    ),
                    googlePay = PaymentSheet.GooglePayConfiguration(
                        environment = if (BuildConfig.DEBUG)
                            PaymentSheet.GooglePayConfiguration.Environment.Test
                        else
                            PaymentSheet.GooglePayConfiguration.Environment.Production,
                        countryCode = "CZ",
                    ),
                    allowsDelayedPaymentMethods = false,
                ),
            )
            // Result handled in PaymentSheet callback (separate file).
        }

      Use rememberPaymentSheet { result -> ... } to receive the result.
      On Completed → navigate to success. On Canceled → snackbar
      "Payment cancelled, you can try again". On Failed → snackbar with
      error.localizedMessage.

files_to_create:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/payments/PaymentSheetHandler.kt
    change: |
      Composable utility wrapping rememberPaymentSheet + result handler
      so the BookingBottomSheet stays readable. Returns a launcher that
      callers invoke with PaymentSheetParams.

dependencies: [TASK-PA6]

verification:
  - MANUAL_STEP: provision STRIPE_PUBLISHABLE_KEY in gradle.properties
    (test mode initially)
  - Real device test:
    - Pick services, address, time, Card payment, swipe to confirm
    - Order is created (visible in admin)
    - PaymentSheet opens, accept test card 4242 4242 4242 4242
    - Order moves to Paid via webhook
    - App navigates to success screen
    - Open the same app session, book another card order
    - PaymentSheet shows the saved card from previous booking (Stripe customer reuse)
  - Test 3DS card 4000 0027 6000 3184 → SCA challenge appears, completes successfully
  - Test failed card 4000 0000 0000 0002 → snackbar, no navigate
  - Test PaymentSheet dismissed without paying → snackbar, order stays in Pending (not Cancelled)
```

### TASK-PA8: Pending-payment cleanup

```yaml
task: Stale unpaid orders auto-cancel after a window so they don't pollute the matching pool
id: TASK-PA8
type: feature
priority: medium
specialist: backend
estimated_complexity: small

context: |
  If a user creates a card order then closes PaymentSheet without paying,
  the order sits in Pending forever. Bad for: matching pool integrity
  (cleaners can't pick it up), admin dashboards (zombie rows), Stripe
  PaymentIntent expiry (Stripe expires intents after ~24h anyway).

  Add a daily Azure Function: any order in Pending with PaymentType=Card
  older than 1 hour → mark Cancelled with reason "payment_not_completed".

files_to_create:
  - path: src/Cleansia.Functions/Functions/CleanupStalePendingOrders.cs
    change: |
      Standard timer-triggered function (cron: every 15 minutes).
      Query: orders where PaymentStatus=Pending AND PaymentType=Card
             AND CreatedOn < UtcNow - 1h.
      For each: order.MarkCancelled(reason: "payment_not_completed").
      Save. Log count.

dependencies: [TASK-PA6]

verification:
  - Local test: create card order, don't pay, wait 1h, run function
    → order is Cancelled
  - Function logs the count of orders cleaned up
  - Stripe webhook for the eventually-canceled PaymentIntent is a no-op
    (order is already Cancelled)
```

---

## Phase 3 — Recurring booking infrastructure (data only, no UI)

### TASK-PA9: RecurringBookingTemplate entity + materializer

```yaml
task: Add RecurringBookingTemplate entity + daily Azure Function that materializes orders 7 days ahead
id: TASK-PA9
type: feature
priority: medium
specialist: backend
estimated_complexity: medium

context: |
  Plus's "every other Tuesday at 10am" perk needs:
    1. A template entity describing the recurrence + booking parameters
    2. A daily job that materializes templates into concrete Order rows
       N days ahead

  No UI in this spec — the table + materializer is the foundation.
  When Plus launches, customer-facing "Recurring bookings" UI creates +
  manages templates.

files_to_create:
  - path: src/Cleansia.Core.Domain/Bookings/RecurringBookingTemplate.cs
    change: |
      public class RecurringBookingTemplate : Auditable, ITenantEntity {
          [Required] public string UserId { get; private set; }
          public User User { get; private set; }

          public RecurrenceFrequency Frequency { get; private set; }
              // Weekly, Biweekly, Monthly

          public DayOfWeek DayOfWeek { get; private set; }
          public TimeOnly TimeOfDay { get; private set; }
          public int Rooms { get; private set; }
          public int Bathrooms { get; private set; }

          // Snapshots — same shape as Order's customer-facing fields
          [Required] public string SavedAddressId { get; private set; }
          public SavedAddress SavedAddress { get; private set; }

          private List<string> _selectedServiceIds = [];
          public IReadOnlyCollection<string> SelectedServiceIds => _selectedServiceIds.AsReadOnly();
          private List<string> _selectedPackageIds = [];
          public IReadOnlyCollection<string> SelectedPackageIds => _selectedPackageIds.AsReadOnly();

          public PaymentType PaymentType { get; private set; }
          public DateTime StartsOn { get; private set; }
          public DateTime? EndsOn { get; private set; }   // null = indefinite
          public bool IsActive { get; private set; } = true;
          public DateTime? LastMaterializedFor { get; private set; }
              // The DateTime of the most-recently materialized occurrence.
              // Materializer reads this to know what to skip.

          public static RecurringBookingTemplate Create(...) { ... }
          public RecurringBookingTemplate Pause() { IsActive = false; return this; }
          public RecurringBookingTemplate Resume() { IsActive = true; return this; }
          public RecurringBookingTemplate MarkMaterializedFor(DateTime occurrenceUtc) {
              LastMaterializedFor = occurrenceUtc; return this;
          }
      }

  - path: src/Cleansia.Core.Domain/Bookings/RecurrenceFrequency.cs
    change: |
      public enum RecurrenceFrequency { Weekly = 1, Biweekly = 2, Monthly = 3 }

  - path: src/Cleansia.Functions/Functions/MaterializeRecurringBookings.cs
    change: |
      Daily timer (e.g. 02:00 UTC). For each active template:
        - Compute the next occurrence dates within the 7-day horizon
          based on Frequency + DayOfWeek + TimeOfDay + LastMaterializedFor.
        - For each new occurrence, send a CreateOrder.Command with
          SavedAddressId, services/packages from snapshot, PaymentType,
          PreferredEmployeeId from the most-recent successfully-completed
          order in this template's history (auto-prefer the previous cleaner).
        - Update template.LastMaterializedFor.
        - Log creates + failures (a single-template failure shouldn't kill
          the whole job — wrap each in try/catch).

      Edge cases:
        - If user has no payment method on file (Card flow): create order
          in Pending, send notification "your recurring booking needs
          payment confirmation". Don't auto-charge — the user must
          confirm to comply with SCA / Czech consumer law.
        - If user's membership has lapsed (was Plus, now isn't): still
          materialize the order, but the discount path goes through
          best-wins normally — they just lose the membership perk.

dependencies: [TASK-PA3]

verification:
  - Unit test: Weekly template, LastMaterializedFor=2026-04-25 → run
    on 2026-04-27 → creates orders for 2026-05-02, 2026-05-04 (within
    7-day horizon)
  - Unit test: Biweekly skips correctly
  - Integration test: full materialize flow with seed template → real
    Order rows appear in DB
  - MANUAL_STEP: register the function in host.json
```

---

## Phase 4 — Manual steps + verification gates

### MANUAL-STEP-1: EF Core migrations
After Phase 1 + Phase 3 land, owner runs:
```
dotnet ef migrations add AddPaymentAndPlusFoundations \
  --project src/Cleansia.Infra.Database \
  --startup-project src/Cleansia.AppHost
dotnet ef database update \
  --project src/Cleansia.Infra.Database \
  --startup-project src/Cleansia.AppHost
```
Migration should add:
- `User.StripeCustomerId`
- `Order.PreferredEmployeeId`, `MembershipDiscountAmount`, `MembershipPlanIdAtPurchase`
- `MembershipPlans` table (empty)
- `UserMemberships` table (empty)
- `RecurringBookingTemplates` table (empty)

### MANUAL-STEP-2: NSwag regeneration
After backend DTO changes (TASK-PA6 most relevantly):
```
cd src/Cleansia.App && npm run generate-customer-client
```
Surfaces `CreatePaymentIntent` + response DTOs to web (web doesn't need to USE them yet, but the client should be consistent).

### MANUAL-STEP-3: Stripe configuration
Owner must:
1. Verify Stripe test-mode keys are in dev environment configs (likely already are for the existing checkout flow).
2. Provision `STRIPE_PUBLISHABLE_KEY` for mobile in `~/.gradle/gradle.properties` (test mode initially).
3. Configure webhook endpoint in Stripe Dashboard to point at the Functions webhook URL — add `payment_intent.*` events alongside existing `checkout.session.*` events.

### MANUAL-STEP-4: Mobile build verification
After TASK-PA7 lands:
```
./gradlew :app:assembleDebug
```
(No gradlew exists in customer Android repo currently — use Android Studio sync.)

### MANUAL-STEP-5: End-to-end test on real device
- Cash flow: book → submit → success (regression check, should be unchanged)
- Card flow new user: book → submit → PaymentSheet opens with Google Pay + card form → pay with test card → success → admin shows Paid
- Card flow returning user: same flow → PaymentSheet shows saved card
- Card flow with 3DS: book with test card 4000 0027 6000 3184 → SCA challenge → success
- Card flow cancelled: open PaymentSheet, dismiss → snackbar, no nav
- Stale Pending cleanup: leave a card order unpaid for 1h → cron fires → order Cancelled

---

## Execution order

1. **TASK-PA1** — User.StripeCustomerId (foundation, no dependencies)
2. **TASK-PA2** — Membership entities (foundation, no dependencies)
3. **TASK-PA3** — PreferredEmployeeId on Order (foundation, no dependencies)
4. **TASK-PA4** — Pricing pipeline accepts membership (depends on PA2)
5. **TASK-PA5** — CancellationPolicyResolver (depends on PA2)
6. **MANUAL-STEP-1** — Run migrations
7. **TASK-PA6** — CreatePaymentIntent endpoint (depends on PA1, PA4)
8. **MANUAL-STEP-2** — NSwag regen
9. **MANUAL-STEP-3** — Stripe webhook endpoint config in Dashboard
10. **TASK-PA7** — Mobile PaymentSheet (depends on PA6, MANUAL-3)
11. **TASK-PA8** — Stale Pending cleanup function (depends on PA6)
12. **TASK-PA9** — RecurringBookingTemplate + materializer (depends on PA3)
13. **MANUAL-STEPS-4 + 5** — Mobile build + e2e test on device

Parallelizable:
- PA1, PA2, PA3 can run simultaneously (independent)
- PA4 + PA5 can run simultaneously after PA2
- PA8 + PA9 can run simultaneously after PA6 + PA3 respectively

Estimated effort: **3-4 working days** for one specialist working through sequentially. PA2 and PA9 are the heaviest. PA7 is the most user-visible and has the highest test surface (real Stripe flows).

---

## When Cleansia Plus is ready to launch (Phase 5 — NOT YET STARTED)

The architecture foundation is shipped. Plus launch is its own self-contained
spec; nothing below requires backend refactoring because every hook is in place.

### Phase 5A — Backend: Stripe subscription wiring

```yaml
task: Subscription create/cancel commands + customer.subscription.* webhook handlers
id: TASK-PA10
type: feature
priority: high
specialist: backend
estimated_complexity: medium
status: pending

context: |
  Add backend ability to create / cancel Stripe subscriptions and reflect
  state changes via webhooks. UserMembership rows are written here; the
  pricing pipeline + cancellation policy resolver already read from them.

files_to_create:
  - src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipSubscription.cs
    # Command(membershipPlanCode), creates Stripe subscription against user's
    # StripeCustomerId, returns SetupIntent client_secret if payment method
    # capture is needed first. Idempotent: bails if user already has Active.
  - src/Cleansia.Core.AppServices/Features/Memberships/CancelMembershipSubscription.cs
    # Calls Stripe with cancel_at_period_end=true, marks UserMembership
    # CancelledAt locally. Webhook eventually flips Status to Cancelled.
  - src/Cleansia.Core.AppServices/Features/Memberships/GetMyMembership.cs
    # Query for "what's my Plus status?" — returns plan, period end, status.
  - src/Cleansia.Web.Customer/Controllers/MembershipController.cs
  - Extend HandlePaymentNotification.cs to handle:
      customer.subscription.created
      customer.subscription.updated
      customer.subscription.deleted
      invoice.payment_failed (transition to PastDue)
    All four resolve UserMembership by StripeSubscriptionId and call
    UpdateFromStripeWebhook with the new status + period bounds.
  - Extend IStripeClient with:
      CreateSubscriptionAsync(customerId, priceId)
      CancelSubscriptionAtPeriodEndAsync(subscriptionId)

files_to_modify:
  - src/Cleansia.Core.AppServices/Common/Constants.cs
    # Add subscription event type constants alongside the PaymentIntent ones.
```

### Phase 5B — Customer web: Subscribe to Plus + manage subscription

```yaml
task: Customer-facing Plus subscribe + management screens on the web
id: TASK-PA11
type: feature
priority: high
specialist: frontend
app: customer-web
estimated_complexity: medium
status: pending

context: |
  Two surfaces:
    1. "Cleansia Plus" landing/upgrade page (likely on the rewards or
       profile feature lib). Lists benefits, shows price, primary CTA
       opens Stripe Checkout in subscription mode (or PaymentSheet via
       Stripe.js for in-page UX).
    2. "My membership" management section in profile — current plan, next
       billing date, cancel button (confirms then calls
       CancelMembershipSubscription).

files_to_create:
  - libs/cleansia-customer-features/membership/  (new feature lib)
      - subscribe-page.component.{ts,html,scss}   # marketing + CTA
      - membership-management.component.{ts,html,scss}  # profile section
      - membership.facade.ts                       # state + API calls
  - i18n keys for all benefit copy + CTAs in 5 locales

dependencies: [TASK-PA10]
```

### Phase 5C — Customer mobile: Subscribe to Plus + manage

```yaml
task: Mobile equivalent of Phase 5B
id: TASK-PA12
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
status: pending

context: |
  Subscribe flow uses Stripe PaymentSheet in setupIntent mode (one-time
  payment method capture, then backend creates the subscription).
  Management lives in the existing Profile screen as a new card.

files_to_create:
  - features/membership/SubscribePlusScreen.kt
  - features/membership/MembershipManagementCard.kt  (rendered from ProfileScreen)
  - features/membership/MembershipViewModel.kt
  - core/memberships/MembershipApi.kt + DTOs + Module + Repository

dependencies: [TASK-PA10]
```

### Phase 5D — "Request favorite cleaner" UI

```yaml
task: Surface PreferredEmployeeId picker on the booking flow for Plus members
id: TASK-PA13
type: feature
priority: medium
specialist: mobile + frontend
estimated_complexity: small
status: pending

context: |
  Backend already accepts PreferredEmployeeId on CreateOrder.Command (with
  validation that the user has had a Completed order with that cleaner).
  Today no UI sets it. For Plus members, surface a "Request your favorite
  cleaner" toggle/dropdown on the booking confirm step.

  Discovery: query "previously-served cleaners" from order history (need a
  small backend query helper — `GET /api/Order/MyServingCleaners`).
  List shows cleaner name + rating + last-served date; one tap selects.

files_to_create:
  - Backend: src/Cleansia.Core.AppServices/Features/Orders/GetMyServingCleaners.cs
  - Mobile: features/booking/PreferredCleanerPicker.kt
  - Web: integration into wizard-summary-step.component.ts
  - Gate visibility via membershipFacade.isActive() — non-Plus users don't see it

dependencies: [TASK-PA10]
```

### Phase 5E — Recurring bookings UI

```yaml
task: Customer UI to create / pause / delete RecurringBookingTemplate rows
id: TASK-PA14
type: feature
priority: medium
specialist: mobile + frontend
estimated_complexity: medium
status: pending

context: |
  Materializer cron already runs nightly (TASK-PA9). Add the customer-facing
  CRUD: list templates, create with frequency/day/time/services/address picker,
  pause/resume/delete.

  Backend: add CreateRecurringBookingTemplate / UpdateRecurringBookingTemplate
  / PauseRecurringBookingTemplate / ListMyRecurringBookingTemplates handlers.

  Mobile: RecurringBookingsListScreen + RecurringBookingFormScreen accessed
  from Profile. Web: equivalent in profile feature lib.

  Gate visibility on Plus membership.

dependencies: [TASK-PA10]
```

### Phase 5F — Cancellation UI surfaces the wider window

```yaml
task: Show "free cancellation up to 4h before" instead of generic 24h for Plus members
id: TASK-PA15
type: feature
priority: low
specialist: mobile + frontend
estimated_complexity: trivial
status: pending

context: |
  CancellationPolicyResolver already returns the right window. Surface it
  in the cancel-order confirmation dialogs (mobile CancelOrderSheet, web
  cancel modal) by calling a new `GET /api/Order/MyCancellationPolicy?orderId=…`
  endpoint that returns the resolved policy.

dependencies: [TASK-PA10]
```

---

## Side-effects from NSwag regen — admin Service category picker

```yaml
task: Add categoryId picker to admin Service create/edit form
id: TASK-PA16
type: bugfix
priority: medium
specialist: frontend
app: admin-web
estimated_complexity: small
status: pending

context: |
  Surfaced when the customer NSwag client was regenerated and `categoryId`
  became required on `ICreateServiceCommand` / `IUpdateServiceCommand`.
  Build is currently unblocked by sending `undefined` (with TODO comments
  in service-form.facade.ts) but the backend validator will reject any
  create/update because category is required server-side.

  Fix: add a category picker (PrimeNG `<p-dropdown>`) to the form, populated
  by `adminServiceCategoryClient.getAll()` (or the existing categories
  endpoint — grep first), bind to ServiceFormData.categoryId, remove the
  TODO comments, send the real id.

files_to_modify:
  - libs/cleansia-admin-features/service-management/src/lib/service-form/service-form.component.{ts,html}
  - libs/cleansia-admin-features/service-management/src/lib/service-form/service-form.facade.ts
    # ServiceFormData.categoryId field + load categories on init

verification:
  - npx nx build cleansia-admin.app
  - Admin can create + edit a service with a real category, backend accepts
```

---

## Manual operational steps still on the owner

These never had code work attached — they're external configuration:

1. **EF migration** — `dotnet ef migrations add AddPaymentAndPlusFoundations` from `src/Cleansia.Infra.Database`, then `dotnet ef database update`
2. **Stripe webhook config** — in Stripe Dashboard, ensure the webhook endpoint subscribes to `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`, AND (when Phase 5A lands) `customer.subscription.created`/`updated`/`deleted` + `invoice.payment_failed`
3. **Mobile Stripe key** — `STRIPE_PUBLISHABLE_KEY=pk_test_...` in `~/.gradle/gradle.properties` (test mode initially)
4. **End-to-end mobile test** — book card order, verify PaymentSheet flow on real device with Stripe test cards (4242, 4000-0027-6000-3184 for SCA, 4000-0000-0000-0002 for decline)

---

## Execution order for Phase 5

1. **TASK-PA10** — backend subscription wiring (foundation for all Plus UI)
2. **TASK-PA11 + PA12** — customer subscribe/manage UI (web + mobile, parallel)
3. **TASK-PA13** — favorite cleaner picker (parallel with PA11/PA12)
4. **TASK-PA14** — recurring bookings UI (parallel with PA11/PA12)
5. **TASK-PA15** — cancellation policy surfacing (smallest, can land anytime after PA10)
6. **TASK-PA16** — admin Service category picker (independent of Plus, can land anytime)

PA10 is the gate; everything else can run in parallel after it.

Estimated total effort for Phase 5: ~5-7 working days for one full-stack engineer.

---

## Out of scope (followup specs)

- **Cleansia Plus launch spec** (above) — separate, when product is ready
- **Tipping flow** — Stripe addition to completed orders
- **Stripe Connect for cleaner payouts** — current invoicing is manual; Connect would automate
- **iOS Stripe** — when iOS work begins
- **Admin UI for membership management** — Stripe Dashboard suffices early
- **Subscription billing failure recovery flow** — Stripe handles retries; UI for "your card declined, update it" is a launch-time thing
- **Per-Plus-tier benefit usage tracking** (e.g. "you've used 0 of 1 free express upgrade this month") — when Plus has multiple usage-capped perks
- **Multi-plan ladder** (Plus / Plus Pro / Plus Family) — only when there's data showing Plus has product-market fit
