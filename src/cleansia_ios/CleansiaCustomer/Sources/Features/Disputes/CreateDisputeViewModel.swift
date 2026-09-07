import CleansiaCore
import Combine
import Foundation

@MainActor
final class CreateDisputeViewModel: ViewModel {
    @Published private(set) var submitState: ActionState = .idle

    let created = PassthroughSubject<String, Never>()

    /// Nil when the route carried no orderId (the FAB flow). A null orderId
    /// renders a graceful missing-order banner + a submit error, never a crash
    /// (the `CreateDisputeViewModel.kt` parity).
    let orderId: String?

    /// What was ON this order, so the customer can point at the parts that were not done properly.
    ///
    /// Empty until the order loads, empty for the FAB flow that arrives with no order, and empty if
    /// the fetch fails. The form omits the section in every one of those cases: a dispute names no
    /// items by default, and failing to load them must never stop someone filing one. That is why the
    /// load below is SILENT — no spinner, no error, no retry.
    @Published private(set) var lineOptions: [OrderItemLine] = []

    /// Ticked rows, by `OrderItemLine.id`.
    @Published var pickedLineIds: Set<String> = []

    private let repository: DisputeRepository
    private let orderClient: OrderClient
    private let snackbar: SnackbarController

    init(
        orderId: String?,
        repository: DisputeRepository,
        orderClient: OrderClient,
        snackbar: SnackbarController
    ) {
        let trimmed = orderId?.trimmingCharacters(in: .whitespacesAndNewlines)
        self.orderId = (trimmed?.isEmpty ?? true) ? nil : trimmed
        self.repository = repository
        self.orderClient = orderClient
        self.snackbar = snackbar
    }

    /// Load the order's items. Called by the view on appear; silent on every failure.
    func loadLineOptions() async {
        guard let orderId, lineOptions.isEmpty else { return }
        if case let .success(order) = await orderClient.getById(orderId: orderId) {
            lineOptions = OrderItemLine.lines(of: order)
        }
    }

    func toggleLine(_ line: OrderItemLine) {
        if pickedLineIds.contains(line.id) {
            pickedLineIds.remove(line.id)
        } else {
            pickedLineIds.insert(line.id)
        }
    }

    var hasOrderContext: Bool {
        orderId != nil
    }

    func descriptionIsValid(_ text: String) -> Bool {
        let range = DisputeFormConstants.descriptionMinLength ... DisputeFormConstants.descriptionMaxLength
        return range.contains(text.count)
    }

    func submit(reason: Int, description: String) async {
        guard !submitState.isSubmitting else { return }
        guard let orderId else {
            submitState = .error(L10n.Disputes.createMissingOrder)
            return
        }
        let trimmed = description.trimmingCharacters(in: .whitespacesAndNewlines)
        guard descriptionIsValid(trimmed), (1 ... 7).contains(reason) else { return }

        submitState = .submitting
        // Only rows still on the loaded order. Nothing can go stale here today — the order is fixed
        // by the route — but the filter keeps the invariant local rather than assumed.
        let lines = lineOptions.filter { pickedLineIds.contains($0.id) }
        switch await repository.create(
            orderId: orderId,
            reason: reason,
            description: trimmed,
            lines: lines
        ) {
        case let .success(id):
            submitState = .idle
            await repository.refresh()
            created.send(id)
        case let .failure(error):
            snackbar.showApiError(error)
            submitState = .error(L10n.Disputes.createRetryHint)
        }
    }

    func clearError() {
        if case .error = submitState { submitState = .idle }
    }
}
