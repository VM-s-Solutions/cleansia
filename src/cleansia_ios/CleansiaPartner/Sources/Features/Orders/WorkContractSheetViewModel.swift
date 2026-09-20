import CleansiaCore
import Combine
import Foundation

/// What the sheet is opened for. The take and the standalone acceptance swipe; a read only reads.
enum WorkContractRequest: Equatable, Identifiable {
    case take(orderId: String)
    case accept(orderId: String)
    case read(acceptanceId: String)

    var id: String {
        switch self {
        case let .take(orderId): "take:\(orderId)"
        case let .accept(orderId): "accept:\(orderId)"
        case let .read(acceptanceId): "read:\(acceptanceId)"
        }
    }
}

enum WorkContractSheetState {
    case loading
    case error(ApiError)
    /// The order has no contract text in force, so it cannot be taken now; nothing to accept.
    case unavailable(ApiError)
    case loaded(WorkContract)

    var loadedContract: WorkContract? {
        if case let .loaded(contract) = self { return contract }
        return nil
    }
}

enum WorkContractNotice: Equatable {
    case textUpdated

    /// The notice is the refusal's own sentence: the mismatch is the one server verdict the sheet
    /// answers itself instead of handing to its host.
    var message: String {
        switch self {
        case .textUpdated: ApiErrorLocalizer().message(for: ApiError(code: WorkContractErrorKey.textMismatch))
        }
    }
}

/// How the sheet ended for its host. The host — the board, the detail or the offers list — reacts to
/// the take exactly as it did when the take was its own one tap: a success refreshes its panes, a
/// refusal is framed and reconciled in its own words. The two contract keys never leave the sheet.
enum WorkContractOutcome: Equatable {
    case taken(orderId: String)
    case accepted(orderId: String)
    case refused(WorkContractRequest, ApiError)

    var request: WorkContractRequest {
        switch self {
        case let .taken(orderId): .take(orderId: orderId)
        case let .accepted(orderId): .accept(orderId: orderId)
        case let .refused(request, _): request
        }
    }

    var result: ApiResult<Void> {
        switch self {
        case .taken, .accepted: .success(())
        case let .refused(_, error): .failure(error)
        }
    }
}

enum WorkContractErrorKey {
    static let notAccepted = "contract.not_accepted"
    static let textMismatch = "contract.text_mismatch"
    static let acceptanceRequired = "contract.acceptance_required"
    static let documentNotFound = "legal.document_not_found"
}

/// The contract sheet: loads the preview (or the accepted contract), and on the swipe echoes the
/// previewed text row to the take or the standalone acceptance. Built per presentation, so every open
/// loads afresh — the preview is the server's word at that moment, never a cached one.
///
/// `contract.text_mismatch` is the one refusal handled here: the echoed text is not this order's, so
/// the preview is re-run, the gesture reset and the cleaner told to read again. `legal.document_not_found`
/// can only come from the preview and means the job cannot be taken now. Every other refusal is the
/// host's to frame.
@MainActor
final class WorkContractSheetViewModel: ViewModel {
    @Published private(set) var state: WorkContractSheetState = .loading
    @Published private(set) var actionState: ActionState = .idle
    @Published private(set) var notice: WorkContractNotice?

    let outcome = PassthroughSubject<WorkContractOutcome, Never>()

    let request: WorkContractRequest
    private let client: PartnerOrderClient
    private let languageTag: () -> String

    init(
        request: WorkContractRequest,
        client: PartnerOrderClient,
        languageTag: @escaping () -> String = { CoreL10n.languageTag }
    ) {
        self.request = request
        self.client = client
        self.languageTag = languageTag
    }

    var hasGesture: Bool {
        switch request {
        case .take, .accept: true
        case .read: false
        }
    }

    func load() async {
        state = .loading
        let language = languageTag()
        let result: ApiResult<WorkContract> = switch request {
        case let .take(orderId), let .accept(orderId):
            await client.getWorkContractPreview(orderId: orderId, language: language)
        case let .read(acceptanceId):
            await client.getWorkContract(acceptanceId: acceptanceId, language: language)
        }
        switch result {
        case let .success(contract):
            state = .loaded(contract)
        case let .failure(error):
            state = error.code == WorkContractErrorKey.documentNotFound ? .unavailable(error) : .error(error)
        }
    }

    /// The swipe. Echoes the text row on screen; a read has nothing to swipe.
    func accept() async {
        guard let contract = state.loadedContract, !actionState.isSubmitting else { return }
        let textId = contract.legalDocumentTextId
        let submitted: WorkContractOutcome
        switch request {
        case let .take(orderId):
            actionState = .submitting
            submitted = await client.takeOrder(orderId: orderId, acceptedWorkContractTextId: textId)
                .outcome(request: request) { .taken(orderId: orderId) }
        case let .accept(orderId):
            actionState = .submitting
            submitted = await client.acceptWorkContract(orderId: orderId, acceptedWorkContractTextId: textId)
                .outcome(request: request) { .accepted(orderId: orderId) }
        case .read:
            return
        }
        actionState = .idle
        if case let .refused(_, error) = submitted, error.code == WorkContractErrorKey.textMismatch {
            notice = .textUpdated
            await load()
            return
        }
        outcome.send(submitted)
    }
}

private extension Result where Success == Void, Failure == ApiError {
    func outcome(request: WorkContractRequest, success: () -> WorkContractOutcome) -> WorkContractOutcome {
        switch self {
        case .success: success()
        case let .failure(error): .refused(request, error)
        }
    }
}
