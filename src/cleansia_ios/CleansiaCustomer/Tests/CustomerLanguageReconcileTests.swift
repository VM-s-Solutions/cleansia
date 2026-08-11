import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// The language push is silent on failure and has nothing to retry it, so a push that never landed used
/// to sit wrong until the customer next opened the picker — and it decides the language every
/// server-rendered mail is written in. One call per session start heals it with the seam that exists.
@MainActor
final class CustomerLanguageReconcileTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!
    private var client: FakeUserProfileClient!

    override func setUp() {
        super.setUp()
        suiteName = "CustomerLanguageReconcileTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
        client = FakeUserProfileClient()
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        client = nil
        super.tearDown()
    }

    /// The whole reason the reconcile reads the server instead of the cached profile. That cache is
    /// session-scoped and cleared on sign-out, so on the launch this exists for it holds nothing — a
    /// reconcile that trusted it would compare against nil and quietly do nothing at all.
    func testASessionBeginningReadsTheServerRatherThanAColdProfileCache() async {
        client.currentUserResult = .success(profile(language: "en"))
        let (reconciler, hasSession, repository) = makeReconciler(chosen: "uk")
        XCTAssertNil(repository.currentUser, "the arrangement is a cold cache — otherwise this proves nothing")
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await settle()
        XCTAssertEqual(client.updateCallCount, 1)
        XCTAssertEqual(client.lastUpdate?.languageCode, "uk")
        XCTAssertEqual(client.lastUpdate?.firstName, "Olena", "the language rides a full profile replay")
    }

    /// The remedy is a reconcile, not a write: the server is asked, and agreement ends it. An
    /// unconditional push would replay a locally cached profile over the server's on every launch.
    func testAServerThatAlreadyAgreesIsAskedAndNotWritten() async {
        client.currentUserResult = .success(profile(language: "uk"))
        let (reconciler, hasSession, _) = makeReconciler(chosen: "uk")
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await settle()
        XCTAssertEqual(client.currentUserCallCount, 1)
        XCTAssertEqual(client.updateCallCount, 0)
    }

    func testNothingIsAskedBeforeASessionBegins() async {
        let (reconciler, hasSession, _) = makeReconciler(chosen: "uk")

        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        await settle()
        XCTAssertEqual(client.currentUserCallCount, 0)
        XCTAssertEqual(client.updateCallCount, 0)
    }

    /// Silent on failure, and no retry: the language is a stamp on server-rendered mail, not a save
    /// anybody is waiting on, and the next session start is the next attempt.
    func testAProfileThatCannotBeReadWritesNothingAndSaysNothing() async {
        client.currentUserResult = .failure(ApiError(code: "network.unreachable"))
        let (reconciler, hasSession, _) = makeReconciler(chosen: "uk")
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await settle()
        XCTAssertEqual(client.currentUserCallCount, 1)
        XCTAssertEqual(client.updateCallCount, 0)
    }

    /// A customer who has never opened the picker is displaying the handset's locale, which is an
    /// accident of the device rather than a decision. There is no failed push to heal, and pushing it
    /// would overwrite a language chosen on the web or set by support. The gate refuses before the read.
    func testACustomerWhoNeverPickedALanguageIsNotEvenAskedAbout() async {
        let (reconciler, hasSession, _) = makeReconciler(chosen: nil)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await settle()
        XCTAssertEqual(client.currentUserCallCount, 0)
        XCTAssertEqual(client.updateCallCount, 0)
    }

    /// Sign-in navigates away from whatever screen triggered it, so a reconcile owned by that screen's
    /// task would be cancelled between the profile read and the write. The task belongs to the container.
    func testTheReconcileOutlivesTheTaskThatTriggeredIt() async {
        client.currentUserResult = .success(profile(language: "en"))
        let (reconciler, hasSession, _) = makeReconciler(chosen: "uk")
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        let screen = Task { hasSession.send(true) }
        screen.cancel()
        await screen.value

        await settle()
        XCTAssertEqual(client.updateCallCount, 1)
        XCTAssertEqual(client.lastUpdate?.languageCode, "uk")
    }

    /// The one line that puts the reconcile on the session signal. Without it every assertion above is
    /// about a wire nothing connects.
    func testTheContainerPutsTheReconcileOnTheSessionSignal() throws {
        let baseURL = try XCTUnwrap(URL(string: "https://language-reconcile.test"))
        let container = CustomerAppContainer(snackbar: SnackbarController(), apiBaseURL: baseURL)
        XCTAssertFalse(container.languageReconciler.isObservingSession)

        container.startLanguageReconcile()

        XCTAssertTrue(container.languageReconciler.isObservingSession)
    }

    private func makeReconciler(
        chosen: String?
    ) -> (LanguageReconciler, CurrentValueSubject<Bool, Never>, UserProfileRepository) {
        let settings = UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { ["en"] })
        if let chosen { settings.setLanguage(chosen) }
        let repository = UserProfileRepository(client: client)
        let sync = LiveLanguagePreferenceSync(repository: repository)
        let reconciler = LanguageReconciler(settings: settings) { tag in
            Task { @MainActor in await sync.reconcile(languageCode: tag) }
        }
        return (reconciler, CurrentValueSubject(false), repository)
    }

    private func profile(language: String?) -> CurrentUserProfile {
        CurrentUserProfile(
            id: "user-1",
            email: "olena@example.com",
            firstName: "Olena",
            lastName: "Kovalenko",
            phoneNumber: "+420777111222",
            birthDate: nil,
            preferredLanguageCode: language,
            isEmailConfirmed: true
        )
    }

    private func settle() async {
        try? await Task.sleep(nanoseconds: 200_000_000)
    }
}
