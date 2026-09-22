import CleansiaCore
import Combine
import Foundation

@MainActor
final class DisputeDetailViewModel: ViewModel {
    @Published private(set) var state: UiState<DisputeDetail> = .loading
    @Published private(set) var sendState: ActionState = .idle
    @Published private(set) var uploadState: ActionState = .idle

    let messageSent = PassthroughSubject<Void, Never>()
    let evidenceUploaded = PassthroughSubject<Void, Never>()

    private let disputeId: String
    private let repository: DisputeRepository
    private let snackbar: SnackbarController

    init(disputeId: String, repository: DisputeRepository, snackbar: SnackbarController) {
        self.disputeId = disputeId
        self.repository = repository
        self.snackbar = snackbar
    }

    func load() async {
        guard !disputeId.isBlank else {
            state = .error(ApiError(code: "dispute.missing_id"))
            return
        }
        state = .loading
        switch await repository.getById(disputeId) {
        case let .success(detail):
            state = .loaded(detail)
        case let .failure(error):
            snackbar.showApiError(error)
            state = .error(error)
        }
    }

    // MARK: - Reply

    func sendMessage(_ content: String) async {
        guard !disputeId.isBlank, !sendState.isSubmitting else { return }
        let trimmed = content.trimmingCharacters(in: .whitespacesAndNewlines)
        guard (1 ... DisputeFormConstants.messageMaxLength).contains(trimmed.count) else { return }
        sendState = .submitting
        switch await repository.addMessage(disputeId: disputeId, message: trimmed) {
        case .success:
            sendState = .idle
            messageSent.send()
            await reloadDetail()
        case let .failure(error):
            snackbar.showApiError(error)
            sendState = .error(L10n.Disputes.detailSendRetry)
        }
    }

    // MARK: - Evidence upload

    /// Uploads picked sources SEQUENTIALLY — one multipart call each, gated on the
    /// loaded disputeId. Per file: prepare (image → 1920/0.7 + EXIF/GPS strip →
    /// temp .jpg; PDF → temp .pdf), fail-closed validate AFTER compression, upload,
    /// then clean up the temp file (success or failure). The single global spinner
    /// stays Submitting across the batch (the `uploadEvidence` Android parity).
    func uploadEvidence(_ sources: [EvidenceSource]) async {
        guard !disputeId.isBlank, !sources.isEmpty, !uploadState.isSubmitting else { return }
        uploadState = .submitting
        var anySucceeded = false
        for source in sources {
            let succeeded = await uploadOne(source)
            anySucceeded = anySucceeded || succeeded
        }
        uploadState = .idle
        if anySucceeded {
            evidenceUploaded.send()
            await reloadDetail()
        }
    }

    private func uploadOne(_ source: EvidenceSource) async -> Bool {
        let prepared: PreparedEvidence
        switch EvidencePreparer.prepare(source) {
        case let .success(file):
            prepared = file
        case let .failure(error):
            snackbar.showError(error.message)
            return false
        }
        defer { prepared.cleanUp() }
        switch await repository.uploadEvidence(disputeId: disputeId, file: prepared.url) {
        case .success:
            return true
        case let .failure(error):
            snackbar.showApiError(error)
            return false
        }
    }

    private func reloadDetail() async {
        if case let .success(detail) = await repository.getById(disputeId) {
            state = .loaded(detail)
        }
    }
}
