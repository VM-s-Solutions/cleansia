import CleansiaCore
import Foundation

@MainActor
final class CustomerSplashViewModel: ViewModel {
    @Published private(set) var outcome: CustomerSplashOutcome?

    private let hasValidSession: Bool
    private let hold: () async -> Void

    /// The default hold (1.8s) gives the branded splash time to play its letter-by-letter reveal
    /// before the gate auto-advances — matching Android's SplashScreen.kt brand-hold pacing.
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
