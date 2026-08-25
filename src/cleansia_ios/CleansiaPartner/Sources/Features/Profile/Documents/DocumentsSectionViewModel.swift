import CleansiaCore
import CleansiaPartnerApi
import Foundation

@MainActor
final class DocumentsSectionViewModel: ViewModel {
    @Published private(set) var state: UiState<[GetMyDocumentsMyDocumentDto]> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published private(set) var busyDocumentId: String?

    /// What the cleaner's country asks for, whether or not any of it is uploaded. This is the
    /// placeholder the screen shows before anything exists — it used to open on an empty box that
    /// named nothing, so the first step of onboarding was contacting support to ask.
    ///
    /// Sorted here rather than in the view: the server orders by `sortOrder` and a checklist in
    /// arbitrary order reads as arbitrary.
    @Published private(set) var requirements: [MyDocumentRequirementDto] = []

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

        // The checklist is advisory, so a failure here is silent: a cleaner who can see their own
        // documents should not be shown an error because the list of what we WANT did not load. It
        // falls back to the empty state the screen had before this section existed.
        if case let .success(rows) = await client.getDocumentRequirements() {
            requirements = rows.sorted { ($0.sortOrder ?? 0) < ($1.sortOrder ?? 0) }
        } else {
            requirements = []
        }
    }

    /// The file picker's size guard reports through here so the view keeps its
    /// single dependency (the view model) and never touches the snackbar itself.
    func showTooLarge() {
        snackbar.showError(L10n.Profile.documentTooLarge)
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

    /// Supersede a document with a newer file. No admin needed: the server creates the new version
    /// before retiring the old one, so the count never dips and the registration lock never
    /// re-engages. The document TYPE is not sent — the server carries it over from the old version.
    func replace(
        documentId: String,
        fileName: String,
        contentType: String,
        base64Content: String,
        description: String?
    ) async {
        guard !action.isSubmitting else { return }
        action = .submitting
        let file = BlobFileDto(
            fileName: fileName,
            base64Content: base64Content,
            contentType: contentType
        )
        switch await client.replaceDocument(
            documentId: documentId,
            file: file,
            description: description?.trimmedOrNil
        ) {
        case .success:
            action = .idle
            await load()
        case let .failure(error):
            action = .error(localizer.message(for: error))
            snackbar.showError(localizer.message(for: error))
        }
    }

    /// Ask an admin to remove a document. It removes NOTHING — which is the whole point. The button
    /// this replaced soft-deleted on the spot, and that flipped `AreDocumentsUploaded` and re-engaged
    /// the registration lock: one tap, no dialog, and a cleaner had lost their access to work.
    ///
    /// Confirmation is a SUCCESS message rather than a reload: the document is unchanged, so a list
    /// that looked different afterwards would be lying about what just happened.
    func requestDeletion(documentId: String, reason: String) async {
        guard busyDocumentId == nil else { return }
        busyDocumentId = documentId
        switch await client.requestDocumentDeletion(documentId: documentId, reason: reason) {
        case .success:
            busyDocumentId = nil
            snackbar.showSuccess(L10n.Profile.documentDeletionRequested)
        case let .failure(error):
            busyDocumentId = nil
            snackbar.showError(localizer.message(for: error))
        }
    }
}
