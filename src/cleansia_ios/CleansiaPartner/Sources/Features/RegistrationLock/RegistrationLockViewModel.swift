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
    private let localizer = ApiErrorLocalizer()

    private var lastStatus: RegistrationCompletionStatus?

    var missingFields: [String] {
        lastStatus?.missingFields ?? []
    }

    init(client: PartnerRegistrationClient, authClient: AuthClient) {
        self.client = client
        self.authClient = authClient
    }

    func load() async {
        switch await client.checkRegistrationStatus() {
        case let .success(status):
            lastStatus = status
            let complete = isRegistrationComplete(status)
            state = .loaded(RegistrationLockData(
                steps: buildSteps(status),
                errorMessage: nil,
                isComplete: complete
            ))
            if complete { completed.send() }
        case let .failure(error):
            state = .loaded(RegistrationLockData(
                steps: buildSteps(lastStatus),
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
