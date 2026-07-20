import CleansiaCore
import Foundation

@MainActor
final class SplashViewModel: ViewModel {
    @Published private(set) var outcome: SplashOutcome?

    private let hasValidSession: Bool
    private let settings: AppSettingsStore
    private let client: PartnerRegistrationClient
    private let hold: () async -> Void

    /// The default hold gives the branded splash time to play its letter-by-letter reveal (~1.2s)
    /// before the gate resolves and `PartnerRootView` swaps in the next screen — otherwise the
    /// no-session path resolves synchronously and the reveal is torn down to a flash. Mirrors
    /// `CustomerSplashViewModel`; tests inject a no-op hold.
    init(
        hasValidSession: Bool,
        settings: AppSettingsStore,
        client: PartnerRegistrationClient,
        hold: @escaping () async -> Void = { try? await Task.sleep(nanoseconds: 1_800_000_000) }
    ) {
        self.hasValidSession = hasValidSession
        self.settings = settings
        self.client = client
        self.hold = hold
    }

    func resolve() async {
        await hold()

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
