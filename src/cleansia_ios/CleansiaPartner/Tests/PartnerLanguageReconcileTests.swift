import CleansiaCore
import Combine
import XCTest
@testable import CleansiaPartner

/// The language push is silent on failure and has nothing to retry it, so a push that never landed used
/// to sit wrong until the cleaner next opened the picker — and the document it decides is a payout
/// invoice PDF they file. One call per session start heals it with the seam that already exists.
@MainActor
final class PartnerLanguageReconcileTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!
    private var client: FakePartnerUserClient!

    override func setUp() {
        super.setUp()
        suiteName = "PartnerLanguageReconcileTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
        client = FakePartnerUserClient()
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        client = nil
        super.tearDown()
    }

    func testASessionBeginningPushesTheStoredLanguageOntoAServerThatDisagrees() async {
        client.currentUser = .success(.stub(preferredLanguageCode: "en"))
        let (reconciler, hasSession) = makeReconciler(chosen: "uk", signedIn: true)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(client.updates.map(\.languageCode), ["uk"])
        XCTAssertEqual(client.updates.first?.firstName, "Ondrej", "the language rides a full profile replay")
    }

    /// The remedy is a reconcile, not a write: the server is asked, and agreement ends it. An
    /// unconditional push would replay a locally cached profile over the server's on every launch.
    func testAServerThatAlreadyAgreesIsAskedAndNotWritten() async {
        client.currentUser = .success(.stub(preferredLanguageCode: "uk"))
        let (reconciler, hasSession) = makeReconciler(chosen: "uk", signedIn: true)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await fulfillment(of: [client.read], timeout: 5)
        await settle()
        XCTAssertEqual(client.reads, 1)
        XCTAssertEqual(client.updates, [])
    }

    /// The pre-auth intro carousel offers the language picker, so the app can hold an explicit choice
    /// with nobody signed in. The session is answered from the token store rather than by letting a call
    /// 401, because on a handset that has never signed in a 401 wakes the shared refresh path for
    /// nothing.
    func testNoSessionMeansNoRequestAtAll() async {
        client.read.isInverted = true
        let (reconciler, hasSession) = makeReconciler(chosen: "uk", signedIn: false)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await fulfillment(of: [client.read], timeout: 0.5)
        XCTAssertEqual(client.reads, 0)
        XCTAssertEqual(client.updates, [])
    }

    /// A cleaner who has never opened the picker is displaying the handset's locale, which is an
    /// accident of the device rather than a decision. There is no failed push to heal, and pushing it
    /// would overwrite a language chosen on the web or set by support. The gate refuses before the read,
    /// so it costs no request either.
    func testACleanerWhoNeverPickedALanguageIsNotEvenAskedAbout() async {
        client.read.isInverted = true
        let (reconciler, hasSession) = makeReconciler(chosen: nil, signedIn: true)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)

        await fulfillment(of: [client.read], timeout: 0.5)
        XCTAssertEqual(client.reads, 0)
        XCTAssertEqual(client.updates, [])
    }

    /// The hazard the seam was built non-`async` for. Sign-in navigates away from whatever screen
    /// triggered it, so a reconcile owned by that screen's task is cancelled between the profile read
    /// and the write. The first arrangement runs that hazard deliberately, so the second cannot pass
    /// vacuously.
    func testTheReconcileOutlivesTheTaskThatTriggeredIt() async {
        client.currentUser = .success(.stub(preferredLanguageCode: "en"))
        let owned = Task { await self.sync(signedIn: true).push(languageCode: "uk") }
        owned.cancel()
        await owned.value
        XCTAssertEqual(client.updates, [], "the fake completes a cancelled round trip — nothing below means anything")

        let (reconciler, hasSession) = makeReconciler(chosen: "uk", signedIn: true)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())
        let screen = Task { hasSession.send(true) }
        screen.cancel()
        await screen.value

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(client.updates.map(\.languageCode), ["uk"])
    }

    /// The one line that puts the reconcile on the session signal. Without it every assertion above is
    /// about a wire nothing connects.
    func testTheContainerPutsTheReconcileOnTheSessionSignal() throws {
        let baseURL = try XCTUnwrap(URL(string: "https://language-reconcile.test"))
        let container = PartnerAppContainer(snackbar: SnackbarController(), apiBaseURL: baseURL)
        XCTAssertFalse(container.languageReconciler.isObservingSession)

        container.startLanguageReconcile()

        XCTAssertTrue(container.languageReconciler.isObservingSession)
    }

    private func makeReconciler(
        chosen: String?,
        signedIn: Bool
    ) -> (LanguageReconciler, CurrentValueSubject<Bool, Never>) {
        let settings = UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { ["en"] })
        if let chosen { settings.setLanguage(chosen) }
        let sync = sync(signedIn: signedIn)
        let reconciler = LanguageReconciler(settings: settings) { tag in
            sync.send(languageCode: tag)
        }
        return (reconciler, CurrentValueSubject(false))
    }

    private func sync(signedIn: Bool) -> LiveLanguagePreferenceSync {
        LiveLanguagePreferenceSync(tokenStore: SessionTokenStore(signedIn: signedIn), client: client)
    }

    private func settle() async {
        try? await Task.sleep(nanoseconds: 100_000_000)
    }
}
