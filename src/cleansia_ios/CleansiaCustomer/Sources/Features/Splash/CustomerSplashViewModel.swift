import CleansiaCore
import Foundation

@MainActor
final class CustomerSplashViewModel: ViewModel {
    @Published private(set) var outcome: CustomerSplashOutcome?

    private let hasValidSession: Bool
    private let hold: () async -> Void

    /// The default hold mirrors Android's SplashScreen.kt pacing: a 600ms
    /// fade-in plus a 1200ms brand hold before auto-advancing.
    init(
        hasValidSession: Bool,
        hold: @escaping () async -> Void = { try? await Task.sleep(nanoseconds: 1_800_000_000) }
    ) {
        self.hasValidSession = hasValidSession
        self.hold = hold
    }

    func resolve() async {
        await hold()
        outcome = hasValidSession ? .authenticated : .unauthenticated
    }
}
