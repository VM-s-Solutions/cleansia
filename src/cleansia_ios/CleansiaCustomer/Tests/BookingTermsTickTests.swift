import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The review step's terms tick: shown unless the account already holds Terms of Service and Privacy
/// Policy, and asserted on `CreateOrder` only when the box was shown and ticked — an account that saw
/// no box asserts nothing new, which is the web wizard's own rule.
@MainActor
final class BookingTermsTickTests: XCTestCase {
    private func makeVM(
        consent: FakeConsentStatusClient = FakeConsentStatusClient(),
        create: FakeOrderCreateClient = FakeOrderCreateClient(),
        tokenStore: FakeTokenStore = .signedIn()
    ) -> BookingViewModel {
        BookingViewModel(
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 1234,
                currencyId: "cur-czk",
                currencyCode: "CZK"
            ))),
            profileClient: FakeProfileClient(),
            orderCreateClient: create,
            countryResolver: FakeCountryResolver(),
            consentClient: consent,
            tokenStore: tokenStore,
            isCardPaymentAvailable: false,
            quoteDebounce: .milliseconds(400),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    private func readyState(termsAccepted: Bool) -> (BookingState) -> BookingState {
        { _ in
            var s = BookingState()
            s.selectedServiceIds = ["s-1"]
            s.street = "Zenklova 6"
            s.city = "Praha"
            s.zipCode = "18000"
            s.countryIsoCode = "cz"
            s.selectedInstant = Date(timeIntervalSinceNow: 3600 * 48)
            s.paymentMethod = .cash
            s.termsAccepted = termsAccepted
            return s
        }
    }

    // MARK: - What is on record

    func testTheBoxIsAskedUntilTheRecordSaysOtherwise() {
        let vm = makeVM()

        XCTAssertFalse(vm.alreadyConsented)
    }

    func testBothConsentsOnRecordHideTheBox() async {
        let consent = FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy])
        let vm = makeVM(consent: consent)

        await vm.loadConsentStatus()

        XCTAssertTrue(vm.alreadyConsented)
        XCTAssertEqual(consent.callCount, 1)
    }

    /// One of the two is not both: the sentence names the Terms of Service AND the Privacy Policy.
    func testOnlyOneOfTheTwoConsentsStillAsks() async {
        let vm = makeVM(consent: FakeConsentStatusClient(granted: [.termsOfService]))

        await vm.loadConsentStatus()

        XCTAssertFalse(vm.alreadyConsented)
    }

    func testAMarketingConsentAloneCountsForNothing() async {
        let vm = makeVM(consent: FakeConsentStatusClient(granted: [.marketingEmails, .dataProcessing]))

        await vm.loadConsentStatus()

        XCTAssertFalse(vm.alreadyConsented)
    }

    /// A read that failed is "ask", never "none": a consent that might not exist is asked for.
    func testAFailedReadAsksRatherThanAssumes() async {
        let vm = makeVM(consent: FakeConsentStatusClient(granted: nil))

        await vm.loadConsentStatus()

        XCTAssertFalse(vm.alreadyConsented)
    }

    func testAGuestIsNeverLookedUp() async {
        let consent = FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy])
        let vm = makeVM(consent: consent, tokenStore: .guest)

        await vm.loadConsentStatus()

        XCTAssertFalse(vm.alreadyConsented)
        XCTAssertEqual(consent.callCount, 0)
    }

    /// The sheet re-reads at every opening, so an answer that went stale — a sign-out and another
    /// account's sign-in on the same session-lived view model — is replaced, not kept.
    func testARereadReplacesAStaleAnswer() async {
        let consent = FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy])
        let vm = makeVM(consent: consent)
        await vm.loadConsentStatus()
        XCTAssertTrue(vm.alreadyConsented)

        consent.granted = []
        await vm.loadConsentStatus()

        XCTAssertFalse(vm.alreadyConsented)
    }

    // MARK: - Per booking, never remembered

    func testResetStartsTheNextBookingUntickedButKeepsWhatIsOnRecord() async {
        let vm = makeVM(consent: FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy]))
        await vm.loadConsentStatus()
        vm.update(readyState(termsAccepted: true))

        vm.reset()

        XCTAssertFalse(vm.state.termsAccepted)
        XCTAssertTrue(vm.alreadyConsented)
    }

    // MARK: - What CreateOrder carries

    func testATickedBoxAssertsTheTermsOnTheOrder() async {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update(readyState(termsAccepted: true))

        _ = await vm.submit()

        XCTAssertEqual(create.commands.first?.termsAccepted, true)
    }

    /// The box was not shown, so the order asserts nothing new — never a `true` the customer did not
    /// tick on this screen, and never a `false` that reads as a refusal.
    func testAnAccountThatAlreadyConsentedAssertsNothingOnTheOrder() async throws {
        let create = FakeOrderCreateClient()
        let vm = makeVM(consent: FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy]), create: create)
        await vm.loadConsentStatus()
        vm.update(readyState(termsAccepted: true))

        _ = await vm.submit()

        let command = try XCTUnwrap(create.commands.first)
        XCTAssertNil(command.termsAccepted)
    }

    /// Reachable only by a caller that bypasses the gate; the order must then not claim a tick.
    func testAnUntickedBoxAssertsNothingOnTheOrder() async throws {
        let create = FakeOrderCreateClient()
        let vm = makeVM(create: create)
        vm.update(readyState(termsAccepted: false))

        _ = await vm.submit()

        let command = try XCTUnwrap(create.commands.first)
        XCTAssertNil(command.termsAccepted)
    }
}
