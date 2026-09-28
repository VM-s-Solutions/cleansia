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

    private func makeVM(
        editing: RecurringTemplate?,
        client: FakeRecurringBookingClient,
        cleaners: FakeServingCleanersClient = FakeServingCleanersClient()
    ) -> CreateRecurringViewModel {
        CreateRecurringViewModel(
            sourceOrderId: nil,
            editing: editing,
            repository: RecurringBookingRepository(client: client),
            catalogClient: FakeCatalogClient(result: .success(CatalogFixtures.populated)),
            addressClient: FakeRecurringSavedAddressClient(),
            orderClient: FakeOrderClient(),
            quoteClient: FakeQuoteClient(),
            cleanersClient: cleaners,
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

    // MARK: - A new schedule can ask for a favourite cleaner

    private static let eva = ServingCleaner(id: "emp-1", fullName: "Eva")

    private func fillValid(_ vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("s-1")
        vm.setStartsOn(Date(timeIntervalSince1970: 1_780_000_000))
    }

    /// A schedule has no single instant, so the list is who served this customer, not who is free.
    func testTheFormOffersTheCleanersWhoServedTheCustomer() async {
        let cleaners = FakeServingCleanersClient(result: .success([Self.eva]))
        let vm = makeVM(editing: nil, client: FakeRecurringBookingClient(), cleaners: cleaners)

        await vm.load()

        XCTAssertEqual(vm.servingCleaners, [Self.eva])
        XCTAssertEqual(cleaners.callCount, 1)
    }

    func testAFailedCleanerListOffersNoPickAndStillSaves() async {
        let client = FakeRecurringBookingClient()
        let cleaners = FakeServingCleanersClient(result: .failure(ApiError(httpStatus: 500)))
        let vm = makeVM(editing: nil, client: client, cleaners: cleaners)
        await vm.load()
        fillValid(vm)

        let saved = await vm.submit()

        XCTAssertTrue(vm.servingCleaners.isEmpty)
        XCTAssertTrue(saved)
        XCTAssertNil(client.createInputs.first?.preferredEmployeeId)
    }

    func testANewScheduleSendsTheFavouriteCleanerPicked() async {
        let client = FakeRecurringBookingClient()
        let cleaners = FakeServingCleanersClient(result: .success([Self.eva]))
        let vm = makeVM(editing: nil, client: client, cleaners: cleaners)
        await vm.load()
        fillValid(vm)
        vm.setPreferredEmployeeId("emp-1")

        let saved = await vm.submit()

        XCTAssertTrue(saved)
        XCTAssertEqual(client.createInputs.first?.preferredEmployeeId, "emp-1")
    }

    func testANewScheduleWithNoPickSendsNone() async {
        let client = FakeRecurringBookingClient()
        let cleaners = FakeServingCleanersClient(result: .success([Self.eva]))
        let vm = makeVM(editing: nil, client: client, cleaners: cleaners)
        await vm.load()
        fillValid(vm)

        _ = await vm.submit()

        XCTAssertEqual(client.createInputs.count, 1)
        XCTAssertNil(client.createInputs.first?.preferredEmployeeId)
    }

    /// A new schedule has no stored favourite to keep: the snackbar names the refusal and the picker is
    /// the way through, so the pick stays on screen to be changed or cleared.
    func testARefusedPickOnANewScheduleKeepsThePickAndOffersNoSaveWithout() async {
        let client = FakeRecurringBookingClient()
        client.createResult = .failure(Self.notEligible)
        let cleaners = FakeServingCleanersClient(result: .success([Self.eva]))
        let vm = makeVM(editing: nil, client: client, cleaners: cleaners)
        await vm.load()
        fillValid(vm)
        vm.setPreferredEmployeeId("emp-1")

        let saved = await vm.submit()

        XCTAssertFalse(saved)
        XCTAssertFalse(vm.preferredCleanerRefused)
        XCTAssertEqual(vm.formState.preferredEmployeeId, "emp-1")
        XCTAssertEqual(client.createInputs.count, 1)
    }

    // MARK: - An edit can change or clear the favourite cleaner

    func testAnEditSendsANewlyPickedFavouriteCleaner() async {
        let client = FakeRecurringBookingClient()
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()
        vm.setPreferredEmployeeId("emp-2")

        _ = await vm.submit()

        XCTAssertEqual(client.updateInputs.first?.preferredEmployeeId, "emp-2")
    }

    func testAnEditThatClearsTheFavouriteCleanerSendsNone() async {
        let client = FakeRecurringBookingClient()
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()
        vm.setPreferredEmployeeId(nil)

        _ = await vm.submit()

        XCTAssertNil(client.updateInputs.first?.preferredEmployeeId)
    }

    func testChangingTheFavouriteCleanerAfterARefusalWithdrawsTheRefusal() async {
        let client = FakeRecurringBookingClient()
        client.updateResult = .failure(Self.notEligible)
        let vm = makeVM(editing: Self.schedule(), client: client)
        await vm.load()
        _ = await vm.submit()
        XCTAssertTrue(vm.preferredCleanerRefused)

        vm.setPreferredEmployeeId(nil)

        XCTAssertFalse(vm.preferredCleanerRefused)
    }
}
