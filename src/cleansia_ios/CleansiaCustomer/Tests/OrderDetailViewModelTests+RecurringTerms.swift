import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// A recurring visit's confirm asks the booking review's terms tick on the booking's own rule: shown unless the
/// account already holds the Terms of Service and the Privacy Policy in force, and asserted on
/// ConfirmRecurringOrder only when the box was shown and ticked.
@MainActor
extension OrderDetailViewModelTests {
    private static let bothConsents: Set<SignupConsentType> = [.termsOfService, .privacyPolicy]

    private func awaitingConfirmation(
        _ consent: FakeConsentStatusClient,
        client: FakeOrderClient = FakeOrderClient()
    ) -> OrderDetailViewModel {
        client.detailResults = [.success(recurringOccurrence(paymentType: 1))]
        return makeVM(client: client, consent: consent)
    }

    // MARK: What is on record

    func testTheConfirmStaysClosedAndUnaskedUntilTheConsentsAreRead() {
        let vm = awaitingConfirmation(FakeConsentStatusClient(granted: Self.bothConsents))

        XCTAssertNil(vm.alreadyConsented)
        XCTAssertFalse(vm.asksForTerms)
        XCTAssertFalse(vm.canConfirmRecurring)
    }

    func testBothConsentsOnRecordHideTheBoxAndOpenTheConfirm() async {
        let consent = FakeConsentStatusClient(granted: Self.bothConsents)
        let vm = awaitingConfirmation(consent)

        await vm.load()

        XCTAssertEqual(vm.alreadyConsented, true)
        XCTAssertFalse(vm.asksForTerms)
        XCTAssertTrue(vm.canConfirmRecurring)
        XCTAssertEqual(consent.callCount, 1)
    }

    /// One of the two is not both: the sentence names the Terms of Service AND the Privacy Policy.
    func testOnlyOneOfTheTwoConsentsStillAsks() async {
        let vm = awaitingConfirmation(FakeConsentStatusClient(granted: [.termsOfService]))

        await vm.load()

        XCTAssertTrue(vm.asksForTerms)
        XCTAssertFalse(vm.canConfirmRecurring)
    }

    /// A read that failed is "ask", never "none": a consent that might not exist is asked for.
    func testAFailedReadAsksRatherThanAssumes() async {
        let vm = awaitingConfirmation(FakeConsentStatusClient(granted: nil))

        await vm.load()

        XCTAssertEqual(vm.alreadyConsented, false)
        XCTAssertTrue(vm.asksForTerms)
    }

    func testTheTickOpensTheConfirm() async {
        let vm = awaitingConfirmation(FakeConsentStatusClient())
        await vm.load()
        XCTAssertFalse(vm.canConfirmRecurring)

        vm.setTermsAccepted(true)

        XCTAssertTrue(vm.canConfirmRecurring)
    }

    func testAnOrderNotAwaitingConfirmationReadsNoConsents() async {
        let consent = FakeConsentStatusClient(granted: Self.bothConsents)
        let client = FakeOrderClient()
        client.detailResults = [.success(recurringOccurrence(paymentType: 1, needsConfirmation: false))]
        let vm = makeVM(client: client, consent: consent)

        await vm.load()

        XCTAssertEqual(consent.callCount, 0)
        XCTAssertNil(vm.alreadyConsented)
    }

    /// Every opening re-reads, so an answer that went stale — a consent withdrawn on the web GDPR page, or terms
    /// replaced since the visit was generated — is replaced, not kept.
    func testEveryOpeningRereadsTheConsents() async {
        let consent = FakeConsentStatusClient(granted: Self.bothConsents)
        let vm = awaitingConfirmation(consent)
        await vm.load()
        XCTAssertFalse(vm.asksForTerms)

        consent.granted = []
        await vm.load()

        XCTAssertTrue(vm.asksForTerms)
        XCTAssertEqual(consent.callCount, 2)
    }

    func testAnOrderReachedThroughRetryReadsTheConsents() async {
        let consent = FakeConsentStatusClient()
        let client = FakeOrderClient()
        client.detailResults = [.failure(ApiError(httpStatus: 503)), .success(recurringOccurrence(paymentType: 1))]
        let vm = makeVM(client: client, consent: consent)
        await vm.load()
        XCTAssertEqual(consent.callCount, 0)

        await vm.retry()

        XCTAssertEqual(consent.callCount, 1)
        XCTAssertTrue(vm.asksForTerms)
    }

    // MARK: What ConfirmRecurringOrder carries

    func testATickedBoxAssertsTheTermsOnTheConfirm() async {
        let client = FakeOrderClient()
        let vm = awaitingConfirmation(FakeConsentStatusClient(), client: client)
        await vm.load()
        vm.setTermsAccepted(true)

        await vm.confirmRecurring()

        XCTAssertEqual(client.confirmRecurringTerms, [true])
    }

    /// The box was not shown, so the confirm asserts nothing new — never a `true` the customer did not tick on
    /// this screen, and never a `false` that reads as a refusal.
    func testAnAccountThatAlreadyConsentedAssertsNothingOnTheConfirm() async {
        let client = FakeOrderClient()
        let vm = awaitingConfirmation(FakeConsentStatusClient(granted: Self.bothConsents), client: client)
        await vm.load()
        vm.setTermsAccepted(true)

        await vm.confirmRecurring()

        XCTAssertEqual(client.confirmRecurringTerms, [nil])
    }

    /// Reachable only by a caller that bypasses the gate; the confirm must then not claim a tick.
    func testAnUntickedBoxAssertsNothingOnTheConfirm() async {
        let client = FakeOrderClient()
        let vm = awaitingConfirmation(FakeConsentStatusClient(), client: client)
        await vm.load()

        await vm.confirmRecurring()

        XCTAssertEqual(client.confirmRecurringTerms, [nil])
    }

    // MARK: What the footer binds

    /// There is no view harness, so the binding is pinned by reading it: a box re-gated in the view, or a button
    /// enabled off something other than the view model's gate, would leave every assertion above green.
    func testTheConfirmFooterAsksAndGatesOffTheViewModelWithTheBookingsCopy() throws {
        let source = try orderDetailViewSource()

        XCTAssertTrue(
            source.contains(
                "termsAccepted: vm.asksForTerms ? Binding(get: { vm.termsAccepted }, set: vm.setTermsAccepted) : nil"
            ),
            "the confirm footer asks for the terms off something other than the view model's gate"
        )
        XCTAssertTrue(source.contains("canConfirm: vm.canConfirmRecurring"))
        XCTAssertTrue(source.contains("enabled: !submitting && canConfirm"), "the tick no longer gates the confirm")
        XCTAssertTrue(source.contains("markdown: L10n.Auth.acceptTerms,"), "the confirm words the tick differently")
        XCTAssertTrue(source.contains("toggleAccessibilityLabel: L10n.Auth.acceptTermsToggle"))
    }

    private func orderDetailViewSource() throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(
            contentsOf: root.appendingPathComponent("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift"),
            encoding: .utf8
        )
    }
}
