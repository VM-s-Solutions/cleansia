import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class RecurringBookingsViewModelTests: XCTestCase {
    // swiftlint:disable large_tuple
    private func makeVM(
        client: FakeRecurringBookingClient = FakeRecurringBookingClient(),
        membership: MyMembership = MembershipFixtures.active,
        membershipClient: FakeMembershipManagementClient? = nil,
        addressClient: FakeRecurringSavedAddressClient = FakeRecurringSavedAddressClient(),
        catalog: FakeCatalogClient = FakeCatalogClient()
    ) -> (RecurringBookingsViewModel, RecurringBookingRepository, MembershipRepository) {
        let repo = RecurringBookingRepository(client: client)
        let memClient = membershipClient ?? FakeMembershipManagementClient()
        if membershipClient == nil {
            memClient.mineResults = [.success(membership)]
        }
        let memRepo = MembershipRepository(client: memClient)
        let vm = RecurringBookingsViewModel(
            repository: repo,
            membershipRepository: memRepo,
            addressClient: addressClient,
            catalogClient: catalog,
            snackbar: SnackbarController()
        )
        return (vm, repo, memRepo)
    }

    // swiftlint:enable large_tuple

    func testLoadPopulatesTemplates() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template()])]
        let (vm, _, _) = makeVM(client: client)

        await vm.load()

        XCTAssertEqual(vm.templates.count, 1)
        XCTAssertEqual(vm.templates.first?.id, "tpl-1")
    }

    func testToggleActiveFlipsAndRefetches() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [
            .success([RecurringFixtures.template(isActive: true)]),
            .success([RecurringFixtures.template(isActive: false)])
        ]
        let (vm, _, _) = makeVM(client: client)
        await vm.load()

        await vm.toggleActive(templateId: "tpl-1", currentlyActive: true)

        XCTAssertEqual(client.setActiveCalls.map(\.active), [false])
        XCTAssertEqual(vm.templates.first?.isActive, false)
    }

    func testDeleteRemovesAndRefetches() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [
            .success([RecurringFixtures.template()]),
            .success([])
        ]
        let (vm, _, _) = makeVM(client: client)
        await vm.load()

        await vm.delete(templateId: "tpl-1")

        XCTAssertEqual(client.deletedIds, ["tpl-1"])
        XCTAssertTrue(vm.templates.isEmpty)
    }

    // MARK: - A schedule holding a retired entry says so

    /// `CatalogFixtures.populated` lists s-1, s-2 and p-1; anything else is no longer offered.
    private func judged(
        _ templates: [RecurringTemplate],
        addresses: [RecurringSavedAddress] = [RecurringFixtures.address(id: "addr-1", countryId: "cze")],
        catalog: FakeCatalogClient = FakeCatalogClient(result: .success(CatalogFixtures.populated))
    ) async -> RecurringBookingsViewModel {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success(templates)]
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success(addresses)
        let (vm, _, _) = makeVM(client: client, addressClient: addressClient, catalog: catalog)
        await vm.load()
        return vm
    }

    func testAScheduleHoldingARetiredServiceIsMarked() async {
        let vm = await judged([RecurringFixtures.template(selectedServiceIds: ["s-1", "s-retired"])])

        XCTAssertEqual(vm.noLongerOfferedIds, ["tpl-1"])
    }

    func testAScheduleHoldingARetiredPackageIsMarked() async {
        let vm = await judged([RecurringFixtures.template(selectedPackageIds: ["p-1", "p-retired"])])

        XCTAssertEqual(vm.noLongerOfferedIds, ["tpl-1"])
    }

    func testAScheduleWhoseEveryEntryIsListedIsNotMarked() async {
        let vm = await judged([RecurringFixtures.template(
            selectedServiceIds: ["s-1", "s-2"],
            selectedPackageIds: ["p-1"]
        )])

        XCTAssertEqual(vm.noLongerOfferedIds, [])
    }

    func testNothingIsMarkedWhileTheMarketsCatalogueIsUnknown() async {
        let vm = await judged(
            [RecurringFixtures.template(selectedServiceIds: ["s-retired"])],
            catalog: FakeCatalogClient(result: .failure(ApiError(httpStatus: 500)))
        )

        XCTAssertEqual(vm.noLongerOfferedIds, [])
    }

    func testAScheduleWhoseAddressIsNotKnownIsNotJudged() async {
        let vm = await judged(
            [RecurringFixtures.template(selectedServiceIds: ["s-retired"])],
            addresses: [RecurringFixtures.address(id: "addr-other", countryId: "cze")]
        )

        XCTAssertEqual(vm.noLongerOfferedIds, [])
    }

    /// Each schedule is judged by its own market's catalogue: s-2 is listed in Czechia and not in
    /// Slovakia, so only the Slovak schedule holding it is marked.
    func testEachScheduleIsJudgedByItsOwnMarketsCatalogue() async {
        let catalog = MarketCatalogClient(byCountry: ["cze": CatalogFixtures.populated, "svk": CatalogFixtures.slovak])
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([
            RecurringFixtures.template(id: "tpl-cz", selectedServiceIds: ["s-2"], savedAddressId: "addr-cz"),
            RecurringFixtures.template(id: "tpl-sk", selectedServiceIds: ["s-2"], savedAddressId: "addr-sk")
        ])]
        let addressClient = FakeRecurringSavedAddressClient()
        addressClient.result = .success([
            RecurringFixtures.address(id: "addr-cz", countryId: "cze"),
            RecurringFixtures.address(id: "addr-sk", countryId: "svk")
        ])
        let vm = RecurringBookingsViewModel(
            repository: RecurringBookingRepository(client: client),
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
            addressClient: addressClient,
            catalogClient: catalog,
            snackbar: SnackbarController()
        )

        await vm.load()

        XCTAssertEqual(Set(catalog.requestedCountryIds), ["cze", "svk"])
        XCTAssertEqual(vm.noLongerOfferedIds, ["tpl-sk"])
    }

    func testNoScheduleReadsNoCatalogue() async {
        let catalog = FakeCatalogClient(result: .success(CatalogFixtures.populated))
        let (vm, _, _) = makeVM(catalog: catalog)

        await vm.load()

        XCTAssertEqual(catalog.callCount, 0)
    }

    // MARK: - Authoring is gated, management is not

    func testAuthoringIsAllowedForMember() async {
        let (vm, _, _) = makeVM(membership: MembershipFixtures.active)
        await vm.load()
        XCTAssertEqual(vm.authoring, .allowed)
    }

    func testAuthoringIsRefusedForResolvedNonMember() async {
        let (vm, _, _) = makeVM(membership: MembershipFixtures.inactive)
        await vm.load()
        XCTAssertEqual(vm.authoring, .upsell)
    }

    /// The reported defect: `hasMembership` was read out of a cache nothing on this screen
    /// filled, so a paid-up member met the upsell wall whenever it had not landed.
    func testAnUnresolvedMembershipFailsOpen() async {
        let memClient = FakeMembershipManagementClient()
        memClient.mineResults = [.failure(ApiError(httpStatus: 500))]
        let (vm, _, _) = makeVM(membershipClient: memClient)

        await vm.load()

        XCTAssertNil(vm.hasMembership)
        XCTAssertEqual(vm.authoring, .allowed)
    }

    func testTheScreenFetchesMembershipItselfRatherThanTrustingAnotherScreensCache() async {
        let memClient = FakeMembershipManagementClient()
        memClient.mineResults = [.success(MembershipFixtures.inactive)]
        let (vm, _, _) = makeVM(membershipClient: memClient)

        await vm.load()

        XCTAssertEqual(memClient.mineCallCount, 1)
        XCTAssertEqual(vm.authoring, .upsell)
    }

    func testALapsedMemberCanStillPauseAndResumeASchedule() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template(isActive: true)])]
        let (vm, _, _) = makeVM(client: client, membership: MembershipFixtures.inactive)
        await vm.load()
        XCTAssertEqual(vm.authoring, .upsell)

        await vm.toggleActive(templateId: "tpl-1", currentlyActive: true)
        await vm.toggleActive(templateId: "tpl-1", currentlyActive: false)

        XCTAssertEqual(client.setActiveCalls.map(\.active), [false, true])
    }

    func testALapsedMemberCanStillDeleteAScheduleThatIsChargingThem() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template()])]
        let (vm, _, _) = makeVM(client: client, membership: MembershipFixtures.inactive)
        await vm.load()

        await vm.delete(templateId: "tpl-1")

        XCTAssertEqual(client.deletedIds, ["tpl-1"])
    }

    func testALapsedMemberStillSeesTheirSchedules() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template()])]
        let (vm, _, _) = makeVM(client: client, membership: MembershipFixtures.inactive)

        await vm.load()

        XCTAssertEqual(vm.templates.count, 1)
        XCTAssertFalse(vm.affordances.showPlusUpsell)
        XCTAssertTrue(vm.affordances.showLapsedNotice)
    }

    // MARK: - The affordance table

    func testANonMemberWithSchedulesGetsTheLapsedNoticeAndNoCreateAffordance() {
        let affordances = RecurringListAffordances.of(gate: .upsell, hasTemplates: true)

        XCTAssertFalse(affordances.showCreateAction)
        XCTAssertFalse(affordances.showEdit)
        XCTAssertFalse(affordances.showPlusUpsell)
        XCTAssertTrue(affordances.showLapsedNotice)
    }

    func testANonMemberWithNoSchedulesGetsTheUpsellInsteadOfTheCreateCta() {
        let affordances = RecurringListAffordances.of(gate: .upsell, hasTemplates: false)

        XCTAssertTrue(affordances.showPlusUpsell)
        XCTAssertFalse(affordances.showCreateAction)
        XCTAssertFalse(affordances.showEdit)
        XCTAssertFalse(affordances.showLapsedNotice)
    }

    func testAMemberWithSchedulesGetsCreateAndEditAndNoUpsellCopy() {
        let affordances = RecurringListAffordances.of(gate: .allowed, hasTemplates: true)

        XCTAssertTrue(affordances.showCreateAction)
        XCTAssertTrue(affordances.showEdit)
        XCTAssertFalse(affordances.showPlusUpsell)
        XCTAssertFalse(affordances.showLapsedNotice)
    }

    /// Nothing replaces the empty state for a member, so its own create CTA renders.
    func testAMemberWithNoSchedulesKeepsTheEmptyStateCreateCta() {
        let affordances = RecurringListAffordances.of(gate: .allowed, hasTemplates: false)

        XCTAssertFalse(affordances.showPlusUpsell)
        XCTAssertFalse(affordances.showCreateAction)
    }

    // MARK: - Binding lifetime

    /// Both repositories are session-lived singletons and are deliberately held past the
    /// screen: a binding that retains `self` keeps the view model alive for the process.
    func testTheViewModelIsReleasedWhenTheScreenGoesAway() async {
        let repository = RecurringBookingRepository(client: FakeRecurringBookingClient())
        let membershipRepository = MembershipRepository(client: FakeMembershipManagementClient())
        weak var released: RecurringBookingsViewModel?

        func openAndLeaveTheScreen() async {
            let vm = RecurringBookingsViewModel(
                repository: repository,
                membershipRepository: membershipRepository,
                addressClient: FakeRecurringSavedAddressClient(),
                catalogClient: FakeCatalogClient(),
                snackbar: SnackbarController()
            )
            released = vm
            await vm.load()
            XCTAssertNotNil(released, "the view model was released before the screen was left")
        }

        await openAndLeaveTheScreen()

        XCTAssertNil(released, "the view model outlived its screen")
    }

    /// The bindings replay each repository's current value on subscribe, which is what makes
    /// separate seed assignments redundant. The membership half uses a resolved NON-member:
    /// `.allowed` is also the unresolved default, so a member fixture would pass unbound.
    func testWarmRepositoriesAreVisibleBeforeTheFirstLoad() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template()])]
        let repository = RecurringBookingRepository(client: client)
        await repository.refresh()
        let memClient = FakeMembershipManagementClient()
        memClient.mineResults = [.success(MembershipFixtures.inactive)]
        let membershipRepository = MembershipRepository(client: memClient)
        await membershipRepository.refresh()

        let vm = RecurringBookingsViewModel(
            repository: repository,
            membershipRepository: membershipRepository,
            addressClient: FakeRecurringSavedAddressClient(),
            catalogClient: FakeCatalogClient(),
            snackbar: SnackbarController()
        )

        XCTAssertEqual(vm.templates.map(\.id), ["tpl-1"])
        XCTAssertTrue(vm.loaded)
        XCTAssertEqual(vm.hasMembership, false)
        XCTAssertEqual(vm.authoring, .upsell)
    }

    func testTheGateResolverMirrorsTheServerSplit() {
        XCTAssertEqual(RecurringAuthoringGate.resolve(hasMembership: false), .upsell)
        XCTAssertEqual(RecurringAuthoringGate.resolve(hasMembership: true), .allowed)
        XCTAssertEqual(RecurringAuthoringGate.resolve(hasMembership: nil), .allowed)
    }
}

/// Answers each market with its own catalogue, so a test can price two schedules in two countries.
private final class MarketCatalogClient: CatalogClient, @unchecked Sendable {
    let byCountry: [String: Catalog]
    private(set) var requestedCountryIds: [String?] = []

    init(byCountry: [String: Catalog]) {
        self.byCountry = byCountry
    }

    func loadCatalog(countryId: String?) async -> ApiResult<Catalog> {
        requestedCountryIds.append(countryId)
        guard let countryId, let catalog = byCountry[countryId] else { return .failure(ApiError(httpStatus: 404)) }
        return .success(catalog)
    }
}
