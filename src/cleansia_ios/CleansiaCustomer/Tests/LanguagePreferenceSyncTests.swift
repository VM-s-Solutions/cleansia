import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

@MainActor
final class LanguagePreferenceSyncTests: XCTestCase {
    // MARK: - The pure decision

    func testBuildsAFullProfileReplayCarryingTheNewLanguage() throws {
        let update = try XCTUnwrap(LanguagePreferencePush.update(for: profile(language: "en"), languageCode: "uk"))

        XCTAssertEqual(update.languageCode, "uk")
        XCTAssertEqual(update.id, "user-1")
        XCTAssertEqual(update.firstName, "Olena")
        XCTAssertEqual(update.lastName, "Kovalenko")
        XCTAssertEqual(update.phoneNumber, "+420777111222")
        XCTAssertEqual(update.birthDate, Date(timeIntervalSince1970: 400_000_000))
        XCTAssertFalse(update.removePhoto)
        XCTAssertNil(update.photo)
    }

    func testNoPushWhenTheServerAlreadyHoldsThatLanguage() {
        XCTAssertNil(LanguagePreferencePush.update(for: profile(language: "uk"), languageCode: "uk"))
    }

    /// `UpdateCurrentUser` replaces first and last name outright and its validators reject blanks, so
    /// replaying a profile without them would either 400 or overwrite good data with nothing.
    func testNoPushWhenANameIsMissing() {
        XCTAssertNil(LanguagePreferencePush.update(for: profile(firstName: ""), languageCode: "uk"))
        XCTAssertNil(LanguagePreferencePush.update(for: profile(lastName: " "), languageCode: "uk"))
    }

    /// The customer an Apple or Google sign-up leaves without a phone is the one the gate used to strand
    /// on the server's 'en' stamp. The phone is not the server's to require here: a blank keeps it.
    func testAProfileWithoutAPhoneStillPushes() throws {
        for phone in [nil, ""] {
            let update = try XCTUnwrap(LanguagePreferencePush.update(for: profile(phone: phone), languageCode: "uk"))
            XCTAssertEqual(update.languageCode, "uk")
            XCTAssertEqual(UpdateCurrentUserCommand(update).phoneNumber, "", "the push must not omit the phone")
        }
    }

    func testPushesWhenTheServerHasNoLanguageYet() {
        XCTAssertNotNil(LanguagePreferencePush.update(for: profile(language: nil), languageCode: "cs"))
    }

    // MARK: - The live sync

    func testLiveSyncSendsTheReplayAndRefreshesTheCache() async {
        let client = FakeUserProfileClient()
        client.currentUserResult = .success(profile(language: "en"))
        let repository = UserProfileRepository(client: client)
        await repository.refresh()
        let sync = LiveLanguagePreferenceSync(repository: repository)

        await sync.send(languageCode: "uk")

        XCTAssertEqual(client.updateCallCount, 1)
        XCTAssertEqual(client.lastUpdate?.languageCode, "uk")
        XCTAssertEqual(client.lastUpdate?.firstName, "Olena")
    }

    func testLiveSyncSendsNothingWhenSignedOut() async {
        let client = FakeUserProfileClient()
        let sync = LiveLanguagePreferenceSync(repository: UserProfileRepository(client: client))

        await sync.send(languageCode: "uk")

        XCTAssertEqual(client.updateCallCount, 0)
    }

    func testLiveSyncSendsNothingWhenNothingChanged() async {
        let client = FakeUserProfileClient()
        client.currentUserResult = .success(profile(language: "uk"))
        let repository = UserProfileRepository(client: client)
        await repository.refresh()
        let sync = LiveLanguagePreferenceSync(repository: repository)

        await sync.send(languageCode: "uk")

        XCTAssertEqual(client.updateCallCount, 0)
    }

    // MARK: - The model wiring

    func testChoosingALanguageSyncsIt() async {
        let (model, sync) = makeModel()
        let sent = sync.expect(1)

        model.setLanguage("uk")

        await fulfillment(of: [sent], timeout: 2)
        XCTAssertEqual(sync.sent, ["uk"])
    }

    func testFollowingTheSystemSyncsTheResolvedTagNotTheSentinel() async {
        let (model, sync) = makeModel()
        let sent = sync.expect(2)

        model.setLanguage("uk")
        model.setSystemLanguage()

        await fulfillment(of: [sent], timeout: 2)
        XCTAssertEqual(sync.sent.last, model.languageTag)
        XCTAssertFalse(sync.sent.contains(CustomerPreferencesLabels.systemLanguageId))
    }

    /// The Notification Service Extension and the Live Activity render in whatever the app leaves in its
    /// App Group, so the app leaves the RESOLVED tag there at launch and on every change.
    func testTheResolvedLanguageIsLeftForTheExtensionsAtLaunchAndOnEveryChange() {
        let group = "LanguagePreferenceSyncTests.group.\(UUID().uuidString)"
        defer { UserDefaults().removePersistentDomain(forName: group) }
        let settings = UserDefaultsAppSettingsStore(
            defaults: UserDefaults(suiteName: UUID().uuidString) ?? .standard,
            preferredLanguageTags: { ["sk"] }
        )
        settings.setLanguage("cs")

        let model = CustomerPreferencesModel(settings: settings, languageSync: SpyLanguageSync(), appGroup: group)
        XCTAssertEqual(AppGroupLanguage.read(appGroup: group), "cs")

        model.setLanguage("uk")
        XCTAssertEqual(AppGroupLanguage.read(appGroup: group), "uk")

        model.setSystemLanguage()
        XCTAssertEqual(AppGroupLanguage.read(appGroup: group), "sk", "the resolved tag, never the System sentinel")
    }

    func testTheLocalChoiceIsAppliedBeforeAndIndependentlyOfTheSync() {
        let (model, _) = makeModel()

        model.setLanguage("cs")

        XCTAssertEqual(model.languageTag, "cs")
        XCTAssertFalse(model.isFollowingSystemLanguage)
    }

    // MARK: - Support

    private func makeModel() -> (CustomerPreferencesModel, SpyLanguageSync) {
        let sync = SpyLanguageSync()
        let settings = UserDefaultsAppSettingsStore(
            defaults: UserDefaults(suiteName: UUID().uuidString)!,
            preferredLanguageTags: { ["en"] }
        )
        return (CustomerPreferencesModel(settings: settings, languageSync: sync), sync)
    }

    private func profile(
        firstName: String = "Olena",
        lastName: String = "Kovalenko",
        phone: String? = "+420777111222",
        language: String? = "en"
    ) -> CurrentUserProfile {
        CurrentUserProfile(
            id: "user-1",
            email: "olena@example.com",
            firstName: firstName,
            lastName: lastName,
            phoneNumber: phone,
            birthDate: Date(timeIntervalSince1970: 400_000_000),
            preferredLanguageCode: language,
            isEmailConfirmed: true
        )
    }
}

@MainActor
private final class SpyLanguageSync: LanguagePreferenceSync {
    private(set) var sent: [String] = []
    private(set) var reconciled: [String] = []
    private var expectation: XCTestExpectation?

    func expect(_ count: Int) -> XCTestExpectation {
        let expectation = XCTestExpectation(description: "language pushed \(count)x")
        expectation.expectedFulfillmentCount = count
        self.expectation = expectation
        return expectation
    }

    func send(languageCode: String) async {
        sent.append(languageCode)
        expectation?.fulfill()
    }

    func reconcile(languageCode: String) async {
        reconciled.append(languageCode)
    }
}
