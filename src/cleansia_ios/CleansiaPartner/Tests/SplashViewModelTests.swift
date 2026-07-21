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

    func testEmptyStatusResolvesRegistrationLockFailClosed() async {
        let client = FakeRegistrationClient()
        client.result = .success(RegistrationCompletionStatus())
        let vm = makeViewModel(hasValidSession: true, client: client)

        await vm.resolve()

        XCTAssertEqual(vm.outcome, .needsRegistrationLock)
    }
}
