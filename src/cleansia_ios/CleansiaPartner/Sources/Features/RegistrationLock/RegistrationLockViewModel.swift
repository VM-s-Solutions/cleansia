import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

struct RegistrationLockData: Equatable {
    let steps: [RegistrationStep]
    let errorMessage: String?
    let isComplete: Bool

    var completedCount: Int {
        steps.filter { $0.status == .done }.count
    }

    var totalCount: Int {
        steps.count
    }
}

@MainActor
final class RegistrationLockViewModel: ViewModel {
    @Published private(set) var state: UiState<RegistrationLockData> = .loading
    @Published private(set) var action: ActionState = .idle

    let completed = PassthroughSubject<Void, Never>()
    let signedOut = PassthroughSubject<Void, Never>()

    private let client: PartnerRegistrationClient
    private let authClient: AuthClient
    private let legalDocumentsClient: PartnerProfileClient
    private let languageTag: () -> String
    private let localizer = ApiErrorLocalizer()

    private var lastStatus: RegistrationCompletionStatus?
    /// An admin cannot approve a cleaner who has not accepted the contract documents in force, so they
    /// are a step of their own. Nil until first read.
    private var lastLegalDocuments: [CleanerLegalDocument]?

    var missingFields: [String] {
        lastStatus?.missingFields ?? []
    }

    init(
        client: PartnerRegistrationClient,
        authClient: AuthClient,
        legalDocumentsClient: PartnerProfileClient,
        languageTag: @escaping () -> String = { CoreL10n.languageTag }
    ) {
        self.client = client
        self.authClient = authClient
        self.legalDocumentsClient = legalDocumentsClient
        self.languageTag = languageTag
    }

    func load() async {
        var documentsError: String?
        switch await legalDocumentsClient.getLegalDocuments(language: languageTag()) {
        case let .success(documents):
            lastLegalDocuments = documents
        case let .failure(error):
            documentsError = localizer.message(for: error)
        }
        switch await client.checkRegistrationStatus() {
        case let .success(status):
            lastStatus = status
            let complete = isRegistrationComplete(status)
            state = .loaded(RegistrationLockData(
                steps: buildSteps(status, legalDocuments: lastLegalDocuments),
                errorMessage: documentsError,
                isComplete: complete
            ))
            if complete { completed.send() }
        case let .failure(error):
            state = .loaded(RegistrationLockData(
                steps: buildSteps(lastStatus, legalDocuments: lastLegalDocuments),
                errorMessage: localizer.message(for: error),
                isComplete: false
            ))
        }
    }

    func signOut() async {
        if action.isSubmitting { return }
        action = .submitting
        await authClient.logout()
        action = .idle
        signedOut.send()
    }
}
