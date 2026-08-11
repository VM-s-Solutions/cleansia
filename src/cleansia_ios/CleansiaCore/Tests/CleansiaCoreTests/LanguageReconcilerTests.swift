import Combine
import XCTest
@testable import CleansiaCore

/// The language push is silent on failure and has no retry, so a push that never landed used to
/// self-heal only when the user next opened the picker. These pin the three decisions that close that
/// hole with one call instead of a retry queue: WHEN it fires, that a device default is not a choice,
/// and that the seam — not this — decides whether anything is written.
final class LanguageReconcilerTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        suiteName = "LanguageReconcilerTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        super.tearDown()
    }

    func testASessionThatBeginsReconcilesTheStoredChoice() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("uk")
        let (reconciler, pushed) = makeReconciler(settings: store)
        let hasSession = CurrentValueSubject<Bool, Never>(false)

        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())
        hasSession.send(true)

        XCTAssertEqual(pushed.values, ["uk"])
    }

    /// A restored token at cold start is a session beginning just as much as a credential exchange is,
    /// and it is the far more frequent one. Anchoring on the sign-in callback instead would leave a
    /// cleaner who signs in once a year with one chance a year to self-heal.
    func testASessionAlreadyLiveWhenTheReconcilerAttachesCountsAsOne() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("cs")
        let (reconciler, pushed) = makeReconciler(settings: store)

        reconciler.attach(hasSession: CurrentValueSubject<Bool, Never>(true).eraseToAnyPublisher())

        XCTAssertEqual(pushed.values, ["cs"])
    }

    func testNothingIsReconciledWhileNobodyIsSignedIn() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("uk")
        let (reconciler, pushed) = makeReconciler(settings: store)

        reconciler.attach(hasSession: CurrentValueSubject<Bool, Never>(false).eraseToAnyPublisher())

        XCTAssertEqual(pushed.values, [], "a handset that has never signed in has no row to reconcile")
    }

    func testTheEndOfASessionReconcilesNothing() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("uk")
        let (reconciler, pushed) = makeReconciler(settings: store)
        let hasSession = CurrentValueSubject<Bool, Never>(true)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(false)

        XCTAssertEqual(pushed.values, ["uk"], "signing out is not a session start")
    }

    /// The session signal is re-sent at every route change, so an edge — not a level — is what fires
    /// this. Otherwise one launch spends a profile round trip per screen the user walks through.
    func testARepeatedSessionSignalDoesNotReconcileTwice() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("uk")
        let (reconciler, pushed) = makeReconciler(settings: store)
        let hasSession = CurrentValueSubject<Bool, Never>(false)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)
        hasSession.send(true)
        hasSession.send(true)

        XCTAssertEqual(pushed.values, ["uk"])
    }

    func testSigningOutAndInAgainReconcilesForTheNewSession() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("uk")
        let (reconciler, pushed) = makeReconciler(settings: store)
        let hasSession = CurrentValueSubject<Bool, Never>(false)
        reconciler.attach(hasSession: hasSession.eraseToAnyPublisher())

        hasSession.send(true)
        hasSession.send(false)
        hasSession.send(true)

        XCTAssertEqual(pushed.values, ["uk", "uk"])
    }

    /// The whole population that has never opened the picker resolves to the handset's locale ordering.
    /// That is an accident of the device, not a decision — and pushing it would overwrite a language
    /// chosen on the web, or set by support, with whatever this phone happens to be set to.
    func testALanguageNobodyChoseIsNeverReconciled() {
        let (reconciler, pushed) = makeReconciler(settings: makeStore(deviceLanguage: "uk"))

        reconciler.attach(hasSession: CurrentValueSubject<Bool, Never>(true).eraseToAnyPublisher())

        XCTAssertEqual(pushed.values, [], "the device default is not evidence of a choice")
    }

    func testGoingBackToFollowingTheDeviceStopsReconciling() {
        let store = makeStore(deviceLanguage: "en")
        store.setLanguage("uk")
        store.clearLanguage()
        let (reconciler, pushed) = makeReconciler(settings: store)

        reconciler.attach(hasSession: CurrentValueSubject<Bool, Never>(true).eraseToAnyPublisher())

        XCTAssertEqual(pushed.values, [])
    }

    private func makeStore(deviceLanguage: String) -> UserDefaultsAppSettingsStore {
        UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { [deviceLanguage] })
    }

    private func makeReconciler(settings: AppSettingsStore) -> (LanguageReconciler, Recorder) {
        let recorder = Recorder()
        return (LanguageReconciler(settings: settings) { recorder.record($0) }, recorder)
    }
}

private final class Recorder: @unchecked Sendable {
    private let lock = NSLock()
    private var recorded: [String] = []

    var values: [String] {
        lock.withLock { recorded }
    }

    func record(_ tag: String) {
        lock.withLock { recorded.append(tag) }
    }
}
