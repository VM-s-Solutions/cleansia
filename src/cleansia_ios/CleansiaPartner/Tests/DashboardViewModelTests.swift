import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class DashboardViewModelTests: XCTestCase {
    private final class FakeDashboardClient: PartnerDashboardClient {
        var statsResult: ApiResult<DashboardStats> = .success(.stub())
        var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
        var previewResult: ApiResult<AvailableJobsPreview> = .success(
            AvailableJobsPreview(totalAvailableCount: 0, totalPotentialEarnings: 0)
        )
        private(set) var statsEmployeeId: String??
        private(set) var previewLimit: Int?

        func getStats(employeeId: String?) async -> ApiResult<DashboardStats> {
            statsEmployeeId = .some(employeeId)
            return statsResult
        }

        func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreview> {
            previewLimit = limit
            return previewResult
        }

        func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
            employeeResult
        }
    }

    private var client: FakeDashboardClient!
    private var settings: UserDefaultsAppSettingsStore!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        client = FakeDashboardClient()
        suiteName = "DashboardViewModelTests.\(UUID().uuidString)"
        settings = UserDefaultsAppSettingsStore(defaults: UserDefaults(suiteName: suiteName)!)
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
