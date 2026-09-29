import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

enum LegalDocumentNotice: Equatable {
    case textUpdated
}

/// `legal.document_not_in_force` is the one refusal handled here: a newer version came into force while
/// the old text was on screen, so the list is re-read and the cleaner is told to read it again.
@MainActor
final class LegalDocumentsViewModel: ViewModel {
    @Published private(set) var state: UiState<[CleanerLegalDocument]> = .loading
    @Published private(set) var actionState: ActionState = .idle
    @Published private(set) var notice: LegalDocumentNotice?

    let accepted = PassthroughSubject<LegalDocumentType, Never>()

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController
    private let languageTag: () -> String
    private let localizer = ApiErrorLocalizer()

    private static let notInForceKey = "legal.document_not_in_force"

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        languageTag: @escaping () -> String = { CoreL10n.languageTag }
    ) {
        self.client = client
        self.snackbar = snackbar
        self.languageTag = languageTag
    }

    /// A re-read keeps the list on screen, so an open document is replaced rather than closed.
    func load() async {
        if state.loadedValue == nil {
            state = .loading
        }
        switch await client.getLegalDocuments(language: languageTag()) {
        case let .success(documents):
            state = .loaded(documents)
        case let .failure(error):
            if state.loadedValue == nil {
                state = .error(error)
            } else {
                snackbar.showApiError(error)
            }
        }
    }

    func document(ofType type: LegalDocumentType?) -> CleanerLegalDocument? {
        guard let type else { return nil }
        return state.loadedValue?.first { $0.type == type }
    }

    func onDocumentClosed() {
        guard !actionState.isSubmitting else { return }
        actionState = .idle
        notice = nil
    }

    func accept(_ document: CleanerLegalDocument) async {
        guard !actionState.isSubmitting else { return }
        actionState = .submitting
        switch await client.acceptLegalDocument(legalDocumentTextId: document.legalDocumentTextId) {
        case .success:
            actionState = .idle
            notice = nil
            snackbar.showSuccess(L10n.LegalDocuments.acceptedToast)
            accepted.send(document.type)
            await load()
        case let .failure(error):
            if error.code == Self.notInForceKey {
                notice = .textUpdated
                actionState = .idle
                await load()
            } else {
                actionState = .error(localizer.message(for: error))
            }
        }
    }
}
