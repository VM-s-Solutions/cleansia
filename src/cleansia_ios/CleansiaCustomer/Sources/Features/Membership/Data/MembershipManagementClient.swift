import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol MembershipManagementClient: Sendable {
    func getMine() async -> ApiResult<MyMembership>
    func getPlans() async -> ApiResult<[MembershipPlan]>
    func subscribe(planCode: String, paymentMethodConfirmed: Bool, idempotencyToken: String) async
        -> ApiResult<SubscriptionSetup>
    func cancel() async -> ApiResult<Date?>
    func swapPlan(newPlanCode: String) async -> ApiResult<Void>
}

struct LiveMembershipManagementClient: MembershipManagementClient {
    func getMine() async -> ApiResult<MyMembership> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipGetMine().toDomain()
        }
    }

    func getPlans() async -> ApiResult<[MembershipPlan]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipGetPlans().map { try $0.toDomain() }
        }
    }

    func subscribe(
        planCode: String,
        paymentMethodConfirmed: Bool,
        idempotencyToken: String
    ) async -> ApiResult<SubscriptionSetup> {
        let command = CreateMembershipSubscriptionCommand(
            planCode: planCode,
            paymentMethodConfirmed: paymentMethodConfirmed,
            idempotencyToken: idempotencyToken
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI
                .membershipSubscribe(createMembershipSubscriptionCommand: command)
                .toDomain()
        }
    }

    func cancel() async -> ApiResult<Date?> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipCancel()
        }
        return result.map(\.effectiveEndDate)
    }

    func swapPlan(newPlanCode: String) async -> ApiResult<Void> {
        let command = SwapMembershipPlanCommand(newPlanCode: newPlanCode)
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMembershipAPI.membershipSwapPlan(swapMembershipPlanCommand: command)
        }
        return result.map { _ in () }
    }
}

/// **Refuse.** `hasMembership` is the switch every membership surface reads, and `false` is a claim:
/// coerced, it withdraws the discount, the cancellation window and the recurring bookings from
/// someone who is paying for them.
///
/// The quota pair stays optional all the way to `ExpressWaiverStatus`. The server states three
/// shapes — *null = no membership, 0 = exhausted or trialing* — and a resolver that cannot tell
/// which says nothing rather than picking one.
private extension GetMyMembershipResponse {
    func toDomain() throws -> MyMembership {
        try MyMembership(
            hasMembership: hasMembership.require("hasMembership"),
            planCode: planCode,
            planName: planName,
            discountPercentage: discountPercentage,
            freeCancellationWindowHours: freeCancellationWindowHours,
            allowsExpressUpgrade: allowsExpressUpgrade,
            currentPeriodEnd: currentPeriodEnd,
            cancelRequested: cancelRequested.require("cancelRequested"),
            billingInterval: billingInterval,
            expressUpgradesPerMonth: expressUpgradesPerMonth,
            expressUpgradesRemaining: expressUpgradesRemaining,
            trialEndsAtUtc: trialEndsAtUtc
        )
    }
}

/// **Refuse the page.** The plans are alternatives to each other, so a dropped one is a different
/// purchase the customer is never offered rather than a shorter list — and every number here is
/// printed on the card they choose from. A coerced `price` of `0` advertises a paid plan as free,
/// and `billingInterval` decides annual-versus-monthly, so a default there re-labels the whole
/// screen.
private extension GetMembershipPlansResponse {
    func toDomain() throws -> MembershipPlan {
        try MembershipPlan(
            code: code.requireNonBlank("code"),
            name: name.requireNonBlank("name"),
            price: price.require("price"),
            monthlyEquivalentPrice: monthlyEquivalentPrice.require("monthlyEquivalentPrice"),
            billingInterval: billingInterval.require("billingInterval"),
            discountPercentage: discountPercentage.require("discountPercentage"),
            freeCancellationWindowHours: freeCancellationWindowHours.require("freeCancellationWindowHours"),
            allowsExpressUpgrade: allowsExpressUpgrade.require("allowsExpressUpgrade"),
            trialPeriodDays: trialPeriodDays.require("trialPeriodDays"),
            savingsPercentVsMonthly: savingsPercentVsMonthly.require("savingsPercentVsMonthly")
        )
    }
}

/// **One endpoint, two payloads — and the refusal has to know which one it is looking at.**
///
/// `POST /api/Membership/Subscribe` answers in two shapes, because it is called twice per
/// subscription:
///
/// - **Setup** (`paymentMethodConfirmed: false`) mints a Stripe SetupIntent and returns the three
///   inputs the payment sheet needs. No membership exists yet, so `membershipId` is `""`.
/// - **Confirm** (`paymentMethodConfirmed: true`) creates the membership and returns its id. The
///   Stripe inputs are spent, so the server returns them as `""`.
///
/// The guard used to demand all four in both, which meant it fired on the FIRST call of every
/// subscription: `membershipId` is legitimately blank there, `requireNonBlank` read that as absence,
/// and the customer got "The server sent incomplete data" before Stripe was ever reached. The trial
/// could not be started at all. `MembershipViewModel` already branched on `membershipId.isEmpty` at
/// both call sites — the guard contradicted the caller it was protecting.
///
/// The refusal is kept exactly where it earns its keep: a blank Stripe input does not degrade the
/// payment, it hands Stripe an empty string and fails inside a third-party UI where the reason never
/// reaches us. So the setup shape still demands all three, and the confirm shape demands the id.
/// `stripeCustomerId` is required by both because both return it.
private extension CreateMembershipSubscriptionResponse {
    func toDomain() throws -> SubscriptionSetup {
        let customerId = try stripeCustomerId.requireNonBlank("stripeCustomerId")

        // Confirm: the membership exists, and the Stripe inputs are deliberately spent.
        if let id = membershipId, !id.isBlank {
            return SubscriptionSetup(
                membershipId: id,
                setupIntentClientSecret: "",
                stripeCustomerId: customerId,
                ephemeralKey: ""
            )
        }

        // Setup: no membership yet, and every input the sheet needs must actually be there.
        return try SubscriptionSetup(
            membershipId: "",
            setupIntentClientSecret: setupIntentClientSecret.requireNonBlank("setupIntentClientSecret"),
            stripeCustomerId: customerId,
            ephemeralKey: ephemeralKey.requireNonBlank("ephemeralKey")
        )
    }
}
