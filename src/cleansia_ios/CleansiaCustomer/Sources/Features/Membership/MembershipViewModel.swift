import CleansiaCore
import Combine
import Foundation

@MainActor
final class MembershipViewModel: ViewModel {
    @Published private(set) var current: MyMembership?
    @Published private(set) var plans: [MembershipPlan] = []
    @Published private(set) var submitState: ActionState = .idle

    private let repository: MembershipRepository
    private let snackbar: SnackbarController
    private let isCardPaymentAvailable: Bool
    private var cancellables: Set<AnyCancellable> = []

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
        current = repository.current
        plans = repository.plans
        repository.$current.assign(to: \.current, on: self).store(in: &cancellables)
        repository.$plans.assign(to: \.plans, on: self).store(in: &cancellables)
    }

    /// Fail-closed gate: the Subscribe CTA is hidden AND the
    /// subscribe branch is unreachable when the publishable key is empty.
    var canSubscribe: Bool {
        isCardPaymentAvailable
    }

    func load() async {
        await repository.refresh()
        await repository.refreshPlans()
    }

    func refresh() async {
        await repository.refresh()
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
            snackbar.showApiError(error)
            return .failed
        }
    }

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
            snackbar.showApiError(error)
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
