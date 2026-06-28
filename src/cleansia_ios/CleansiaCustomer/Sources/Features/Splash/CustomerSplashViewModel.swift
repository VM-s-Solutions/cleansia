import CleansiaCore
import Foundation

@MainActor
final class CustomerSplashViewModel: ViewModel {
    @Published private(set) var outcome: CustomerSplashOutcome?

    private let hasValidSession: Bool

    init(hasValidSession: Bool) {
        self.hasValidSession = hasValidSession
    }

    func resolve() async {
        outcome = hasValidSession ? .authenticated : .unauthenticated
    }
}
