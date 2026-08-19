import CleansiaCore
import Combine
import Foundation

/// Submits the cleaner's account-deletion REQUEST. → /decisions/adr-0052
///
/// Deliberately unlike `CleansiaCustomer`'s `DeleteAccountViewModel` in two ways, and both are the
/// point rather than an omission:
///
/// - **No `authClient`, so no session teardown.** The customer flow calls `signOutLocal()` because
///   the account really is gone. Here nothing has happened yet — an admin fulfils the request after
///   the paperwork — so signing the cleaner out would lock them out of an account that still exists
///   with jobs still assigned to them. The absence of the collaborator is the structural guarantee;
///   there is no code path from here to a sign-out.
/// - **`requested` is terminal.** There is no local pending state to reload, because the server
///   already refuses a second request with `gdpr.deletion_already_pending`. A local copy of a fact
///   the backend owns is how the two drift apart.
@MainActor
final class DeleteAccountViewModel: ViewModel {
    @Published private(set) var submitState: ActionState = .idle
    @Published private(set) var requested = false

    private let client: PartnerGdprDeletionClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerGdprDeletionClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func submit() async {
        guard !submitState.isSubmitting, !requested else { return }
        submitState = .submitting

        switch await client.requestDeletion() {
        case .success:
            submitState = .idle
            requested = true
            snackbar.showSuccess(L10n.DeleteAccount.requestedSnackbar)
        case let .failure(error):
            let message = errorMessage(for: error)
            snackbar.showError(message)
            submitState = .error(message)
        }
    }

    /// The two employee-only refusals get their own words. Falling through to the generic localizer
    /// would tell a cleaner "something went wrong" when the truthful answer is "you are on a job
    /// tomorrow" — which is actionable, and which they can resolve themselves.
    private func errorMessage(for error: ApiError) -> String {
        switch PartnerDeletionBlock(code: error.code) {
        case .blockedByAssignedOrder: L10n.DeleteAccount.errorBlockedByAssignedOrder
        case .blockedByUnsettledPay: L10n.DeleteAccount.errorBlockedByUnsettledPay
        case .blockedByInvoice: L10n.DeleteAccount.errorBlockedByInvoice
        case .blockedByOrder: L10n.DeleteAccount.errorBlockedByOrder
        case .alreadyPending: L10n.DeleteAccount.errorAlreadyPending
        case nil: localizer.message(for: error)
        }
    }
}
