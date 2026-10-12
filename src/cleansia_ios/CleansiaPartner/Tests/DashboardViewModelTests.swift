import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class DashboardViewModelTests: XCTestCase {
    @MainActor final class FakeDashboardClient: PartnerDashboardClient {
        var statsResult: ApiResult<DashboardStats> = .success(.stub())
        var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
        var previewResult: ApiResult<AvailableJobsPreview> = .success(
            AvailableJobsPreview(totalAvailableCount: 0, totalPotentialEarnings: 0)
        )
        var upcomingResult: ApiResult<[OrderListItem]> = .success([])
        private(set) var statsEmployeeId: String??
        private(set) var previewLimit: Int?
        private(set) var upcomingLimit: Int?
        private(set) var statsEmployeeIds: [String?] = []
        private(set) var upcomingEmployeeIds: [String] = []
        private var employeeCalls = 0
        private var previewCalls = 0
        var holdEmployee = false
        var holdStats = false
        var holdPreview = false
        var holdUpcoming = false
        var employeeStarted: ((Int) -> Void)?
        var statsStarted: ((Int) -> Void)?
        var previewStarted: ((Int) -> Void)?
        var upcomingStarted: ((Int) -> Void)?
        var employeeCompleted: ((Int) -> Void)?
        var statsCompleted: ((Int) -> Void)?
        var previewCompleted: ((Int) -> Void)?
        var upcomingCompleted: ((Int) -> Void)?
        private var heldEmployee: [Int: CheckedContinuation<ApiResult<EmployeeItem>, Never>] = [:]
        private var heldStats: [Int: CheckedContinuation<ApiResult<DashboardStats>, Never>] = [:]
        private var heldPreview: [Int: CheckedContinuation<ApiResult<AvailableJobsPreview>, Never>] = [:]
        private var heldUpcoming: [Int: CheckedContinuation<ApiResult<[OrderListItem]>, Never>] = [:]

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

        func getUpcomingOrders(employeeId: String, limit: Int) async -> ApiResult<[OrderListItem]> {
            upcomingEmployeeIds.append(employeeId)
            upcomingLimit = limit
            let call = upcomingEmployeeIds.count
            defer { upcomingCompleted?(call) }
            if holdUpcoming {
                return await withCheckedContinuation { continuation in
                    heldUpcoming[call] = continuation
                    upcomingStarted?(call)
                }
            }
            upcomingStarted?(call)
            return upcomingResult
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

        func releaseUpcoming(call: Int, result: ApiResult<[OrderListItem]>) {
            heldUpcoming.removeValue(forKey: call)?.resume(returning: result)
        }

        func releaseAll() {
            holdEmployee = false
            holdStats = false
            holdPreview = false
            holdUpcoming = false
            let employee = heldEmployee.values
            let stats = heldStats.values
            let preview = heldPreview.values
            let upcoming = heldUpcoming.values
            heldEmployee = [:]
            heldStats = [:]
            heldPreview = [:]
            heldUpcoming = [:]
            employee.forEach { $0.resume(returning: employeeResult) }
            stats.forEach { $0.resume(returning: statsResult) }
            preview.forEach { $0.resume(returning: previewResult) }
            upcoming.forEach { $0.resume(returning: upcomingResult) }
        }
    }

    var client: FakeDashboardClient!
    var settings: UserDefaultsAppSettingsStore!
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

    private var clock = Date(timeIntervalSince1970: 1_780_000_000)

    func makeViewModel() -> DashboardViewModel {
        DashboardViewModel(client: client, settings: settings, staleness: Staleness(window: 60, now: { self.clock }))
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

    func testUpcomingIsReadForTheCapturedEmployeeWithLimitTen() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(client.upcomingEmployeeIds, ["emp-1"])
        XCTAssertEqual(client.upcomingLimit, 10)
    }

    func testTheCleanersActiveJobIsTheHeroOverAvailableWork() async {
        let startsAt = Date(timeIntervalSinceNow: 3600)
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.previewResult = .success(AvailableJobsPreview(totalAvailableCount: 2, totalPotentialEarnings: 650))
        client.upcomingResult = .success([
            .upcoming(id: "order-7", status: 3, startsAt: startsAt, customerName: "Jana", address: "Praha 5")
        ])

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(
            vm.state.loadedValue?.hero,
            .nextJob(orderId: "order-7", status: ._3, startsAt: startsAt, whereLine: "Jana · Praha 5")
        )
    }

    /// Android skips the read without an id; a failed or blank employee read leaves the fetch none to pass.
    func testWithNoEmployeeIdUpcomingIsNotReadAndTheBoardIsTheHero() async {
        client.previewResult = .success(AvailableJobsPreview(totalAvailableCount: 2, totalPotentialEarnings: 650))
        for employee: ApiResult<EmployeeItem> in [
            .failure(ApiError(httpStatus: 500)),
            .success(EmployeeItem(id: " "))
        ] {
            client.employeeResult = employee
            let vm = makeViewModel()
            await vm.load()

            XCTAssertEqual(vm.state.loadedValue?.hero, .availableWork(jobCount: 2, potentialEarnings: 650))
            XCTAssertNil(vm.state.loadedValue?.todaysJobsCount, "an unread list is not a free day")
        }
        XCTAssertTrue(client.upcomingEmployeeIds.isEmpty)
    }

    func testAFailedUpcomingReadStillLoadsWithTheBoardAsTheHero() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.previewResult = .success(AvailableJobsPreview(totalAvailableCount: 2, totalPotentialEarnings: 650))
        client.upcomingResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(client.upcomingEmployeeIds, ["emp-1"])
        XCTAssertEqual(vm.state.loadedValue?.hero, .availableWork(jobCount: 2, potentialEarnings: 650))
        XCTAssertNil(vm.state.loadedValue?.todaysJobsCount, "a failed read is not a free day")
    }

    func testAnUpcomingListWithNothingTodayIsAFreeDay() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.upcomingResult = .success([
            .upcoming(id: "tomorrow", status: 2, startsAt: Date(timeIntervalSinceNow: 2 * 86400))
        ])

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.todaysJobsCount, 0)
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

    // MARK: - Returning to the tab

    /// Android keeps the dashboard for 60 s and refreshes it silently after that; iOS reloaded all four reads
    /// behind a full-screen spinner every time the tab came back.
    func testATabReturnWithinAMinuteReadsNothing() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        let vm = makeViewModel()
        await vm.load()
        var publications: [UiState<DashboardData>] = []
        let observation = vm.$state.dropFirst().sink { publications.append($0) }

        clock.addTimeInterval(59)
        await vm.load()
        observation.cancel()

        XCTAssertEqual(client.statsEmployeeIds.count, 1)
        XCTAssertEqual(client.upcomingEmployeeIds.count, 1)
        XCTAssertTrue(publications.isEmpty, "the return published \(publications.count) states")
        XCTAssertNotNil(vm.state.loadedValue)
    }

    func testATabReturnAfterAMinuteRefreshesInPlaceWithoutASpinner() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.statsResult = .success(.stub(weekEarnings: 111))
        let vm = makeViewModel()
        await vm.load()
        var publications: [UiState<DashboardData>] = []
        let observation = vm.$state.dropFirst().sink { publications.append($0) }

        clock.addTimeInterval(60)
        client.statsResult = .success(.stub(weekEarnings: 222))
        await vm.load()
        observation.cancel()

        XCTAssertEqual(client.statsEmployeeIds.count, 2)
        XCTAssertFalse(publications.contains { $0.isLoading }, "the refresh replaced the dashboard with a spinner")
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 222)
    }

    func testAPullAlwaysReadsWithinTheMinute() async {
        let vm = makeViewModel()
        await vm.load()

        await vm.userRefresh()

        XCTAssertEqual(client.statsEmployeeIds.count, 2)
    }

    func testARetryAfterAFailedLoadReadsAgainWithinTheMinute() async {
        client.statsResult = .failure(ApiError(httpStatus: 500))
        let vm = makeViewModel()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }

        client.statsResult = .success(.stub(weekEarnings: 100))
        await vm.load()

        XCTAssertEqual(client.statsEmployeeIds.count, 2)
        XCTAssertEqual(vm.state.loadedValue?.weekEarnings, 100)
    }
}
