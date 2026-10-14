import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
extension DashboardViewModelTests {
    func testPreviewStartsWhileEmployeeIsHeldAndFinalLoadWaitsForIt() async {
        let employeeEntered = expectation(description: "employee entered")
        let previewEntered = expectation(description: "preview entered")
        let statsEntered = expectation(description: "captured employee stats entered")
        let statsCompleted = expectation(description: "stats completed")
        let completed = expectation(description: "employee and preview completed")
        completed.expectedFulfillmentCount = 2
        client.holdEmployee = true
        client.holdStats = true
        client.holdPreview = true
        client.employeeStarted = { _ in employeeEntered.fulfill() }
        client.previewStarted = { _ in previewEntered.fulfill() }
        client.statsStarted = { _ in statsEntered.fulfill() }
        client.statsCompleted = { _ in statsCompleted.fulfill() }
        client.employeeCompleted = { _ in completed.fulfill() }
        client.previewCompleted = { _ in completed.fulfill() }
        let vm = makeViewModel()
        var loadedPublications = 0
        var finished = false
        let observation = vm.$state.dropFirst().sink { state in
            if case .loaded = state { loadedPublications += 1 }
        }
        let load = Task {
            await vm.load()
            finished = true
        }
        await fulfillment(of: [employeeEntered, previewEntered], timeout: 2)
        XCTAssertTrue(client.statsEmployeeIds.isEmpty)
        XCTAssertTrue(vm.state.isLoading)
        client.releaseEmployee(call: 1, result: .success(EmployeeItem(id: "captured", firstName: "Jana")))
        await fulfillment(of: [statsEntered], timeout: 2)
        XCTAssertEqual(client.statsEmployeeIds, ["captured"])
        client.releaseStats(call: 1, result: .success(.stub(weekEarnings: 6262)))
        await fulfillment(of: [statsCompleted], timeout: 2)
        XCTAssertTrue(vm.state.isLoading)
        XCTAssertFalse(finished)
        client.releasePreview(call: 1, result: .success(AvailableJobsPreview(
            totalAvailableCount: 2, totalPotentialEarnings: 650
        )))
        client.releaseAll()
        await load.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 6262)
        XCTAssertEqual(vm.state.loadedValue?.hero, .availableWork(jobCount: 2, potentialEarnings: 650))
        XCTAssertEqual(loadedPublications, 1)
        observation.cancel()
        load.cancel()
    }

    func testSupersededFetchCannotOverwriteCurrentDataOrSpendItsPrompt() async {
        let oldStatsEntered = expectation(description: "old stats entered")
        let newStatsEntered = expectation(description: "new stats entered")
        let completed = expectation(description: "four optional and stats legs completed")
        completed.expectedFulfillmentCount = 4
        client.holdStats = true
        client.employeeResult = .success(EmployeeItem(id: "old", firstName: "Old", jobRadiusKm: 25))
        client.statsStarted = { call in
            if call == 1 { oldStatsEntered.fulfill() } else { newStatsEntered.fulfill() }
        }
        client.statsCompleted = { _ in completed.fulfill() }
        client.previewCompleted = { _ in completed.fulfill() }
        let vm = makeViewModel()
        let old = Task { await vm.load() }
        await fulfillment(of: [oldStatsEntered], timeout: 2)
        client.employeeResult = .success(EmployeeItem(id: "new", firstName: "New", jobRadiusKm: nil))
        let new = Task { await vm.userRefresh() }
        await fulfillment(of: [newStatsEntered], timeout: 2)
        XCTAssertEqual(client.statsEmployeeIds, ["old", "new"])
        client.releaseStats(call: 2, result: .success(.stub(weekEarnings: 222)))
        if client.statsEmployeeIds.count != 2 { client.releaseAll() }
        await new.value
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222)
        XCTAssertTrue(vm.showsJobRadiusPrompt)
        client.releaseStats(call: 1, result: .success(.stub(weekEarnings: 111)))
        client.releaseAll()
        await old.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222)
        XCTAssertEqual(vm.state.loadedValue?.firstName, "New")
        XCTAssertTrue(vm.showsJobRadiusPrompt)
        XCTAssertFalse(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "new"))
        vm.answerJobRadiusPrompt()
        XCTAssertTrue(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "new"))
        XCTAssertFalse(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "old"))
        old.cancel()
        new.cancel()
    }

    func testCancelledUserRefreshRetainsLoadedDataAndDoesNotPromptNewEmployee() async {
        client.employeeResult = .success(EmployeeItem(id: "prime", jobRadiusKm: 25))
        client.statsResult = .success(.stub(weekEarnings: 111))
        let vm = makeViewModel()
        await vm.load()
        let employeeEntered = expectation(description: "cancelled refresh employee entered")
        let previewEntered = expectation(description: "cancelled refresh preview entered")
        let completed = expectation(description: "cancelled refresh legs completed")
        completed.expectedFulfillmentCount = 2
        client.employeeResult = .success(EmployeeItem(id: "cancelled", jobRadiusKm: nil))
        client.statsResult = .success(.stub(weekEarnings: 999))
        client.holdEmployee = true
        client.holdPreview = true
        client.employeeStarted = { call in if call == 2 { employeeEntered.fulfill() } }
        client.previewStarted = { call in if call == 2 { previewEntered.fulfill() } }
        client.employeeCompleted = { call in if call == 2 { completed.fulfill() } }
        client.previewCompleted = { call in if call == 2 { completed.fulfill() } }
        let refresh = Task { await vm.userRefresh() }
        await fulfillment(of: [employeeEntered, previewEntered], timeout: 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 111)
        refresh.cancel()
        client.releaseAll()
        await refresh.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 111)
        XCTAssertFalse(vm.showsJobRadiusPrompt)
        XCTAssertEqual(client.statsEmployeeIds, ["prime"])
        XCTAssertEqual(client.upcomingEmployeeIds, ["prime"], "the cancelled refresh read upcoming orders")
        XCTAssertFalse(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "cancelled"))
    }

    func testCancelledRefreshAfterEmployeeReadKeepsVisiblePromptOwner() async {
        client.employeeResult = .success(EmployeeItem(id: "visible", jobRadiusKm: nil))
        client.statsResult = .success(.stub(weekEarnings: 111))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertTrue(vm.showsJobRadiusPrompt)
        let employeeCompleted = expectation(description: "replacement employee completed")
        let statsEntered = expectation(description: "replacement stats entered")
        let previewEntered = expectation(description: "replacement preview entered")
        let completed = expectation(description: "cancelled stats and preview completed")
        completed.expectedFulfillmentCount = 2
        client.employeeResult = .success(EmployeeItem(id: "replacement", jobRadiusKm: nil))
        client.statsResult = .success(.stub(weekEarnings: 999))
        client.holdStats = true
        client.holdPreview = true
        client.employeeCompleted = { call in if call == 2 { employeeCompleted.fulfill() } }
        client.statsStarted = { call in if call == 2 { statsEntered.fulfill() } }
        client.previewStarted = { call in if call == 2 { previewEntered.fulfill() } }
        client.statsCompleted = { call in if call == 2 { completed.fulfill() } }
        client.previewCompleted = { call in if call == 2 { completed.fulfill() } }
        let refresh = Task { await vm.userRefresh() }
        await fulfillment(of: [employeeCompleted, statsEntered, previewEntered], timeout: 2)
        XCTAssertEqual(client.statsEmployeeIds, ["visible", "replacement"])
        refresh.cancel()
        client.releaseAll()
        await refresh.value
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 111)
        XCTAssertTrue(vm.showsJobRadiusPrompt)
        vm.answerJobRadiusPrompt()
        XCTAssertTrue(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "visible"))
        XCTAssertFalse(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "replacement"))
    }

    /// The slowest read decides when the dashboard appears, so the hero never swaps under the cleaner.
    func testTheDashboardIsPublishedOnceUpcomingHasArrived() async {
        let upcomingEntered = expectation(description: "upcoming entered")
        let othersCompleted = expectation(description: "employee, stats and preview completed")
        othersCompleted.expectedFulfillmentCount = 3
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.holdUpcoming = true
        client.upcomingStarted = { _ in upcomingEntered.fulfill() }
        client.employeeCompleted = { _ in othersCompleted.fulfill() }
        client.statsCompleted = { _ in othersCompleted.fulfill() }
        client.previewCompleted = { _ in othersCompleted.fulfill() }
        let vm = makeViewModel()
        var loadedPublications = 0
        let observation = vm.$state.dropFirst().sink { state in
            if case .loaded = state { loadedPublications += 1 }
        }
        let load = Task { await vm.load() }
        await fulfillment(of: [upcomingEntered, othersCompleted], timeout: 2)
        XCTAssertTrue(vm.state.isLoading)
        XCTAssertEqual(loadedPublications, 0)
        let startsAt = Date(timeIntervalSinceNow: 7200)
        client.releaseUpcoming(call: 1, result: .success([.upcoming(id: "order-1", status: 2, startsAt: startsAt)]))
        client.releaseAll()
        await load.value
        XCTAssertEqual(loadedPublications, 1)
        XCTAssertEqual(
            vm.state.loadedValue?.hero,
            .nextJob(orderId: "order-1", status: ._2, startsAt: startsAt, whereLine: nil)
        )
        observation.cancel()
    }

    func testASupersededFetchsUpcomingOrdersNeverReachTheDashboard() async {
        let oldEntered = expectation(description: "old upcoming entered")
        let newEntered = expectation(description: "new upcoming entered")
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.holdUpcoming = true
        client.upcomingStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let vm = makeViewModel()
        let old = Task { await vm.load() }
        await fulfillment(of: [oldEntered], timeout: 2)
        let new = Task { await vm.userRefresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseUpcoming(call: 2, result: .success([]))
        await new.value
        XCTAssertEqual(vm.state.loadedValue?.hero, .empty)
        let stale = OrderListItem.upcoming(id: "stale", status: 4, startsAt: Date(timeIntervalSinceNow: 600))
        client.releaseUpcoming(call: 1, result: .success([stale]))
        client.releaseAll()
        await old.value
        XCTAssertEqual(vm.state.loadedValue?.hero, .empty)
        XCTAssertEqual(client.upcomingEmployeeIds, ["emp-1", "emp-1"])
    }

    func testMutationDuringEachHeldDashboardLegCannotPublishOrRewarm() async throws {
        for leg in ["stats", "preview", "upcoming"] {
            client = FakeDashboardClient()
            client.employeeResult = .success(EmployeeItem(id: "visible", firstName: "Visible", jobRadiusKm: nil))
            client.statsResult = .success(.stub(weekEarnings: 111))
            let shared = OrdersStaleness(now: { self.clock })
            let vm = makeViewModel(staleness: shared)
            await vm.load()
            let lastGood = vm.state.loadedValue
            let entered = expectation(description: "held \(leg) entered")
            switch leg {
            case "stats":
                client.holdStats = true
                client.statsStarted = { call in if call == 2 { entered.fulfill() } }
            case "preview":
                client.holdPreview = true
                client.previewStarted = { call in if call == 2 { entered.fulfill() } }
            default:
                client.holdUpcoming = true
                client.upcomingStarted = { call in if call == 2 { entered.fulfill() } }
            }
            client.employeeResult = .success(EmployeeItem(id: "obsolete", firstName: "Obsolete", jobRadiusKm: 25))
            client.statsResult = .success(.stub(weekEarnings: 999))
            let old = Task { await vm.userRefresh() }
            await fulfillment(of: [entered], timeout: 2)
            GeneratedWireSpine.install(recording: WireBodies()) { _ in (200, Data("{}".utf8)) }
            _ = try await LivePartnerOrderClient(staleness: shared).startOrder(orderId: "order-1").get()
            XCTAssertTrue(shared.isDashboardStale, leg)
            client.releaseAll()
            await old.value
            XCTAssertEqual(vm.state.loadedValue, lastGood, leg)
            XCTAssertTrue(shared.isDashboardStale, leg)
            vm.answerJobRadiusPrompt()
            XCTAssertTrue(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "visible"), leg)
            XCTAssertFalse(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "obsolete"), leg)
            client.employeeResult = .success(EmployeeItem(id: "visible", jobRadiusKm: 25))
            client.statsResult = .success(.stub(weekEarnings: 222))
            await vm.load()
            XCTAssertEqual(client.statsEmployeeIds.count, 3, leg)
            XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222, leg)
            XCTAssertFalse(shared.isDashboardStale, leg)
            GenMockURLProtocol.handler = nil
        }
    }

    func testPostMutationDashboardRemainsCurrentWhenOlderFetchAnswersLast() async throws {
        client.employeeResult = .success(EmployeeItem(id: "visible", jobRadiusKm: 25))
        let shared = OrdersStaleness(now: { self.clock })
        let vm = makeViewModel(staleness: shared)
        await vm.load()
        let entered = expectation(description: "pre-mutation stats entered")
        client.holdStats = true
        client.statsStarted = { call in if call == 2 { entered.fulfill() } }
        let old = Task { await vm.userRefresh() }
        await fulfillment(of: [entered], timeout: 2)
        GeneratedWireSpine.install(recording: WireBodies()) { _ in (200, Data("{}".utf8)) }
        defer { GenMockURLProtocol.handler = nil }
        _ = try await LivePartnerOrderClient(staleness: shared).startOrder(orderId: "order-1").get()
        client.holdStats = false
        client.statsResult = .success(.stub(weekEarnings: 222))
        await vm.load()
        client.releaseStats(call: 2, result: .success(.stub(weekEarnings: 999)))
        client.releaseAll()
        await old.value
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222)
        XCTAssertFalse(shared.isDashboardStale)
        XCTAssertEqual(client.statsEmployeeIds.count, 3)
    }

    func testRegisteredSessionClearRevokesAHeldDashboardPublication() async {
        client.employeeResult = .success(EmployeeItem(id: "old-account", jobRadiusKm: nil))
        client.statsResult = .success(.stub(weekEarnings: 111))
        let shared = OrdersStaleness(now: { self.clock })
        let registry = SessionScopedCacheRegistry()
        registry.register(shared)
        let vm = makeViewModel(staleness: shared)
        await vm.load()
        XCTAssertTrue(vm.showsJobRadiusPrompt)
        let entered = expectation(description: "old account stats entered")
        client.holdStats = true
        client.statsStarted = { call in if call == 2 { entered.fulfill() } }
        let old = Task { await vm.userRefresh() }
        await fulfillment(of: [entered], timeout: 2)
        await registry.clearAll()
        XCTAssertTrue(shared.isDashboardStale)
        client.releaseStats(call: 2, result: .success(.stub(weekEarnings: 999)))
        client.releaseAll()
        await old.value
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 111)
        XCTAssertTrue(shared.isDashboardStale)
        client.employeeResult = .success(EmployeeItem(id: "next-account", jobRadiusKm: nil))
        client.statsResult = .success(.stub(weekEarnings: 222))
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222)
        XCTAssertTrue(vm.showsJobRadiusPrompt)
        vm.answerJobRadiusPrompt()
        XCTAssertTrue(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "next-account"))
        XCTAssertFalse(settings.hasAnsweredPrompt(JobRadiusPrompt.settingsKey, userId: "old-account"))
    }

    func testMaterialWriteRestalesAWarmDashboardWithinTheMinute() async throws {
        let shared = OrdersStaleness(now: { self.clock })
        let vm = makeViewModel(staleness: shared)
        await vm.load()
        let bodies = WireBodies()
        GeneratedWireSpine.install(recording: bodies) { _ in (200, Data("{}".utf8)) }
        defer { GenMockURLProtocol.handler = nil }
        _ = try await LivePartnerOrderClient(staleness: shared).startOrder(orderId: "order-1").get()
        client.statsResult = .success(.stub(weekEarnings: 222))
        await vm.load()
        XCTAssertEqual(client.statsEmployeeIds.count, 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222)
    }
}
