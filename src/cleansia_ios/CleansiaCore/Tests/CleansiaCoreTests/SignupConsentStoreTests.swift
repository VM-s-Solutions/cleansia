import XCTest
@testable import CleansiaCore

final class SignupConsentStoreTests: XCTestCase {
    private var suiteName: String!
    private var defaults: UserDefaults!

    override func setUpWithError() throws {
        try super.setUpWithError()
        suiteName = "signup-consent-\(UUID().uuidString)"
        defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        super.tearDown()
    }

    private func makeStore() -> UserDefaultsSignupConsentStore {
        UserDefaultsSignupConsentStore(defaults: defaults)
    }

    /// The confirmation mail is often read on another device and answered a day later, so the
    /// tick has to outlive the process that parked it.
    func testAParkedTickSurvivesAFreshStoreOverTheSameDefaults() {
        makeStore().save(PendingSignupConsent(email: "ada@example.com", types: SignupConsentType.signupTick))

        let reread = makeStore().read()

        XCTAssertEqual(reread?.email, "ada@example.com")
        XCTAssertEqual(reread?.types, [.termsOfService, .privacyPolicy])
    }

    func testSettlingOneTypeLeavesTheOther() {
        let store = makeStore()
        store.save(PendingSignupConsent(email: "ada@example.com", types: SignupConsentType.signupTick))

        store.settle(.termsOfService)

        XCTAssertEqual(store.read()?.types, [.privacyPolicy])
    }

    func testSettlingTheLastTypeDropsTheRecordEntirely() {
        let store = makeStore()
        store.save(PendingSignupConsent(email: "ada@example.com", types: SignupConsentType.signupTick))

        store.settle(.termsOfService)
        store.settle(.privacyPolicy)

        XCTAssertNil(store.read())
        XCTAssertNil(defaults.string(forKey: "consent.pending_email"))
    }

    func testAnEmptyStoreReadsAsNothingParked() {
        XCTAssertNil(makeStore().read())
    }

    /// A record whose stored types no longer resolve is not a record: it would otherwise
    /// pin an email forever and re-read the consent list on every sign-in.
    func testARecordWithNoResolvableTypesReadsAsNothingParked() {
        defaults.set("ada@example.com", forKey: "consent.pending_email")
        defaults.set([99], forKey: "consent.pending_types")

        XCTAssertNil(makeStore().read())
    }
}
