import CleansiaCore
import Combine
import Foundation

@MainActor
final class MembershipViewModel: ViewModel {
    @Published private(set) var current: MyMembership?
    @Published private(set) var plans: [MembershipPlan] = []
    /// The plans priced for the chosen market. `.loaded([])` is Plus not on sale in that market —
    /// no price and no button, never a zero.
    @Published private(set) var plansState: UiState<[MembershipPlan]> = .loading
    @Published private(set) var submitState: ActionState = .idle

    private let repository: MembershipRepository
    private let snackbar: SnackbarController
    private let isCardPaymentAvailable: Bool

    private var subscribeIdempotencyToken: String?

    init(
        repository: MembershipRepository,
        snackbar: SnackbarController,
        isCardPaymentAvailable: Bool = StripeConfig.isCardPaymentAvailable
    ) {
        self.repository = repository
        self.snackbar = snackbar
        self.isCardPaymentAvailable = isCardPaymentAvailable
        super.init()
        repository.$current.assign(to: &$current)
        repository.$plans.assign(to: &$plans)
        repository.$plansState.assign(to: &$plansState)
    }

    /// Fail-closed gate: the Subscribe CTA is hidden AND the
    /// subscribe branch is unreachable when the publishable key is empty.
    var canSubscribe: Bool {
        isCardPaymentAvailable
    }

    var expressWaiverStatus: ExpressWaiverStatus {
        ExpressWaiverStatus.resolve(current)
    }

    var expressWaiverAdvertised: Bool {
        expressWaiverStatus.isAdvertised
    }

    func load() async {
        await repository.refresh()
        await repository.refreshPlans()
    }

    func refresh() async {
        await repository.refresh()
    }

    func reloadPlans() async {
        await repository.refreshPlans()
    }

    /// The annual switch is offered only when the plan's price can be stated in the subscription's
    /// own currency: a swap charges the plan's row in THAT currency, whatever market is chosen, so a
    /// price labelled with another market's code would name a figure the customer is not charged.
    var annualSwitchPlan: MembershipPlan? {
        guard let membership = current, membership.hasMembership, !membership.cancelRequested,
              membership.billingInterval == 1,
              let yearly = plans.first(where: \.isAnnual),
              yearly.currencyCode == membership.currencyCode
        else { return nil }
        return yearly
    }

    /// Phase 1 — request a SetupIntent. Mints ONE idempotency token for this
    /// logical attempt; it is replayed unchanged on every `confirmSubscribe`.
    func startSubscribe(planCode: String) async -> SubscribeOutcome {
        guard canSubscribe else { return .failed }
        guard !submitState.isSubmitting else { return .failed }
        submitState = .submitting
        defer { submitState = .idle }

        let token = UUID().uuidString
        subscribeIdempotencyToken = token

        switch await repository.subscribePhase1(planCode: planCode, idempotencyToken: token) {
        case let .success(setup):
            if !setup.membershipId.isEmpty {
                return .alreadyActive
            }
            return .needsPaymentMethod(PaymentSheetPresentation(
                clientSecret: setup.setupIntentClientSecret,
                ephemeralKey: setup.ephemeralKey,
                stripeCustomerId: setup.stripeCustomerId,
                merchantDisplayName: "Cleansia",
                intentKind: .setup
            ))
        case let .failure(error):
            report(error)
            return .failed
        }
    }

    /// Stripe keeps one currency per Customer for life, so a second subscription in another
    /// currency is refused; the refusal names the currency the billing is locked to when the
    /// membership snapshot knows it, and says only "another currency" when it does not.
    private func report(_ error: ApiError) {
        if error.code == Self.currencyLockedKey, let currencyCode = current?.currencyCode {
            snackbar.showError(L10n.Membership.currencyLockedIn(currencyCode))
        } else {
            snackbar.showApiError(error)
        }
    }

    static let currencyLockedKey = "membership.stripe_customer_currency_locked"

    /// Phase 2 — called after PaymentSheet returns `.completed`. Replays the
    /// SAME token minted at Phase 1 so the backend collapses double-taps onto a
    /// single subscription. `.completed` is UX-only — on success we re-read
    /// `membershipGetMine` (the webhook is the sole active-state authority).
    func confirmSubscribe(planCode: String) async -> SubscribeOutcome {
        guard !submitState.isSubmitting else { return .failed }
        submitState = .submitting
        defer { submitState = .idle }

        let token = subscribeIdempotencyToken ?? UUID().uuidString
        subscribeIdempotencyToken = token

        switch await repository.subscribePhase2(planCode: planCode, idempotencyToken: token) {
        case let .success(setup):
            guard !setup.membershipId.isEmpty else {
                snackbar.showError(L10n.localized("error_generic_network"))
                return .failed
            }
            await repository.refresh()
            return .subscribed(membershipId: setup.membershipId)
        case let .failure(error):
            report(error)
            return .failed
        }
    }

    func cancel() async -> Date? {
        guard !submitState.isSubmitting else { return nil }
        submitState = .submitting
        defer { submitState = .idle }
        switch await repository.cancel() {
        case let .success(date):
            await repository.refresh()
            return date
        case let .failure(error):
            snackbar.showApiError(error)
            return nil
        }
    }

    func swapPlan(newPlanCode: String) async -> Bool {
        guard !submitState.isSubmitting else { return false }
        submitState = .submitting
        defer { submitState = .idle }
        switch await repository.swapPlan(newPlanCode: newPlanCode) {
        case .success:
            await repository.refresh()
            return true
        case let .failure(error):
            snackbar.showApiError(error)
            return false
        }
    }
}
