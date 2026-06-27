import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class DocumentsSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<[GetMyDocumentsMyDocumentDto]> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var deletingId: String?

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        switch await client.getMyDocuments() {
        case let .success(documents):
            state = .loaded(documents)
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func upload(
        documentType: DocumentType,
        fileName: String,
        contentType: String,
        base64Content: String,
        description: String?
    ) async {
        guard !action.isSubmitting else { return }
        action = .submitting
        let command = SaveMyDocumentsCommand(documents: [
            SaveMyDocumentsDocumentToSave(
                documentType: documentType,
                file: BlobFileDto(
                    fileName: fileName,
                    base64Content: base64Content,
                    contentType: contentType
                ),
                description: description?.trimmedOrNil
            )
        ])
        switch await client.saveMyDocuments(command) {
        case .success:
            action = .idle
            await load()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }

    func delete(documentId: String) async {
        guard deletingId == nil else { return }
        deletingId = documentId
        switch await client.deleteMyDocument(documentId: documentId) {
        case .success:
            deletingId = nil
            await load()
        case let .failure(error):
            deletingId = nil
            snackbar.showError(localizer.message(for: error))
        }
    }
}
