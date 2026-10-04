import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// CreateRecurringBooking refuses a new schedule unless the account holds the texts in force or the booking's
/// terms tick is asserted. The form asks on Android's rule: until the consents are read and hold both
/// documents, and after a failed read; an edit asks nothing.
@MainActor
final class CreateRecurringTermsTests: XCTestCase {
    private static let bothConsents: Set<SignupConsentType> = [.termsOfService, .privacyPolicy]

    private func makeVM(
        consent: FakeConsentStatusClient,
        editing: RecurringTemplate? = nil,
        client: FakeRecurringBookingClient = FakeRecurringBookingClient()
    ) -> CreateRecurringViewModel {
        CreateRecurringViewModel(
            sourceOrderId: nil,
            editing: editing,
            repository: RecurringBookingRepository(client: client),
            catalogClient: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            addressClient: FakeRecurringSavedAddressClient(),
            orderClient: FakeOrderClient(),
            quoteClient: FakeQuoteClient(),
            cleanersClient: FakeServingCleanersClient(),
            consentClient: consent,
            snackbar: SnackbarController(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    private func fillValid(_ vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("s-1")
        vm.setDirtiness(.normal)
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
        vm.setEarlyPerformanceRequested(true)
    }

    func testANewScheduleAsksBeforeTheConsentsAreRead() {
        let consent = FakeConsentStatusClient(granted: Self.bothConsents)
        let vm = makeVM(consent: consent)

        XCTAssertTrue(vm.termsAsked)
        XCTAssertEqual(consent.callCount, 0)
    }

    func testAnAccountHoldingBothDocumentsIsNotAskedAndAssertsNothing() async {
        let consent = FakeConsentStatusClient(granted: Self.bothConsents)
        let client = FakeRecurringBookingClient()
        let vm = makeVM(consent: consent, client: client)
        await vm.load()
        fillValid(vm)

        XCTAssertFalse(vm.termsAsked)
        XCTAssertTrue(vm.isValid)
        let succeeded = await vm.submit()

        XCTAssertTrue(succeeded)
        XCTAssertEqual(consent.callCount, 1)
        XCTAssertEqual(client.createInputs.count, 1)
        XCTAssertNil(client.createInputs.first?.termsAccepted)
    }

    /// One of the two is not both: the sentence names the Terms of Service AND the Privacy Policy.
    func testAnAccountMissingOneDocumentIsHeldUntilItTicksAndThenAssertsTheTick() async {
        let client = FakeRecurringBookingClient()
        let vm = makeVM(consent: FakeConsentStatusClient(granted: [.termsOfService]), client: client)
        await vm.load()
        fillValid(vm)

        XCTAssertTrue(vm.termsAsked)
        XCTAssertFalse(vm.isValid)
        let refused = await vm.submit()
        XCTAssertFalse(refused)
        XCTAssertTrue(client.createInputs.isEmpty)

        vm.setTermsAccepted(true)
        XCTAssertTrue(vm.isValid)
        let succeeded = await vm.submit()

        XCTAssertTrue(succeeded)
        XCTAssertEqual(client.createInputs.map(\.termsAccepted), [true])
    }

    /// A read that failed is "ask", never "none": a consent that might not exist is asked for.
    func testAFailedReadAsks() async {
        let vm = makeVM(consent: FakeConsentStatusClient(granted: nil))
        await vm.load()
        fillValid(vm)

        XCTAssertTrue(vm.termsAsked)
        XCTAssertFalse(vm.isValid)
    }

    /// Reachable only by a caller that bypasses the hidden box; the create must then not claim a tick.
    func testATickOnAnAccountThatWasNotAskedAssertsNothing() async {
        let client = FakeRecurringBookingClient()
        let vm = makeVM(consent: FakeConsentStatusClient(granted: Self.bothConsents), client: client)
        await vm.load()
        fillValid(vm)
        vm.setTermsAccepted(true)

        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.count, 1)
        XCTAssertNil(client.createInputs.first?.termsAccepted)
    }

    func testAnEditAsksNothingAndReadsNoConsents() async {
        let consent = FakeConsentStatusClient(granted: [])
        let client = FakeRecurringBookingClient()
        let vm = makeVM(consent: consent, editing: RecurringFixtures.template(), client: client)
        XCTAssertFalse(vm.termsAsked)

        await vm.load()

        XCTAssertFalse(vm.termsAsked)
        XCTAssertEqual(consent.callCount, 0)
        XCTAssertTrue(vm.isValid)
        let succeeded = await vm.submit()
        XCTAssertTrue(succeeded)
        XCTAssertEqual(client.updateInputs.count, 1)
    }
}
