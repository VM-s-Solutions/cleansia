import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// Backs the Notes & Issues section: add / edit / delete notes and issues over
/// the order client. The notes/issues LIST is owned by the parent
/// `OrderDetailViewModel` (read from `OrderDetail.orderNotes`/`orderIssues`);
/// this VM only pushes mutations through, then signals the parent to re-fetch
/// (the `mutationVersion`/`onMutated` parity). Per-row `mutatingId` drives the
/// in-flight spinner. Author-only edit/delete is a CLIENT UI gate via
/// `currentEmployeeId`; the backend enforces author-scoping.
@MainActor
final class OrderNotesViewModel: ViewModel {
    @Published private(set) var isSavingNote = false
    @Published private(set) var isReportingIssue = false
    /// noteId or issueId currently being mutated (edit / delete).
    @Published private(set) var mutatingId: String?
    /// The author-gate id; nil until resolved (then no edit/delete is offered).
    @Published private(set) var currentEmployeeId: String?

    /// Fires once per successful mutation so the parent re-fetches the order and
    /// re-renders the notes/issues list. The VM never navigates.
    let mutated = PassthroughSubject<Void, Never>()

    private let orderId: String
    private let client: PartnerOrderClient
    private let snackbar: SnackbarController

    init(orderId: String, client: PartnerOrderClient, snackbar: SnackbarController) {
        self.orderId = orderId
        self.client = client
        self.snackbar = snackbar
    }

    func resolveCurrentEmployeeId() async {
        guard currentEmployeeId == nil else { return }
        if case let .success(id) = await client.currentEmployeeId() {
            currentEmployeeId = id
        }
    }

    func isAuthor(noteEmployeeId: String?) -> Bool {
        guard let currentEmployeeId, let noteEmployeeId, !noteEmployeeId.isEmpty else { return false }
        return noteEmployeeId == currentEmployeeId
    }

    // MARK: Notes

    func addNote(_ content: String) async {
        let trimmed = content.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !isSavingNote else { return }
        isSavingNote = true
        let result = await client.addNote(orderId: orderId, content: trimmed)
        isSavingNote = false
        finish(result, successKey: L10n.Orders.noteSavedToast)
    }

    func updateNote(_ noteId: String, _ content: String) async {
        let trimmed = content.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, mutatingId == nil else { return }
        mutatingId = noteId
        let result = await client.updateNote(orderId: orderId, noteId: noteId, content: trimmed)
        mutatingId = nil
        finish(result, successKey: L10n.Orders.noteSavedToast)
    }

    func deleteNote(_ noteId: String) async {
        guard mutatingId == nil else { return }
        mutatingId = noteId
        let result = await client.deleteNote(orderId: orderId, noteId: noteId)
        mutatingId = nil
        finish(result, successKey: L10n.Orders.noteDeletedToast)
    }

    // MARK: Issues

    func reportIssue(_ description: String) async {
        let trimmed = description.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, !isReportingIssue else { return }
        isReportingIssue = true
        let result = await client.reportIssue(orderId: orderId, description: trimmed)
        isReportingIssue = false
        finish(result, successKey: L10n.Orders.issueReportedToast)
    }

    func updateIssue(_ issueId: String, _ description: String) async {
        let trimmed = description.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, mutatingId == nil else { return }
        mutatingId = issueId
        let result = await client.updateIssue(orderId: orderId, issueId: issueId, description: trimmed)
        mutatingId = nil
        finish(result, successKey: L10n.Orders.issueUpdatedToast)
    }

    func deleteIssue(_ issueId: String) async {
        guard mutatingId == nil else { return }
        mutatingId = issueId
        let result = await client.deleteIssue(orderId: orderId, issueId: issueId)
        mutatingId = nil
        finish(result, successKey: L10n.Orders.issueDeletedToast)
    }

    private func finish(_ result: ApiResult<Void>, successKey: String) {
        switch result {
        case .success:
            snackbar.showSuccess(successKey)
            mutated.send()
        case let .failure(error):
            snackbar.showApiError(error)
        }
    }
}

#if DEBUG
    extension OrderNotesViewModel {
        static var preview: OrderNotesViewModel {
            OrderNotesViewModel(orderId: "preview", client: PreviewOrderClient(), snackbar: SnackbarController())
        }
    }

    private final class PreviewOrderClient: PartnerOrderClient {
        func currentEmployeeId() async -> ApiResult<String> {
            .success("preview")
        }

        func getPaged(_: OrderPageQuery) async -> ApiResult<[CleansiaPartnerApi.OrderListItem]> {
            .success([])
        }

        func getById(orderId _: String) async -> ApiResult<CleansiaPartnerApi.OrderItem> {
            .success(CleansiaPartnerApi.OrderItem())
        }

        func takeOrder(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func notifyOnTheWay(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func startOrder(orderId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func completeOrder(orderId _: String, actualMinutes _: Int?, notes _: String?) async -> ApiResult<Void> {
            .success(())
        }

        func addNote(orderId _: String, content _: String) async -> ApiResult<Void> {
            .success(())
        }

        func updateNote(orderId _: String, noteId _: String, content _: String) async -> ApiResult<Void> {
            .success(())
        }

        func deleteNote(orderId _: String, noteId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func reportIssue(orderId _: String, description _: String) async -> ApiResult<Void> {
            .success(())
        }

        func updateIssue(orderId _: String, issueId _: String, description _: String) async -> ApiResult<Void> {
            .success(())
        }

        func deleteIssue(orderId _: String, issueId _: String) async -> ApiResult<Void> {
            .success(())
        }

        func getPhotos(orderId _: String) async -> ApiResult<[OrderPhoto]> {
            .success([])
        }

        func savePhoto(
            orderId _: String,
            photoType _: PhotoType,
            base64Content _: String,
            fileName _: String,
            contentType _: String
        ) async -> ApiResult<Void> {
            .success(())
        }

        func deletePhoto(photoId _: String) async -> ApiResult<Void> {
            .success(())
        }
    }
#endif
