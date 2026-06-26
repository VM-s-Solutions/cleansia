import CleansiaCore
import Foundation

@MainActor
final class SplashViewModel: ViewModel {
    @Published private(set) var outcome: SplashOutcome?

    private let hasValidSession: Bool
    private let client: PartnerRegistrationClient

    init(hasValidSession: Bool, client: PartnerRegistrationClient) {
        self.hasValidSession = hasValidSession
        self.client = client
    }

    func resolve() async {
        guard hasValidSession else {
            outcome = .unauthenticated
            return
        }

        switch await client.checkRegistrationStatus() {
        case let .success(status):
            outcome = isRegistrationComplete(status) ? .authenticated : .needsRegistrationLock
        case .failure:
            outcome = .needsRegistrationLock
        }
    }
}
