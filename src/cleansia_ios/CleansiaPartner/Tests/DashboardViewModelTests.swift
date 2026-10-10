import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class DashboardViewModelTests: XCTestCase {
    @MainActor private final class FakeDashboardClient: PartnerDashboardClient {
        var statsResult: ApiResult<DashboardStats> = .success(.stub())
        var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
        var previewResult: ApiResult<AvailableJobsPreview> = .success(
            AvailableJobsPreview(totalAvailableCount: 0, totalPotentialEarnings: 0)
        )
        private(set) var statsEmployeeId: String??
        private(set) var previewLimit: Int?
        private(set) var statsEmployeeIds: [String?] = []
        private var employeeCalls = 0
        private var previewCalls = 0
        var holdEmployee = false
        var holdStats = false
        var holdPreview = false
        var employeeStarted: ((Int) -> Void)?
        var statsStarted: ((Int) -> Void)?
        var previewStarted: ((Int) -> Void)?
        var employeeCompleted: ((Int) -> Void)?
        var statsCompleted: ((Int) -> Void)?
        var previewCompleted: ((Int) -> Void)?
        private var heldEmployee: [Int: CheckedContinuation<ApiResult<EmployeeItem>, Never>] = [:]
        private var heldStats: [Int: CheckedContinuation<ApiResult<DashboardStats>, Never>] = [:]
        private var heldPreview: [Int: CheckedContinuation<ApiResult<AvailableJobsPreview>, Never>] = [:]

        func getStats(employeeId: String?) async -> ApiResult<DashboardStats> {
            statsEmployeeId = .some(employeeId)
            statsEmployeeIds.append(employeeId)
            let call = statsEmployeeIds.count
            defer { statsCompleted?(call) }
            if holdStats {
                return await withCheckedContinuation { continuation in
                    heldStats[call] = continuation
                    statsStarted?(call)
                }
            }
            statsStarted?(call)
            return statsResult
        }

        func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreview> {
            previewLimit = limit
            previewCalls += 1
            let call = previewCalls
            defer { previewCompleted?(call) }
            if holdPreview {
                return await withCheckedContinuation { continuation in
                    heldPreview[call] = continuation
                    previewStarted?(call)
                }
            }
            previewStarted?(call)
            return previewResult
        }

        func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
            employeeCalls += 1
            let call = employeeCalls
            defer { employeeCompleted?(call) }
            if holdEmployee {
                return await withCheckedContinuation { continuation in
                    heldEmployee[call] = continuation
                    employeeStarted?(call)
                }
            }
            employeeStarted?(call)
            return employeeResult
        }

        func releaseEmployee(call: Int, result: ApiResult<EmployeeItem>) {
            heldEmployee.removeValue(forKey: call)?.resume(returning: result)
        }

        func releaseStats(call: Int, result: ApiResult<DashboardStats>) {
            heldStats.removeValue(forKey: call)?.resume(returning: result)
        }

        func releasePreview(call: Int, result: ApiResult<AvailableJobsPreview>) {
            heldPreview.removeValue(forKey: call)?.resume(returning: result)
        }

        func releaseAll() {
            holdEmployee = false
            holdStats = false
            holdPreview = false
            let employee = heldEmployee.values
            let stats = heldStats.values
            let preview = heldPreview.values
            heldEmployee = [:]
            heldStats = [:]
            heldPreview = [:]
            employee.forEach { $0.resume(returning: employeeResult) }
            stats.forEach { $0.resume(returning: statsResult) }
            preview.forEach { $0.resume(returning: previewResult) }
        }
    }

    private var client: FakeDashboardClient!
    private var settings: UserDefaultsAppSettingsStore!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        client = FakeDashboardClient()
        suiteName = "DashboardViewModelTests.\(UUID().uuidString)"
        settings = UserDefaultsAppSettingsStore(defaults: UserDefaults(suiteName: suiteName) ?? .standard)
    }

    override func tearDown() {
        UserDefaults().removePersistentDomain(forName: suiteName)
        settings = nil
        suiteName = nil
        client = nil
        super.tearDown()
    }

    private func makeViewModel() -> DashboardViewModel {
        DashboardViewModel(client: client, settings: settings)
    }

    func testInitialStateIsLoading() {
        let vm = makeViewModel()
        XCTAssertTrue(vm.state.isLoading)
    }

    func testStatsSuccessMapsToLoaded() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "Jana"))
        client.statsResult = .success(.stub(
            weekEarnings: 6262,
            weekCompletedCount: 4,
            lastMonthEarnings: 18000,
            lastMonthCompletedOrders: 4,
            thisMonthCompletedOrders: 5,
            currencyCode: "CZK"
        ))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.firstName, "Jana")
        XCTAssertEqual(data.weekEarnings, 6262)
        XCTAssertEqual(data.weekCompletedCount, 4)
        XCTAssertEqual(data.lastMonthEarnings, 18000)
        XCTAssertEqual(data.currencyCode, "CZK")
        XCTAssertEqual(client.statsEmployeeId, .some(.some("emp-1")))
    }

    func testStatsFailureMapsToError() async {
        client.statsResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
    }

    func testFirstNameSubCallFailureStillLoadsWithFallbackGreeting() async {
        client.employeeResult = .failure(ApiError(code: "network.unreachable"))
        client.statsResult = .success(.stub(weekEarnings: 100, currencyCode: "CZK"))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded despite employee failure") }
        XCTAssertNil(data.firstName)
        XCTAssertEqual(data.weekEarnings, 100)
        XCTAssertEqual(client.statsEmployeeId, .some(.none))
    }

    /// A genuinely quiet week is zeros the server sent, and it renders. What must NOT render as a
    /// quiet week is a payload that carried no figures at all — that case is refused one layer down
    /// and is driven in `PartnerWireContractTests`.
    func testAnHonestlyEmptyWeekStillRenders() async {
        client.statsResult = .success(.stub())

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.weekEarnings, 0)
        XCTAssertEqual(data.weekCompletedCount, 0)
        XCTAssertNil(data.payPeriod)
        XCTAssertNil(data.averageRating)
    }

    func testAvailableJobsPreviewMapsToAvailableWorkHero() async {
        client.previewResult = .success(AvailableJobsPreview(
            totalAvailableCount: 2,
            totalPotentialEarnings: 650
        ))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.hero, .availableWork(jobCount: 2, potentialEarnings: 650))
        XCTAssertEqual(client.previewLimit, 5)
    }

    func testZeroAvailableJobsMapsToEmptyHero() async {
        client.previewResult = .success(AvailableJobsPreview(
            totalAvailableCount: 0,
            totalPotentialEarnings: 0
        ))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.hero, .empty)
    }

    func testACleanerWithNoRadiusIsPromptedUntilTheyAnswer() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", jobRadiusKm: nil))

        let vm = makeViewModel()
        await vm.load()
        XCTAssertTrue(vm.showsJobRadiusPrompt)

        vm.answerJobRadiusPrompt()
        XCTAssertFalse(vm.showsJobRadiusPrompt)

        let next = makeViewModel()
        await next.load()
        XCTAssertFalse(next.showsJobRadiusPrompt)
    }

    /// Keeping the country-wide board leaves the radius null, so the prompt has to be spent by the
    /// ANSWER — a gate re-derived from the stored value would ask this cleaner again every launch.
    func testKeepingEveryJobSpendsThePromptEvenThoughTheRadiusStaysNull() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", jobRadiusKm: nil))
        let vm = makeViewModel()
        await vm.load()
        vm.answerJobRadiusPrompt()

        let next = makeViewModel()
        await next.load()

        XCTAssertFalse(next.showsJobRadiusPrompt)
    }

    func testACleanerWhoAlreadySetARadiusIsNotPrompted() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", jobRadiusKm: 25))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertFalse(vm.showsJobRadiusPrompt)
    }

    func testThePromptIsKeyedPerCleanerSoASecondAccountOnTheDeviceStillGetsIt() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", jobRadiusKm: nil))
        let first = makeViewModel()
        await first.load()
        first.answerJobRadiusPrompt()

        client.employeeResult = .success(EmployeeItem(id: "emp-2", jobRadiusKm: nil))
        let other = makeViewModel()
        await other.load()

        XCTAssertTrue(other.showsJobRadiusPrompt)
    }

    /// The ask is not spent by an outage: a failed read cannot tell "no preference" from "unknown".
    func testAFailedEmployeeReadNeitherPromptsNorSpendsTheAsk() async {
        client.employeeResult = .failure(ApiError(httpStatus: 500))
        let failing = makeViewModel()
        await failing.load()
        XCTAssertFalse(failing.showsJobRadiusPrompt)

        client.employeeResult = .success(EmployeeItem(id: "emp-1", jobRadiusKm: nil))
        let recovered = makeViewModel()
        await recovered.load()

        XCTAssertTrue(recovered.showsJobRadiusPrompt)
    }

    func testPreviewFailureStillLoadsWithEmptyHero() async {
        client.statsResult = .success(.stub(weekEarnings: 100))
        client.previewResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded despite preview failure") }
        XCTAssertEqual(data.hero, .empty)
        XCTAssertEqual(data.weekEarnings, 100)
    }
}

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
}
