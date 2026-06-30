import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class ProfileViewModelTests: XCTestCase {
    private var client: FakeUserProfileClient!
    private var repository: UserProfileRepository!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakeUserProfileClient()
        repository = UserProfileRepository(client: client)
        snackbar = SnackbarController()
    }

    private func makeVM() -> ProfileViewModel {
        ProfileViewModel(
            repository: repository,
            settings: UserDefaultsAppSettingsStore(defaults: scratchDefaults()),
            snackbar: snackbar
        )
    }

    private func scratchDefaults() -> UserDefaults {
        UserDefaults(suiteName: "test.\(UUID().uuidString)")!
    }

    func testRefreshLoadsCurrentUser() async {
        client.currentUserResult = .success(ProfileFixtures.user(firstName: "Ada"))
        let vm = makeVM()
        await vm.refresh()
        XCTAssertEqual(repository.currentUser?.firstName, "Ada")
        XCTAssertEqual(vm.refreshState, .idle)
    }

    func testSaveCallsUpdateAndEmitsSaved() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()

        var saved = false
        let token = vm.saved.sink { saved = true }
        defer { token.cancel() }

        await vm.save(
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: "+420999",
            birthDate: nil,
            languageCode: "cs"
        )

        XCTAssertEqual(client.lastUpdate?.firstName, "Grace")
        XCTAssertEqual(client.lastUpdate?.languageCode, "cs")
        XCTAssertTrue(saved)
        XCTAssertEqual(vm.saveState, .idle)
    }

    func testSaveTrimsWhitespace() async {
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.save(
            firstName: "  Grace  ",
            lastName: " Hopper ",
            phoneNumber: " +420 ",
            birthDate: nil,
            languageCode: nil
        )
        XCTAssertEqual(client.lastUpdate?.firstName, "Grace")
        XCTAssertEqual(client.lastUpdate?.lastName, "Hopper")
        XCTAssertEqual(client.lastUpdate?.phoneNumber, "+420")
    }

    func testSaveFailureSurfacesActionErrorAndDoesNotEmitSaved() async {
        client.updateResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM()

        var saved = false
        let token = vm.saved.sink { saved = true }
        defer { token.cancel() }

        await vm.save(firstName: "G", lastName: "H", phoneNumber: nil, birthDate: nil, languageCode: nil)

        XCTAssertFalse(saved)
        XCTAssertNotNil(snackbar.current)
        guard case .error = vm.saveState else { return XCTFail("expected action error") }
    }

    func testSaveReentryGuard() async {
        client.updateResult = .success(())
        let vm = makeVM()
        async let first: Void = vm.save(
            firstName: "A",
            lastName: "B",
            phoneNumber: nil,
            birthDate: nil,
            languageCode: nil
        )
        async let second: Void = vm.save(
            firstName: "C",
            lastName: "D",
            phoneNumber: nil,
            birthDate: nil,
            languageCode: nil
        )
        _ = await (first, second)
        XCTAssertEqual(client.updateCallCount, 1)
    }

    func testCompleteOnboardingSavesAndMarksSeen() async {
        client.currentUserResult = .success(ProfileFixtures.user())
        client.updateResult = .success(())
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)
        await vm.refresh()

        var completed = false
        let token = vm.saved.sink { completed = true }
        defer { token.cancel() }

        await vm.completeOnboarding(phoneNumber: "+420111", birthDate: nil)

        XCTAssertTrue(completed)
        XCTAssertTrue(settings.hasSeenOnboarding)
    }

    func testSkipOnboardingMarksSeen() {
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)
        vm.skipOnboarding()
        XCTAssertTrue(settings.hasSeenOnboarding)
    }
}
