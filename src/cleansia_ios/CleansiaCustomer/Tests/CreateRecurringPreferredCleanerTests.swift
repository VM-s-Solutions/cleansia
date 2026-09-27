import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// `UpdateRecurringBooking` replaces the favourite cleaner with whatever it is sent, so an edit that
/// leaves them off clears them. When the server no longer accepts them, dropping them is the customer's
/// own choice, made by pressing for it — never a resend the form takes on their behalf.
@MainActor
final class CreateRecurringPreferredCleanerTests: XCTestCase {
    private static let endsOn = Date(timeIntervalSince1970: 1_800_000_000)

    private static let notEligible = ApiError(
        code: "order.preferred_employee.not_eligible",
        message: "A validation problem occurred.",
        httpStatus: 400
    )

    private static func schedule(preferredEmployeeId: String? = "emp-1") -> RecurringTemplate {
        RecurringFixtures.template(
            endsOn: endsOn,
            preferredEmployeeId: preferredEmployeeId,
            paymentType: RecurringPaymentType.card
        )
    }

    private func makeVM(editing: RecurringTemplate?, client: FakeRecurringBookingClient) -> CreateRecurringViewModel {
        CreateRecurringViewModel(
            sourceOrderId: nil,
            editing: editing,
            repository: RecurringBookingRepository(client: client),
            catalogClient: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            addressClient: FakeRecurringSavedAddressClient(),
            orderClient: FakeOrderClient(),
            quoteClient: FakeQuoteClient(),
            snackbar: SnackbarController(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    private static func withoutPreferredCleaner(_ input: UpdateRecurringInput) -> UpdateRecurringInput {
        UpdateRecurringInput(
            templateId: input.templateId,
            frequency: input.frequency,
            dayOfWeek: input.dayOfWeek,
            timeOfDay: input.timeOfDay,
            rooms: input.rooms,
            bathrooms: input.bathrooms,
            savedAddressId: input.savedAddressId,
            selectedServiceIds: input.selectedServiceIds,
            selectedPackageIds: input.selectedPackageIds,
            paymentType: input.paymentType,
            startsOn: input.startsOn,
            endsOn: input.endsOn,
            preferredEmployeeId: nil
        )
    }

    // MARK: - An edit keeps what the form does not edit

    func testAnEditSendsTheSchedulesFavouriteCleanerBack() async {
        let client = FakeRecurringBookingClient()
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()
        vm.setRooms(3)

        let saved = await vm.submit()

        XCTAssertTrue(saved)
        XCTAssertEqual(client.updateInputs.count, 1)
        XCTAssertEqual(client.updateInputs.first?.preferredEmployeeId, "emp-1", "the edit cleared the favourite")
        XCTAssertEqual(client.updateInputs.first?.endsOn, Self.endsOn)
        XCTAssertEqual(client.updateInputs.first?.rooms, 3)
        XCTAssertFalse(vm.preferredCleanerRefused)
    }

    func testAScheduleWithNoFavouriteCleanerSendsNone() async {
        let client = FakeRecurringBookingClient()
        let vm = makeVM(editing: Self.schedule(preferredEmployeeId: nil), client: client)
        await vm.load()

        _ = await vm.submit()

        XCTAssertEqual(client.updateInputs.count, 1)
        XCTAssertNil(client.updateInputs.first?.preferredEmployeeId)
    }

    func testTheTemplateReadsItsFavouriteCleanerOffTheWire() throws {
        let payload = RecurringBookingTemplateDto(
            id: "tpl-1",
            frequency: 1,
            dayOfWeek: 4,
            timeOfDay: "10:00",
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "addr-1",
            paymentType: 2,
            startsOn: Date(timeIntervalSince1970: 1_780_000_000),
            isActive: true,
            preferredEmployeeId: "emp-1",
            requiresPaymentMethodChange: false
        )

        XCTAssertEqual(try payload.toDomain().preferredEmployeeId, "emp-1")
    }

    // MARK: - A refused favourite cleaner is named, not dropped

    func testARefusedFavouriteCleanerIsNamedAndNotResentOnItsOwn() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(Self.notEligible)
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()

        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertTrue(vm.preferredCleanerRefused, "the refusal offers no way through")
        XCTAssertEqual(client.updateInputs.count, 1, "the form resent without the favourite on its own")
        XCTAssertEqual(client.updateInputs.first?.preferredEmployeeId, "emp-1")
        XCTAssertEqual(vm.submitState, .error(L10n.Recurring.editFailed))
    }

    func testAnyOtherRefusalOffersNoSaveWithout() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()

        _ = await vm.submit()

        XCTAssertFalse(vm.preferredCleanerRefused)
    }

    func testSavingWithoutThemResendsTheSameEditWithNoFavouriteCleaner() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(Self.notEligible)
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()
        vm.setTimeOfDay("11:30")
        _ = await vm.submit()
        client.updateResult = .success(Self.schedule(preferredEmployeeId: nil))

        let saved = await vm.saveWithoutPreferredCleaner()

        XCTAssertTrue(saved)
        XCTAssertEqual(client.updateInputs.count, 2)
        let refused = client.updateInputs[0]
        let resent = client.updateInputs[1]
        XCTAssertNil(resent.preferredEmployeeId)
        XCTAssertEqual(resent, Self.withoutPreferredCleaner(refused), "saving without them changed more than them")
        XCTAssertEqual(resent.timeOfDay, "11:30")
        XCTAssertEqual(resent.endsOn, Self.endsOn)
        XCTAssertFalse(vm.preferredCleanerRefused)
    }

    /// Nothing is dropped until a save without them actually goes through.
    func testASaveWithoutThemThatFailsKeepsThemForTheNextSave() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(Self.notEligible)
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()
        _ = await vm.submit()
        client.updateResult = .failure(ApiError(httpStatus: 500))

        let savedWithout = await vm.saveWithoutPreferredCleaner()
        _ = await vm.submit()

        XCTAssertFalse(savedWithout)
        XCTAssertEqual(client.updateInputs.map(\.preferredEmployeeId), ["emp-1", nil, "emp-1"])
    }
}
