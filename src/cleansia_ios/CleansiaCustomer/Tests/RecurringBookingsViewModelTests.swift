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

    /// A paused or past-due member's enrolment is live but the server refuses authoring, so they get no
    /// Create, no Edit and no upsell, and the benefits-paused notice instead, as Android's `Paused` gate
    /// (owner decisions 2026-10-05).
    func testAPausedMemberGetsTheNoticeAndNoAuthoring() {
        for hasTemplates in [true, false] {
            let affordances = RecurringListAffordances.of(gate: .paused, hasTemplates: hasTemplates)

            XCTAssertTrue(affordances.showPausedNotice, "hasTemplates=\(hasTemplates)")
            XCTAssertFalse(affordances.showCreateAction, "hasTemplates=\(hasTemplates)")
            XCTAssertFalse(affordances.showEdit, "hasTemplates=\(hasTemplates)")
            XCTAssertFalse(affordances.showPlusUpsell, "hasTemplates=\(hasTemplates)")
            XCTAssertFalse(affordances.showLapsedNotice, "hasTemplates=\(hasTemplates)")
        }
    }

    func testOnlyAPausedMemberGetsTheNotice() {
        for gate in [RecurringAuthoringGate.allowed, .upsell] {
            for hasTemplates in [true, false] {
                XCTAssertFalse(RecurringListAffordances.of(gate: gate, hasTemplates: hasTemplates).showPausedNotice)
            }
        }
    }

    func testAPausedMembersScreenOffersNoCreateOrEditAndAPaidUpMembersDoes() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([RecurringFixtures.template()])]
        let (paused, _, _) = makeVM(client: client, membership: MembershipFixtures.pastDue)
        await paused.load()
        let activeClient = FakeRecurringBookingClient()
        activeClient.mineResults = [.success([RecurringFixtures.template()])]
        let (active, _, _) = makeVM(client: activeClient, membership: MembershipFixtures.active)
        await active.load()

        XCTAssertTrue(paused.benefitsPaused)
        XCTAssertEqual(paused.authoring, .paused)
        XCTAssertTrue(paused.affordances.showPausedNotice)
        XCTAssertFalse(paused.affordances.showCreateAction)
        XCTAssertFalse(paused.affordances.showEdit)
        XCTAssertFalse(paused.affordances.showLapsedNotice)
        XCTAssertEqual(active.authoring, .allowed)
        XCTAssertTrue(active.affordances.showCreateAction)
        XCTAssertTrue(active.affordances.showEdit)
        XCTAssertFalse(active.affordances.showPausedNotice)
    }

    /// With no schedules a paused member gets the notice in place of the empty state and its create CTA,
    /// and no upsell: the server refuses a second subscription while this one lives.
    func testAPausedMemberWithNoSchedulesGetsTheNoticeAndNoUpsell() async {
        let client = FakeRecurringBookingClient()
        client.mineResults = [.success([])]
        let (vm, _, _) = makeVM(client: client, membership: MembershipFixtures.pastDue)

        await vm.load()

        XCTAssertTrue(vm.templates.isEmpty)
        XCTAssertTrue(vm.affordances.showPausedNotice)
        XCTAssertFalse(vm.affordances.showPlusUpsell)
        XCTAssertFalse(vm.affordances.showCreateAction)
    }

    /// The screen's branches, in Android's order: the upsell, the paused notice in place of the empty state
    /// (whose create CTA it hides), the empty state, the list. Read from source, as the card test below.
    func testTheScreenPutsThePausedNoticeBeforeTheEmptyState() throws {
        let source = try compactScreenSource()

        XCTAssertTrue(source.contains(
            "ifvm.affordances.showPlusUpsell{PlusGate(onSubscribe:onSubscribePlus)}"
                + "elseifvm.affordances.showPausedNotice,vm.templates.isEmpty{"
        ))
        let paused = try XCTUnwrap(source.range(of: "elseifvm.affordances.showPausedNotice,vm.templates.isEmpty{"))
        let empty = try XCTUnwrap(source
            .range(of: "}elseifvm.templates.isEmpty{RecurringEmptyState(onCreateNew:onCreateNew)}"))
        let branch = source[paused.upperBound ..< empty.lowerBound]
        XCTAssertTrue(branch.contains("BenefitsPausedNotice()"))
        XCTAssertFalse(branch.contains("onCreateNew"))
        XCTAssertTrue(source.contains("ifshowPausedNotice{BenefitsPausedNotice()}"), "the notice above the list")
        XCTAssertTrue(source
            .contains("ifvm.affordances.showCreateAction{CleansiaPrimaryButton(L10n.Recurring.createFab"))
    }

    /// Android's copy, verbatim, in all five languages.
    func testThePausedNoticeReadsAsAndroidsInEveryLanguage() throws {
        let titles = [
            "en": "Recurring paused — Plus payment failed",
            "cs": "Opakování pozastaveno — platba za Plus selhala",
            "sk": "Opakovanie pozastavené — platba za Plus zlyhala",
            "uk": "Регулярні призупинено — оплата Plus не вдалася",
            "ru": "Регулярные приостановлены — оплата Plus не прошла"
        ]
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for (language, title) in titles {
            L10n.bundle = try localeBundle(language)
            XCTAssertEqual(L10n.Recurring.pausedNoticeTitle, title, language)
            XCTAssertNotEqual(L10n.Recurring.pausedNoticeBody, "recurring_paused_notice_body", language)
        }
        L10n.bundle = try localeBundle("en")
        XCTAssertEqual(
            L10n.Recurring.pausedNoticeBody,
            "We couldn't charge your Plus renewal, so your recurring schedules book no new cleanings, and none can "
                + "be added or changed. They start booking again if a retried payment goes through."
        )
    }

    /// Nothing replaces the empty state for a member, so its own create CTA renders.
    func testAMemberWithNoSchedulesKeepsTheEmptyStateCreateCta() {
        let affordances = RecurringListAffordances.of(gate: .allowed, hasTemplates: false)

        XCTAssertFalse(affordances.showPlusUpsell)
        XCTAssertFalse(affordances.showCreateAction)
    }

    // MARK: - The retired entry's line

    /// The line sends the customer to the edit form only where the card offers Edit (owner decision
    /// 2026-10-05): a member gets "— edit to update", a lapsed or paused member, whose card has no Edit,
    /// the plain fact, in all five languages.
    func testTheRetiredLineAsksForAnEditOnlyWhereTheCardOffersOne() throws {
        let expected: [String: (edit: String, noEdit: String)] = [
            "en": ("Includes a service no longer offered — edit to update", "Includes a service no longer offered"),
            "cs": (
                "Obsahuje službu, kterou už nenabízíme — upravte objednávku",
                "Obsahuje službu, kterou už nenabízíme"
            ),
            "sk": ("Obsahuje službu, ktorú už neponúkame — upravte objednávku", "Obsahuje službu, ktorú už neponúkame"),
            "uk": (
                "Містить послугу, яку ми більше не пропонуємо — змініть бронювання",
                "Містить послугу, яку ми більше не пропонуємо"
            ),
            "ru": (
                "Содержит услугу, которую мы больше не предлагаем — измените бронирование",
                "Содержит услугу, которую мы больше не предлагаем"
            )
        ]
        let member = RecurringListAffordances.of(gate: .allowed, hasTemplates: true)
        let lapsed = RecurringListAffordances.of(gate: .upsell, hasTemplates: true)
        let paused = RecurringListAffordances.of(gate: .paused, hasTemplates: true)
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for (language, lines) in expected {
            L10n.bundle = try localeBundle(language)
            XCTAssertEqual(L10n.Recurring.cardItemNoLongerOffered(canEdit: member.showEdit), lines.edit, language)
            XCTAssertEqual(L10n.Recurring.cardItemNoLongerOffered(canEdit: lapsed.showEdit), lines.noEdit, language)
            XCTAssertEqual(L10n.Recurring.cardItemNoLongerOffered(canEdit: paused.showEdit), lines.noEdit, language)
        }
    }

    /// The card picks the line from the same `showEdit` that draws its Edit action.
    func testTheCardPicksTheLineFromItsOwnEditAffordance() throws {
        let source = try compactScreenSource()

        XCTAssertTrue(source.contains("text:L10n.Recurring.cardItemNoLongerOffered(canEdit:showEdit)"))
        XCTAssertTrue(source.contains("ifshowEdit{CardAction(label:L10n.Recurring.edit,"))
    }

    private func compactScreenSource() throws -> String {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        return try String(
            contentsOf: root.appendingPathComponent("Sources/Features/Recurring/RecurringBookingsScreen.swift"),
            encoding: .utf8
        ).components(separatedBy: .whitespacesAndNewlines).joined()
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
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
        XCTAssertEqual(RecurringAuthoringGate.resolve(hasMembership: true, benefitsPaused: true), .paused)
        XCTAssertEqual(RecurringAuthoringGate.resolve(hasMembership: true, benefitsPaused: false), .allowed)
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
