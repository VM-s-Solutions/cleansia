import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// A package and a service it already includes are both done and both charged
/// (→ /product/business-rules#charging-a-package-and-a-service-together). The clients never
/// de-duplicate: they mark the service and ask before a tap adds it twice.
@MainActor
final class TwiceBookedPickTests: XCTestCase {
    private let english = Locale(identifier: "en")

    /// p-1 holds s-1 and s-2, p-2 holds s-1, p-3 holds nothing; s-3 is in no package.
    private let catalog = Catalog(
        services: [
            CatalogFixtures.service(id: "s-1"),
            CatalogFixtures.service(id: "s-2"),
            CatalogFixtures.service(id: "s-3")
        ],
        packages: [
            TwiceBookedPickTests.package(id: "p-1", including: ["s-1", "s-2"]),
            TwiceBookedPickTests.package(id: "p-2", including: ["s-1"]),
            TwiceBookedPickTests.package(id: "p-3", including: [])
        ],
        currencyCode: "CZK",
        defaultCurrencyCode: "CZK"
    )

    private static func package(id: String, including serviceIds: [String?]) -> CatalogPackage {
        CatalogPackage(
            id: id,
            name: "Package \(id)",
            description: nil,
            price: 1500,
            translations: [:],
            includedServices: serviceIds.map {
                CatalogPackageServiceSummary(serviceId: $0, name: "Included \($0 ?? "-")", translations: [:])
            }
        )
    }

    // MARK: - The marker

    func testTheMarkerNamesEverySelectedPackageThatIncludesTheService() {
        let selected: Set = ["p-1", "p-2", "p-3"]

        XCTAssertEqual(
            catalog.inPackageNote(for: "s-1", selectedPackageIds: selected, locale: english),
            L10n.Booking.inYourPackage("Package p-1, Package p-2")
        )
        XCTAssertEqual(
            catalog.inPackageNote(for: "s-2", selectedPackageIds: selected, locale: english),
            L10n.Booking.inYourPackage("Package p-1")
        )
        XCTAssertNil(catalog.inPackageNote(for: "s-3", selectedPackageIds: selected, locale: english))
    }

    func testAPackageThatIsNotSelectedMarksNothing() {
        XCTAssertNil(catalog.inPackageNote(for: "s-1", selectedPackageIds: [], locale: english))
        XCTAssertNil(catalog.inPackageNote(for: "s-1", selectedPackageIds: ["p-3"], locale: english))
    }

    func testAnIncludedServiceWithNoIdMarksNothingAndAsksNothing() {
        let catalog = Catalog(
            services: [CatalogFixtures.service(id: "s-1")],
            packages: [TwiceBookedPickTests.package(id: "p-1", including: [nil])],
            currencyCode: "CZK",
            defaultCurrencyCode: "CZK"
        )

        XCTAssertNil(catalog.inPackageNote(for: "s-1", selectedPackageIds: ["p-1"], locale: english))
        XCTAssertNil(catalog.twiceBookedPick(addingService: "s-1", selectedPackageIds: ["p-1"]))
        XCTAssertNil(catalog.twiceBookedPick(addingPackage: "p-1", selectedServiceIds: ["s-1"], selectedPackageIds: []))
    }

    /// The packages a service is already in come in catalogue order; the services a package would book
    /// again come in the package's own order.
    func testThePickNamesWhatWouldBeBookedAgainInOrder() {
        XCTAssertEqual(
            catalog.twiceBookedPick(addingService: "s-1", selectedPackageIds: ["p-2", "p-1", "p-3"]),
            .service(id: "s-1", packageIds: ["p-1", "p-2"])
        )
        XCTAssertEqual(
            catalog.twiceBookedPick(
                addingPackage: "p-1",
                selectedServiceIds: ["s-3", "s-2", "s-1"],
                selectedPackageIds: []
            ),
            .package(id: "p-1", serviceIds: ["s-1", "s-2"])
        )
        XCTAssertNil(catalog.twiceBookedPick(addingService: "s-3", selectedPackageIds: ["p-1", "p-2"]))
        XCTAssertNil(catalog.twiceBookedPick(
            addingPackage: "p-3",
            selectedServiceIds: ["s-1"],
            selectedPackageIds: ["p-1"]
        ))
    }

    /// Two chosen packages that share a service book it twice just as a package and the service on its
    /// own do, so adding the second asks too.
    func testAPackageOverlappingAnotherSelectedPackageIsAPick() {
        XCTAssertEqual(
            catalog.twiceBookedPick(addingPackage: "p-1", selectedServiceIds: [], selectedPackageIds: ["p-2"]),
            .package(id: "p-1", serviceIds: ["s-1"])
        )
        XCTAssertEqual(
            catalog.twiceBookedPick(addingPackage: "p-1", selectedServiceIds: ["s-2"], selectedPackageIds: ["p-2"]),
            .package(id: "p-1", serviceIds: ["s-1", "s-2"]),
            "on its own and through another package, together"
        )
        XCTAssertNil(catalog.twiceBookedPick(addingPackage: "p-2", selectedServiceIds: [], selectedPackageIds: ["p-3"]))
    }

    // MARK: - The message

    /// A service already in two chosen packages would be booked a third time: "twice" would be false.
    func testAServiceInTwoSelectedPackagesSaysOnceMoreNotTwice() {
        let many = catalog.twiceBookedMessage(.service(id: "s-1", packageIds: ["p-1", "p-2"]), locale: english)
        let one = catalog.twiceBookedMessage(.service(id: "s-1", packageIds: ["p-1"]), locale: english)

        XCTAssertEqual(
            many,
            L10n.Booking.twiceServiceMessageMany(service: "Service s-1", packages: "Package p-1, Package p-2")
        )
        XCTAssertEqual(one, L10n.Booking.twiceServiceMessage(service: "Service s-1", packages: "Package p-1"))
        XCTAssertNotEqual(
            L10n.Booking.twiceServiceMessageMany(service: "Service s-1", packages: "Package p-1"),
            L10n.Booking.twiceServiceMessage(service: "Service s-1", packages: "Package p-1")
        )
    }

    /// The package's own summaries name what it repeats, so a service the catalogue does not offer on its
    /// own, but another chosen package includes, is still named.
    func testAPackageMessageNamesWhatItRepeatsFromItsOwnSummaries() throws {
        let catalog = Catalog(
            services: [CatalogFixtures.service(id: "s-1")],
            packages: [
                TwiceBookedPickTests.package(id: "p-a", including: ["s-9", "s-1"]),
                TwiceBookedPickTests.package(id: "p-b", including: ["s-9"])
            ],
            currencyCode: "CZK",
            defaultCurrencyCode: "CZK"
        )

        let pick = try XCTUnwrap(
            catalog.twiceBookedPick(addingPackage: "p-a", selectedServiceIds: ["s-1"], selectedPackageIds: ["p-b"])
        )

        XCTAssertEqual(pick, .package(id: "p-a", serviceIds: ["s-9", "s-1"]))
        XCTAssertEqual(
            catalog.twiceBookedMessage(pick, locale: english),
            L10n.Booking.twicePackageMessage(package: "Package p-a", services: "Included s-9, Included s-1")
        )
    }

    // MARK: - The booking's services step

    private func bookingVM() async -> BookingViewModel {
        let vm = BookingViewModel(
            catalogClient: FakeCatalogClient(result: .success(catalog)),
            quoteClient: FakeQuoteClient(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
        await vm.loadCatalog()
        return vm
    }

    func testBookingAddingAServiceASelectedPackageIncludesAsksAndCancelLeavesTheSelection() async {
        let vm = await bookingVM()
        vm.togglePackage("p-1")

        vm.toggleService("s-1")

        XCTAssertEqual(vm.twiceBookedPick, .service(id: "s-1", packageIds: ["p-1"]))
        XCTAssertEqual(vm.state.selectedServiceIds, [], "nothing is added before the customer answers")

        vm.cancelTwiceBooked()

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.state.selectedServiceIds, [])
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-1"])
    }

    func testBookingConfirmingAddsTheServiceAlongsideThePackage() async throws {
        let vm = await bookingVM()
        vm.togglePackage("p-1")
        vm.toggleService("s-2")
        let pick = try XCTUnwrap(vm.twiceBookedPick)

        vm.confirmTwiceBooked(pick)

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-2"])
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-1"], "never de-duplicated")
    }

    func testBookingAddingAPackageThatIncludesASelectedServiceAsksFirst() async {
        let vm = await bookingVM()
        vm.toggleService("s-1")

        XCTAssertFalse(vm.togglePackage("p-2"), "the sheet stays open while the customer is asked")
        XCTAssertEqual(vm.twiceBookedPick, .package(id: "p-2", serviceIds: ["s-1"]))
        XCTAssertEqual(vm.state.selectedPackageIds, [])

        vm.confirmTwiceBooked(.package(id: "p-2", serviceIds: ["s-1"]))

        XCTAssertEqual(vm.state.selectedPackageIds, ["p-2"])
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
    }

    func testBookingAddingAPackageThatOverlapsAnotherSelectedPackageAsksFirst() async {
        let vm = await bookingVM()
        XCTAssertTrue(vm.togglePackage("p-2"))

        XCTAssertFalse(vm.togglePackage("p-1"), "the sheet stays open while the customer is asked")
        XCTAssertEqual(vm.twiceBookedPick, .package(id: "p-1", serviceIds: ["s-1"]))
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-2"])

        vm.cancelTwiceBooked()
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-2"])

        vm.togglePackage("p-1")
        vm.confirmTwiceBooked(.package(id: "p-1", serviceIds: ["s-1"]))
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-1", "p-2"], "never de-duplicated")
    }

    func testBookingAddingAServiceTwoSelectedPackagesIncludeAsksWithTheOnceMoreMessage() async throws {
        let vm = await bookingVM()
        vm.update { var next = $0
            next.selectedPackageIds = ["p-1", "p-2"]
            return next
        }

        vm.toggleService("s-1")

        let pick = try XCTUnwrap(vm.twiceBookedPick)
        XCTAssertEqual(pick, .service(id: "s-1", packageIds: ["p-1", "p-2"]))
        XCTAssertEqual(
            catalog.twiceBookedMessage(pick, locale: english),
            L10n.Booking.twiceServiceMessageMany(service: "Service s-1", packages: "Package p-1, Package p-2")
        )
    }

    func testBookingAnAddThatBooksNothingTwiceNeverAsks() async {
        let vm = await bookingVM()

        XCTAssertTrue(vm.togglePackage("p-1"))
        vm.toggleService("s-3")
        XCTAssertTrue(vm.togglePackage("p-3"))

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-3"])
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-1", "p-3"])
    }

    func testBookingRemovingNeverAsks() async {
        let vm = await bookingVM()
        vm.togglePackage("p-1")
        vm.toggleService("s-1")
        vm.confirmTwiceBooked(.service(id: "s-1", packageIds: ["p-1"]))

        vm.toggleService("s-1")
        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.state.selectedServiceIds, [])

        vm.update { var next = $0
            next.selectedServiceIds = ["s-1"]
            return next
        }
        XCTAssertTrue(vm.togglePackage("p-1"))
        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.state.selectedPackageIds, [])
    }

    /// A Home package card, quick-size, Order again and a resumed draft all write the draft directly;
    /// they show the marker and never ask.
    func testBookingASeededSelectionNeverAsksButIsMarked() async {
        let vm = await bookingVM()
        let order = OrderFixtures.detail(
            services: [OrderFixtures.service(id: "s-1")],
            packages: [OrderFixtures.package(id: "p-2")]
        )

        vm.update { BookingPrefill.rebook($0, order: order, savedAddresses: [], catalog: self.catalog).state }
        vm.update { BookingPrefill.withPackage($0, packageId: "p-1") }

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.state.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.state.selectedPackageIds, ["p-1", "p-2"])
        XCTAssertEqual(
            catalog.inPackageNote(for: "s-1", selectedPackageIds: vm.state.selectedPackageIds, locale: english),
            L10n.Booking.inYourPackage("Package p-1, Package p-2")
        )
    }

    func testBookingResetDropsAPendingQuestion() async {
        let vm = await bookingVM()
        vm.togglePackage("p-1")
        vm.toggleService("s-1")

        vm.reset()

        XCTAssertNil(vm.twiceBookedPick)
    }

    // MARK: - The recurring form

    private func recurringVM(
        orderClient: FakeOrderClient = FakeOrderClient(),
        sourceOrderId: String? = nil
    ) async -> CreateRecurringViewModel {
        let vm = CreateRecurringViewModel(
            sourceOrderId: sourceOrderId,
            repository: RecurringBookingRepository(client: FakeRecurringBookingClient()),
            catalogClient: FakeCatalogClient(result: .success(catalog)),
            addressClient: FakeRecurringSavedAddressClient(),
            orderClient: orderClient,
            quoteClient: FakeQuoteClient(),
            cleanersClient: FakeServingCleanersClient(),
            snackbar: SnackbarController(),
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
        await vm.load()
        return vm
    }

    func testRecurringAddingAServiceASelectedPackageIncludesAsksAndCancelLeavesTheSelection() async {
        let vm = await recurringVM()
        vm.togglePackage("p-2")

        vm.toggleService("s-1")

        XCTAssertEqual(vm.twiceBookedPick, .service(id: "s-1", packageIds: ["p-2"]))
        XCTAssertEqual(vm.formState.selectedServiceIds, [])

        vm.cancelTwiceBooked()

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.formState.selectedServiceIds, [])
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-2"])
    }

    func testRecurringConfirmingAddsTheServiceAlongsideThePackage() async {
        let vm = await recurringVM()
        vm.togglePackage("p-2")
        vm.toggleService("s-1")

        vm.confirmTwiceBooked(.service(id: "s-1", packageIds: ["p-2"]))

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-2"])
    }

    func testRecurringAddingAPackageThatIncludesASelectedServiceAsksFirst() async {
        let vm = await recurringVM()
        vm.toggleService("s-2")

        vm.togglePackage("p-1")

        XCTAssertEqual(vm.twiceBookedPick, .package(id: "p-1", serviceIds: ["s-2"]))
        XCTAssertEqual(vm.formState.selectedPackageIds, [])

        vm.confirmTwiceBooked(.package(id: "p-1", serviceIds: ["s-2"]))

        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-1"])
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-2"])
    }

    func testRecurringAddingAPackageThatOverlapsAnotherSelectedPackageAsksFirst() async {
        let vm = await recurringVM()
        vm.togglePackage("p-2")
        XCTAssertNil(vm.twiceBookedPick)

        vm.togglePackage("p-1")

        XCTAssertEqual(vm.twiceBookedPick, .package(id: "p-1", serviceIds: ["s-1"]))
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-2"])

        vm.cancelTwiceBooked()

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-2"])
    }

    func testRecurringAddingAServiceTwoSelectedPackagesIncludeAsksWithTheOnceMoreMessage() async throws {
        let vm = await recurringVM()
        vm.togglePackage("p-1")
        vm.togglePackage("p-2")
        vm.confirmTwiceBooked(.package(id: "p-2", serviceIds: ["s-1"]))

        vm.toggleService("s-1")

        let pick = try XCTUnwrap(vm.twiceBookedPick)
        XCTAssertEqual(pick, .service(id: "s-1", packageIds: ["p-1", "p-2"]))
        XCTAssertEqual(
            catalog.twiceBookedMessage(pick, locale: english),
            L10n.Booking.twiceServiceMessageMany(service: "Service s-1", packages: "Package p-1, Package p-2")
        )
    }

    func testRecurringRemovingNeverAsks() async {
        let vm = await recurringVM()
        vm.toggleService("s-1")
        vm.togglePackage("p-1")
        vm.confirmTwiceBooked(.package(id: "p-1", serviceIds: ["s-1"]))

        vm.toggleService("s-1")
        vm.togglePackage("p-1")

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.formState.selectedServiceIds, [])
        XCTAssertEqual(vm.formState.selectedPackageIds, [])
    }

    func testRecurringAnOrderItIsMadeFromSeedsBothWithoutAsking() async {
        let orderClient = FakeOrderClient()
        orderClient.detailResults = [.success(OrderFixtures.detail(
            id: "ord-7",
            services: [OrderFixtures.service(id: "s-1")],
            packages: [OrderFixtures.package(id: "p-1")]
        ))]

        let vm = await recurringVM(orderClient: orderClient, sourceOrderId: "ord-7")

        XCTAssertNil(vm.twiceBookedPick)
        XCTAssertEqual(vm.formState.selectedServiceIds, ["s-1"])
        XCTAssertEqual(vm.formState.selectedPackageIds, ["p-1"])
    }
}
