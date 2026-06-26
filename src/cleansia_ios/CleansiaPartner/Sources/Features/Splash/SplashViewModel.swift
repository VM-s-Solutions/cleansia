import CleansiaCore
import Foundation

@MainActor
final class SplashViewModel: ViewModel {
    @Published private(set) var outcome: SplashOutcome?

    private let hasValidSession: Bool
    private let settings: AppSettingsStore
    private let client: PartnerRegistrationClient

    init(hasValidSession: Bool, settings: AppSettingsStore, client: PartnerRegistrationClient) {
        self.hasValidSession = hasValidSession
        self.settings = settings
        self.client = client
    }

    func resolve() async {
        guard hasValidSession else {
            outcome = settings.hasSeenOnboarding ? .unauthenticated : .needsOnboarding
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
