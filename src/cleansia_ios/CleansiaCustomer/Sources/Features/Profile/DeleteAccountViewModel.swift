import CleansiaCore
import Combine
import Foundation

@MainActor
final class DeleteAccountViewModel: ViewModel {
    @Published private(set) var deleteState: ActionState = .idle

    let accountDeleted = PassthroughSubject<Void, Never>()

    private let client: GdprDeleteClient
    private let authClient: AuthClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: GdprDeleteClient, authClient: AuthClient, snackbar: SnackbarController) {
        self.client = client
        self.authClient = authClient
        self.snackbar = snackbar
    }

    func confirmDelete() async {
        guard !deleteState.isSubmitting else { return }
        deleteState = .submitting
        switch await client.deleteMyAccount() {
        case .success:
            await authClient.signOutLocal()
            deleteState = .idle
            accountDeleted.send()
        case let .failure(error):
            let message = errorMessage(for: error)
            snackbar.showError(message)
            deleteState = .error(message)
        }
    }

    private func errorMessage(for error: ApiError) -> String {
        switch GdprDeletionBlock(code: error.code) {
        case .blockedByOrder: L10n.DeleteAccount.errorBlockedByOrder
        case .blockedByInvoice: L10n.DeleteAccount.errorBlockedByInvoice
        case .alreadyPending: L10n.DeleteAccount.errorAlreadyPending
        case nil: localizer.message(for: error)
        }
    }
}
