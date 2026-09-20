import CleansiaCore
import Foundation

/// The contract for work a cleaner accepted for one seat of the customer's order, as the server
/// rendered it: the accepted document's text in the customer's UI language, the job facts frozen at
/// the acceptance, and the acceptance itself. Keyed on the acceptance, so a dropped cleaner's contract
/// stays readable; a retry re-asks in the language current at that moment.
///
/// A refused read (another customer's acceptance, a stale id) is the error state with nothing of the
/// order on it, and the refusal rides the snackbar as the order detail's own load does. A transport
/// failure gets no second sentence — the error state already says the contract could not be loaded.
@MainActor
final class WorkContractViewModel: ViewModel {
    @Published private(set) var state: UiState<WorkContract> = .loading

    private let acceptanceId: String
    private let client: OrderClient
    private let snackbar: SnackbarController
    private let languageTag: () -> String

    init(
        acceptanceId: String,
        client: OrderClient,
        snackbar: SnackbarController,
        languageTag: @escaping () -> String = { CoreL10n.languageTag }
    ) {
        self.acceptanceId = acceptanceId
        self.client = client
        self.snackbar = snackbar
        self.languageTag = languageTag
    }

    func load() async {
        guard !acceptanceId.isBlank else {
            state = .error(ApiError(code: "missing_acceptance_id"))
            return
        }
        state = .loading
        switch await client.getWorkContract(acceptanceId: acceptanceId, language: languageTag()) {
        case let .success(contract):
            state = .loaded(contract)
        case let .failure(error):
            if error.httpStatus != nil { snackbar.showApiError(error) }
            state = .error(error)
        }
    }
}
