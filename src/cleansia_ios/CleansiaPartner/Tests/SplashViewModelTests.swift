import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class SplashViewModelTests: XCTestCase {
    private final class FakeRegistrationClient: PartnerRegistrationClient {
        var result: ApiResult<RegistrationCompletionStatus> = .success(RegistrationCompletionStatus())
        private(set) var callCount = 0

        func checkRegistrationStatus() async -> ApiResult<RegistrationCompletionStatus> {
            callCount += 1
            return result
        }
    }

    private final class FakeSettings: AppSettingsStore {
        private(set) var answeredPrompts: Set<String> = []
        func hasAnsweredPrompt(_ prompt: String, userId: String) -> Bool {
            answeredPrompts.contains("\(prompt)/\(userId)")
        }

        func markPromptAnswered(_ prompt: String, userId: String) {
            answeredPrompts.insert("\(prompt)/\(userId)")
        }

        var hasSeenOnboarding: Bool
        init(hasSeenOnboarding: Bool) {
            self.hasSeenOnboarding = hasSeenOnboarding
        }

        func markOnboardingSeen() {
            hasSeenOnboarding = true
        }

        var languageTag = "en"

        var persistedLanguageTag: String?

        func setLanguage(_ tag: String) {
            languageTag = tag
            persistedLanguageTag = tag
        }

        func clearLanguage() {
            persistedLanguageTag = nil
        }

        var theme: Theme = .system
        func setTheme(_ theme: Theme) {
            self.theme = theme
        }
    }

    private func makeViewModel(
        hasValidSession: Bool,
        client: FakeRegistrationClient,
        hasSeenOnboarding: Bool = true
    ) -> SplashViewModel {
        SplashViewModel(
            hasValidSession: hasValidSession,
            settings: FakeSettings(hasSeenOnboarding: hasSeenOnboarding),
            client: client,
            hold: {}
        )
    }

    private func completeStatus() -> RegistrationCompletionStatus {
        RegistrationCompletionStatus(
            areDocumentsUploaded: true,
            hasCompletedProfile: true,
            contractStatus: .approved
        )
    }

    func testInitialOutcomeIsNil() {
        let vm = makeViewModel(hasValidSession: true, client: FakeRegistrationClient())
        XCTAssertNil(vm.outcome)
    }

    func testNoSessionWithOnboardingSeenResolvesUnauthenticatedWithoutCallingClient() async {
        let client = FakeRegistrationClient()
        let vm = makeViewModel(hasValidSession: false, client: client, hasSeenOnboarding: true)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .unauthenticated)
        XCTAssertEqual(client.callCount, 0)
    }

    func testNoSessionWithoutOnboardingSeenResolvesNeedsOnboardingWithoutCallingClient() async {
        let client = FakeRegistrationClient()
        let vm = makeViewModel(hasValidSession: false, client: client, hasSeenOnboarding: false)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .needsOnboarding)
        XCTAssertEqual(client.callCount, 0)
    }

    func testSessionWithCompleteStatusResolvesAuthenticated() async {
        let client = FakeRegistrationClient()
        client.result = .success(completeStatus())
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .authenticated)
    }

    func testSessionWithIncompleteStatusResolvesRegistrationLock() async {
        let client = FakeRegistrationClient()
        client.result = .success(RegistrationCompletionStatus(
            areDocumentsUploaded: false,
            hasCompletedProfile: true,
            contractStatus: .approved
        ))
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .needsRegistrationLock)
    }

    func testSessionWithFailureResolvesRegistrationLockFailClosed() async {
        let client = FakeRegistrationClient()
        client.result = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .needsRegistrationLock)
    }

    /// The distinction the whole change exists for. A transport failure is "we could not ask",
    /// which is nothing like "you are not approved" — and the lock screen it used to show has no
    /// exit but signing out.
    func testTransportFailureResolvesUnreachableRatherThanTheLock() async {
        let client = FakeRegistrationClient()
        client.result = .failure(ApiError(code: "network.unreachable", message: "offline"))
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .unreachable)
    }

    /// A cancellation is not a failure the cleaner should see anything about — they navigated away.
    /// Showing them a retry screen for it would be noise, so it keeps the fail-closed destination.
    func testACancellationIsNotTreatedAsUnreachable() async {
        let client = FakeRegistrationClient()
        client.result = .failure(ApiError(code: ApiError.cancelledCode))
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertNotEqual(vm.outcome, .unreachable)
    }

    /// Retry has to actually re-ask. The old cold-flow shape could only be re-run by rebuilding the
    /// whole view identity, which a button cannot do.
    func testResolveCanBeRunAgainAndAsksAgain() async {
        let client = FakeRegistrationClient()
        client.result = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()
        XCTAssertEqual(vm.outcome, .unreachable)

        client.result = .success(RegistrationCompletionStatus(
            areDocumentsUploaded: true,
            hasCompletedProfile: true,
            contractStatus: .approved
        ))
        await vm.resolve()

        XCTAssertEqual(vm.outcome, .authenticated)
        XCTAssertEqual(client.callCount, 2)
    }

    func testEmptyStatusResolvesRegistrationLockFailClosed() async {
        let client = FakeRegistrationClient()
        client.result = .success(RegistrationCompletionStatus())
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .needsRegistrationLock)
    }
}
