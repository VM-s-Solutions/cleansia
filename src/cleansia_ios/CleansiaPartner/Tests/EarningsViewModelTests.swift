import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class EarningsViewModelTests: XCTestCase {
    private final class FakeDashboardClient: PartnerDashboardClient {
        var statsResult: ApiResult<DashboardStatsDto> = .success(DashboardStatsDto())
        var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
        private(set) var statsCallCount = 0
        private(set) var statsEmployeeId: String??

        func getStats(employeeId: String?) async -> ApiResult<DashboardStatsDto> {
            statsCallCount += 1
            statsEmployeeId = .some(employeeId)
            return statsResult
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

    private func makeViewModel() -> EarningsViewModel {
        EarningsViewModel(client: client)
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeViewModel().state.isLoading)
    }

    func testLoadMapsStatsToLoaded() async {
        client.statsResult = .success(DashboardStatsDto(weekEarnings: 6262, currencyCode: "CZK"))

        let vm = makeViewModel()
        await vm.load()

        guard let stats = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(stats.weekEarnings, 6262)
        XCTAssertEqual(stats.currencyCode, "CZK")
    }

    func testLoadResolvesOwnEmployeeIdForStats() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.statsResult = .success(DashboardStatsDto())

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(client.statsEmployeeId, .some(.some("emp-1")))
    }

    func testLoadFailureMapsToError() async {
        client.statsResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
    }

    func testRetryAfterErrorKeepsPriorLoadedOnSecondFailure() async {
        client.statsResult = .success(DashboardStatsDto(weekEarnings: 100, currencyCode: "CZK"))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertNotNil(vm.state.loadedValue)

        client.statsResult = .failure(ApiError(httpStatus: 500))
        await vm.load()

        guard let stats = vm.state.loadedValue else {
            return XCTFail("expected the prior loaded stats to be retained on refresh failure")
        }
        XCTAssertEqual(stats.weekEarnings, 100)
    }
}
