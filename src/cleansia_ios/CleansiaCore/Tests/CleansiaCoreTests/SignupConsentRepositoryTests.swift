import XCTest
@testable import CleansiaCore

/// The parking rules for a signup tick that has no session to be recorded against yet.
///
/// Everything here protects against ONE outcome — a consent record the user never gave —
/// so each rule is pinned separately rather than folded into a happy path:
///  - an unticked box parks nothing;
///  - a tick delivers only into a session the server named with the same address;
///  - a type the account has already answered, granted OR withdrawn, is dropped rather
///    than re-granted;
///  - anything that did not land stays parked for the next session.
///
/// The exact commands that reach the wire are pinned per app, over each app's real
/// generated Gdpr API — see `SignupConsentFlowTests` in CleansiaCustomer and CleansiaPartner.
final class SignupConsentRepositoryTests: XCTestCase {
    private var client: RecordingSignupConsentClient!
    private var store: InMemorySignupConsentStore!

    private let typedEmail = "Ada@Example.com"
    private let sessionEmail = "ada@example.com"

    override func setUp() {
        super.setUp()
        client = RecordingSignupConsentClient()
        store = InMemorySignupConsentStore()
    }

    private func makeRepository() -> SignupConsentRepository {
        SignupConsentRepository(store: store, client: client)
    }

    func testATickedBoxGrantsBothDocumentsAndNothingElse() async {
        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)

        await repository.deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(client.granted, [.termsOfService, .privacyPolicy])
    }

    func testAnUntickedBoxParksNothingSoNothingIsEverGranted() async {
        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: false)

        await repository.deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(client.readCount, 0)
        XCTAssertEqual(client.granted, [])
    }

    func testWithNothingParkedNothingIsRead() async {
        await makeRepository().deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(client.readCount, 0)
        XCTAssertEqual(client.granted, [])
    }

    func testAnotherAccountsSessionNeverReceivesThisAccountsTick() async {
        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)

        await repository.deliver(sessionEmail: "someone.else@example.com")
        XCTAssertEqual(client.granted, [])

        await repository.deliver(sessionEmail: sessionEmail)
        XCTAssertEqual(client.granted, [.termsOfService, .privacyPolicy])
    }

    func testWithoutAServerSuppliedEmailNothingIsGrantedAndTheTickSurvives() async {
        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)

        await repository.deliver(sessionEmail: nil)
        XCTAssertEqual(client.granted, [])

        await repository.deliver(sessionEmail: sessionEmail)
        XCTAssertEqual(client.granted, [.termsOfService, .privacyPolicy])
    }

    func testATypeTheAccountAlreadyAnsweredIsNotReGranted() async {
        client.answered = [.termsOfService]

        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)
        await repository.deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(client.granted, [.privacyPolicy])
    }

    func testAnAccountThatAnsweredEverythingSettlesTheTick() async {
        client.answered = Set(SignupConsentType.signupTick)

        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)
        await repository.deliver(sessionEmail: sessionEmail)

        client.answered = []
        await repository.deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(client.granted, [])
    }

    func testAFailedConsentReadGrantsNothingAndKeepsTheTick() async {
        client.answered = nil

        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)
        await repository.deliver(sessionEmail: sessionEmail)
        XCTAssertEqual(client.granted, [])

        client.answered = []
        await repository.deliver(sessionEmail: sessionEmail)
        XCTAssertEqual(client.granted, [.termsOfService, .privacyPolicy])
    }

    func testAFailedGrantKeepsThatTypeParkedForTheNextSession() async {
        client.outcomes[.termsOfService] = .failed

        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)
        await repository.deliver(sessionEmail: sessionEmail)

        client.outcomes[.termsOfService] = .recorded
        await repository.deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(
            client.granted,
            [.termsOfService, .privacyPolicy, .termsOfService]
        )
    }

    func testADuplicateRefusalCountsAsDeliveredAndIsNotRetried() async {
        client.outcomes[.termsOfService] = .alreadyOnFile
        client.outcomes[.privacyPolicy] = .alreadyOnFile

        let repository = makeRepository()
        await repository.recordSignupTick(email: typedEmail, accepted: true)
        await repository.deliver(sessionEmail: sessionEmail)
        await repository.deliver(sessionEmail: sessionEmail)

        XCTAssertEqual(client.granted, [.termsOfService, .privacyPolicy])
    }

    func testTheParkedAddressIsTrimmedAndLowercased() async {
        let repository = makeRepository()
        await repository.recordSignupTick(email: "  ADA@example.COM ", accepted: true)

        await repository.deliver(sessionEmail: "ada@example.com")

        XCTAssertEqual(client.granted, [.termsOfService, .privacyPolicy])
    }

    func testABlankAddressParksNothing() async {
        let repository = makeRepository()
        await repository.recordSignupTick(email: "   ", accepted: true)

        await repository.deliver(sessionEmail: "")

        XCTAssertEqual(client.readCount, 0)
        XCTAssertEqual(client.granted, [])
    }

    func testTheSignupTickIsExactlyTermsOfServiceAndPrivacyPolicy() {
        XCTAssertEqual(SignupConsentType.signupTick, [.termsOfService, .privacyPolicy])
    }

    func testConsentTypesCarryTheBackendEnumValues() {
        XCTAssertEqual(SignupConsentType.termsOfService.rawValue, 0)
        XCTAssertEqual(SignupConsentType.privacyPolicy.rawValue, 1)
        XCTAssertEqual(SignupConsentType.marketingEmails.rawValue, 2)
        XCTAssertEqual(SignupConsentType.dataProcessing.rawValue, 3)
        XCTAssertEqual(SignupConsentType(rawValue: 1), .privacyPolicy)
        XCTAssertNil(SignupConsentType(rawValue: 9))
    }

    func testADuplicateRefusalIsReadOffTheProblemDetailsBagValue() {
        let refusal = ApiError.fromProblemDetails(
            httpStatus: 400,
            body: Data(#"""
            {"title":"Bad Request","type":"ConsentType","detail":"gdpr.consent_already_granted",
             "status":400,"errors":{"ConsentType":"gdpr.consent_already_granted"}}
            """#.utf8)
        )

        XCTAssertEqual(ApiResult<Void>.failure(refusal).consentGrantOutcome, .alreadyOnFile)
    }

    /// The same refusal with the two `Error` slots swapped — the shape that shipped before
    /// the backend fix. Nothing may treat it as delivered, or a real refusal that arrived in
    /// that shape would settle a record that was never written.
    func testTheOldSwappedRefusalShapeIsNotMistakenForADuplicate() {
        let refusal = ApiError.fromProblemDetails(
            httpStatus: 400,
            body: Data(#"""
            {"title":"Bad Request","type":"gdpr.consent_already_granted","detail":"Consent already granted",
             "status":400,"errors":{"gdpr.consent_already_granted":"Consent already granted"}}
            """#.utf8)
        )

        XCTAssertEqual(ApiResult<Void>.failure(refusal).consentGrantOutcome, .failed)
    }

    func testAnUnrelatedFailureIsNeverTreatedAsDelivered() {
        let outage = ApiError(code: "network.unreachable", httpStatus: 500)

        XCTAssertEqual(ApiResult<Void>.failure(outage).consentGrantOutcome, .failed)
        XCTAssertEqual(ApiResult<Void>.success(()).consentGrantOutcome, .recorded)
    }
}

private final class RecordingSignupConsentClient: SignupConsentClient, @unchecked Sendable {
    private let lock = NSLock()
    private var grants: [SignupConsentType] = []
    private var reads = 0

    var answered: Set<SignupConsentType>? = []
    var outcomes: [SignupConsentType: ConsentGrantOutcome] = [:]

    var granted: [SignupConsentType] {
        lock.withLock { grants }
    }

    var readCount: Int {
        lock.withLock { reads }
    }

    func answeredTypes() async -> Set<SignupConsentType>? {
        lock.withLock { reads += 1 }
        return answered
    }

    func grant(_ type: SignupConsentType) async -> ConsentGrantOutcome {
        lock.withLock { grants.append(type) }
        return outcomes[type] ?? .recorded
    }
}

final class InMemorySignupConsentStore: SignupConsentStore, @unchecked Sendable {
    private let lock = NSLock()
    private var pending: PendingSignupConsent?

    func save(_ pending: PendingSignupConsent) {
        lock.withLock { self.pending = pending }
    }

    func read() -> PendingSignupConsent? {
        lock.withLock { pending }
    }

    func settle(_ type: SignupConsentType) {
        lock.withLock {
            guard let current = pending else { return }
            let remaining = current.types.filter { $0 != type }
            pending = remaining.isEmpty ? nil : PendingSignupConsent(email: current.email, types: remaining)
        }
    }
}
