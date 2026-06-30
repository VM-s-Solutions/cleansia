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

    private let repository: DisputeRepository
    private let snackbar: SnackbarController

    init(orderId: String?, repository: DisputeRepository, snackbar: SnackbarController) {
        let trimmed = orderId?.trimmingCharacters(in: .whitespacesAndNewlines)
        self.orderId = (trimmed?.isEmpty ?? true) ? nil : trimmed
        self.repository = repository
        self.snackbar = snackbar
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
        switch await repository.create(orderId: orderId, reason: reason, description: trimmed) {
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
