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

    func testSaveCarriesCurrentUserIdAndPickedBirthDate() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-42"))
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()

        let birthDate = Date(timeIntervalSince1970: 641_520_000)
        await vm.save(
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: nil,
            birthDate: birthDate,
            languageCode: nil
        )

        XCTAssertEqual(client.lastUpdate?.id, "user-42")
        XCTAssertEqual(client.lastUpdate?.birthDate, birthDate)
    }

    func testSaveWithoutLoadedUserFailsWithoutCallingUpdate() async {
        client.updateResult = .success(())
        let vm = makeVM()

        var saved = false
        let token = vm.saved.sink { saved = true }
        defer { token.cancel() }

        await vm.save(firstName: "G", lastName: "H", phoneNumber: nil, birthDate: nil, languageCode: nil)

        XCTAssertEqual(client.updateCallCount, 0)
        XCTAssertFalse(saved)
        XCTAssertNotNil(snackbar.current)
        guard case .error = vm.saveState else { return XCTFail("expected action error") }
    }

    func testSaveTrimsWhitespace() async {
        client.updateResult = .success(())
        let vm = makeVM()
        await vm.refresh()
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
        await vm.refresh()

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
        await vm.refresh()
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

    func testCompleteOnboardingSavesAndMarksSeenForTheUser() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-7", phoneNumber: nil))
        client.updateResult = .success(())
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults(), localeLanguageCode: { "cs" })
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)
        await vm.refresh()

        var completed = false
        let token = vm.saved.sink { completed = true }
        defer { token.cancel() }

        await vm.completeOnboarding(phoneNumber: " +420111 ", birthDate: nil)

        XCTAssertTrue(completed)
        XCTAssertTrue(settings.hasSeenOnboarding(userId: "user-7"))
        XCTAssertEqual(client.lastUpdate?.id, "user-7")
        XCTAssertEqual(client.lastUpdate?.phoneNumber, "+420111")
        XCTAssertEqual(client.lastUpdate?.firstName, "Jane")
        XCTAssertEqual(client.lastUpdate?.lastName, "Doe")
    }

    func testCompleteOnboardingSendsTheResolvedAppLanguage() async {
        client.currentUserResult = .success(ProfileFixtures.user(phoneNumber: nil))
        client.updateResult = .success(())
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults(), localeLanguageCode: { "uk" })
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)
        await vm.refresh()

        await vm.completeOnboarding(phoneNumber: "+420111", birthDate: nil)

        XCTAssertEqual(client.lastUpdate?.languageCode, "uk")
    }

    func testCompleteOnboardingFailureLeavesTheGateUnseen() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-7", phoneNumber: nil))
        client.updateResult = .failure(ApiError(httpStatus: 400))
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)
        await vm.refresh()

        var completed = false
        let token = vm.saved.sink { completed = true }
        defer { token.cancel() }

        await vm.completeOnboarding(phoneNumber: "+420111", birthDate: nil)

        XCTAssertFalse(completed)
        XCTAssertFalse(settings.hasSeenOnboarding(userId: "user-7"))
        XCTAssertNotNil(snackbar.current)
        guard case .error = vm.saveState else { return XCTFail("expected action error") }
    }

    func testSkipOnboardingMarksSeenForTheLoadedUser() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-7"))
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)
        await vm.refresh()

        vm.skipOnboarding()

        XCTAssertTrue(settings.hasSeenOnboarding(userId: "user-7"))
        XCTAssertFalse(settings.hasSeenOnboarding(userId: "someone-else"))
    }

    func testSkipOnboardingWithoutLoadedUserMarksNothing() {
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)

        vm.skipOnboarding()

        XCTAssertFalse(settings.hasSeenOnboarding(userId: "user-1"))
    }

    func testNeedsOnboardingTriggersOnIncompleteUnseenProfileAfterAForcedRefresh() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-7", phoneNumber: nil))
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)

        let needed = await vm.needsOnboarding()

        XCTAssertTrue(needed)
        XCTAssertEqual(client.currentUserCallCount, 1)
    }

    func testNeedsOnboardingFalseWhenProfileComplete() async {
        client.currentUserResult = .success(ProfileFixtures.user(phoneNumber: "+420123"))
        let vm = makeVM()

        let needed = await vm.needsOnboarding()

        XCTAssertFalse(needed)
    }

    func testNeedsOnboardingFalseOnceSeenForThatUserButNotForAnother() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-7", phoneNumber: nil))
        let settings = UserDefaultsAppSettingsStore(defaults: scratchDefaults())
        settings.markOnboardingSeen(userId: "user-7")
        let vm = ProfileViewModel(repository: repository, settings: settings, snackbar: snackbar)

        let neededForSeenUser = await vm.needsOnboarding()
        XCTAssertFalse(neededForSeenUser)

        client.currentUserResult = .success(ProfileFixtures.user(id: "user-8", phoneNumber: nil))
        let neededForNewUser = await vm.needsOnboarding()
        XCTAssertTrue(neededForNewUser)
    }

    func testNeedsOnboardingFalseWhenTheProfileNeverLoads() async {
        client.currentUserResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()

        let needed = await vm.needsOnboarding()

        XCTAssertFalse(needed)
    }

    func testNeedsOnboardingUsesTheCachedUserWhenTheForcedRefreshFails() async {
        client.currentUserResult = .success(ProfileFixtures.user(id: "user-7", phoneNumber: nil))
        let vm = makeVM()
        await vm.refresh()
        client.currentUserResult = .failure(ApiError(httpStatus: 500))

        let needed = await vm.needsOnboarding()

        XCTAssertTrue(needed)
    }
}
