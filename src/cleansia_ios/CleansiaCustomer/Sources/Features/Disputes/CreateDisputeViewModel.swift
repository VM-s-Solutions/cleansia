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

    /// Files attached before the dispute exists, uploaded to it in order once the create is acknowledged.
    @Published private(set) var pickedEvidence: [PickedEvidence] = []

    private let repository: DisputeRepository
    private let orderClient: OrderClient
    private let snackbar: SnackbarController

    /// Set the moment the server acknowledges the create. A later submit — after an upload failed —
    /// resumes from here instead of filing a second dispute about the same money.
    private var createdId: String?

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

    func addEvidence(_ source: EvidenceSource, fileName: String) {
        guard !submitState.isSubmitting else { return }
        switch EvidencePreparer.prepare(source) {
        case let .success(file):
            pickedEvidence.append(PickedEvidence(id: UUID().uuidString, fileName: fileName, file: file))
        case let .failure(error):
            snackbar.showError(error.message)
        }
    }

    func removeEvidence(id: String) {
        guard !submitState.isSubmitting, let index = pickedEvidence.firstIndex(where: { $0.id == id }) else { return }
        pickedEvidence[index].file.cleanUp()
        pickedEvidence.remove(at: index)
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
        var disputeId = createdId
        if disputeId == nil {
            disputeId = await create(orderId: orderId, reason: reason, description: trimmed)
        }
        guard let id = disputeId else { return }
        await uploadPending(to: id)
        submitState = .idle
        created.send(id)
    }

    private func create(orderId: String, reason: Int, description: String) async -> String? {
        // Only rows still on the loaded order. Nothing can go stale here today — the order is fixed
        // by the route — but the filter keeps the invariant local rather than assumed.
        let lines = lineOptions.filter { pickedLineIds.contains($0.id) }
        switch await repository.create(
            orderId: orderId,
            reason: reason,
            description: description,
            lines: lines
        ) {
        case let .success(id):
            createdId = id
            await repository.refresh()
            return id
        case let .failure(error):
            snackbar.showApiError(error)
            submitState = .error(L10n.Disputes.createRetryHint)
            return nil
        }
    }

    /// One file at a time, in the order picked. A failure marks its own row and moves on: the dispute
    /// exists either way, and the customer is told which files to add again from its detail.
    private func uploadPending(to disputeId: String) async {
        var failedNames: [String] = []
        for index in pickedEvidence.indices where pickedEvidence[index].upload != .uploaded {
            pickedEvidence[index].upload = .uploading
            let file = pickedEvidence[index].file
            switch await repository.uploadEvidence(disputeId: disputeId, file: file.url) {
            case .success:
                pickedEvidence[index].upload = .uploaded
                file.cleanUp()
            case .failure:
                pickedEvidence[index].upload = .failed
                failedNames.append(pickedEvidence[index].fileName)
            }
        }
        if !failedNames.isEmpty {
            snackbar.showError(L10n.Disputes.createEvidencePartial(failedNames.joined(separator: ", ")))
        }
    }

    func clearError() {
        if case .error = submitState { submitState = .idle }
    }
}
