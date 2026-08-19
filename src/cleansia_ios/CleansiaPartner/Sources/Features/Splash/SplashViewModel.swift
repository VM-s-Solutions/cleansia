import CleansiaCore
import Foundation

@MainActor
final class SplashViewModel: ViewModel {
    @Published private(set) var outcome: SplashOutcome?

    private let hasValidSession: Bool
    private let settings: AppSettingsStore
    private let client: PartnerRegistrationClient
    private let hold: () async -> Void
    private var hasResolvedOnce = false

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
        // The hold exists to let the branded reveal play on a cold start. A RETRY is not a cold
        // start — the reveal already played — so it would be 1.8s of staring at a splash the user
        // has just asked to leave.
        if !hasResolvedOnce {
            await hold()
            hasResolvedOnce = true
        }

        outcome = nil

        guard hasValidSession else {
            outcome = settings.hasSeenOnboarding ? .unauthenticated : .needsOnboarding
            return
        }

        switch await client.checkRegistrationStatus() {
        case let .success(status):
            outcome = isRegistrationComplete(status) ? .authenticated : .needsRegistrationLock
        case let .failure(error):
            // Only a TRANSPORT failure is `unreachable`. A 4xx or 5xx is the backend answering,
            // and "we asked and were refused" sits far closer to "not approved" than to "we could
            // not ask" — routing a 403 to a retry screen would loop a cleaner forever on a state
            // no retry can change. `httpStatus` is nil exactly when ApiError came from a URLError
            // rather than fromProblemDetails; a cancellation is neither, and must not show an
            // error to someone who navigated away.
            let isTransport = error.httpStatus == nil
                && error.code?.hasPrefix("network.") == true
                && !error.isCancellation
            outcome = isTransport ? .unreachable : .needsRegistrationLock
        }
    }
}
