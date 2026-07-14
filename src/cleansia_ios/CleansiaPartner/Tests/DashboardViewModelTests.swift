import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class DashboardViewModelTests: XCTestCase {
    private final class FakeDashboardClient: PartnerDashboardClient {
        var statsResult: ApiResult<DashboardStatsDto> = .success(DashboardStatsDto())
        var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
        var previewResult: ApiResult<AvailableJobsPreviewResponse> = .success(AvailableJobsPreviewResponse())
        private(set) var statsEmployeeId: String??
        private(set) var previewLimit: Int?

        func getStats(employeeId: String?) async -> ApiResult<DashboardStatsDto> {
            statsEmployeeId = .some(employeeId)
            return statsResult
        }

        func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreviewResponse> {
            previewLimit = limit
            return previewResult
        }

        func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
            employeeResult
        }
    }

    private var client: FakeDashboardClient!

    override func setUp() {
        super.setUp()
        client = FakeDashboardClient()
    }

    override func tearDown() {
        client = nil
        super.tearDown()
    }

    private func makeViewModel() -> DashboardViewModel {
        DashboardViewModel(client: client)
    }

    func testInitialStateIsLoading() {
        let vm = makeViewModel()
        XCTAssertTrue(vm.state.isLoading)
    }

    func testStatsSuccessMapsToLoaded() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1", firstName: "Jana"))
        client.statsResult = .success(DashboardStatsDto(
            thisMonthCompletedOrders: 5,
            lastMonthCompletedOrders: 4,
            weekEarnings: 6262,
            weekCompletedCount: 4,
            lastMonthEarnings: 18000,
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
        client.statsResult = .success(DashboardStatsDto(weekEarnings: 100, currencyCode: "CZK"))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded despite employee failure") }
        XCTAssertNil(data.firstName)
        XCTAssertEqual(data.weekEarnings, 100)
        XCTAssertEqual(client.statsEmployeeId, .some(.none))
    }

    func testEmptyStatsUseZeroFallbacks() async {
        client.statsResult = .success(DashboardStatsDto())

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.weekEarnings, 0)
        XCTAssertEqual(data.weekCompletedCount, 0)
        XCTAssertNil(data.payPeriod)
        XCTAssertNil(data.averageRating)
    }

    func testAvailableJobsPreviewMapsToAvailableWorkHero() async {
        client.previewResult = .success(AvailableJobsPreviewResponse(
            totalPotentialEarnings: 650,
            totalAvailableCount: 2
        ))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(data.hero, .availableWork(jobCount: 2, potentialEarnings: 650))
        XCTAssertEqual(client.previewLimit, 5)
    }

    func testZeroAvailableJobsMapsToEmptyHero() async {
        client.previewResult = .success(AvailableJobsPreviewResponse(
            totalPotentialEarnings: 0,
            totalAvailableCount: 0
        ))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.hero, .empty)
    }

    func testPreviewFailureStillLoadsWithEmptyHero() async {
        client.statsResult = .success(DashboardStatsDto(weekEarnings: 100))
        client.previewResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard let data = vm.state.loadedValue else { return XCTFail("expected loaded despite preview failure") }
        XCTAssertEqual(data.hero, .empty)
        XCTAssertEqual(data.weekEarnings, 100)
    }
}
