import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class EarningsViewModelTests: XCTestCase {
    private final class FakeDashboardClient: PartnerDashboardClient {
        var statsResult: ApiResult<DashboardStats> = .success(.stub())
        var employeeResult: ApiResult<EmployeeItem> = .success(EmployeeItem())
        private(set) var statsCallCount = 0
        private(set) var statsEmployeeId: String??

        func getStats(employeeId: String?) async -> ApiResult<DashboardStats> {
            statsCallCount += 1
            statsEmployeeId = .some(employeeId)
            return statsResult
        }

        func getUpcomingOrders(employeeId _: String, limit _: Int) async -> ApiResult<[OrderListItem]> {
            .success([])
        }

        func getAvailableJobsPreview(limit _: Int) async -> ApiResult<AvailableJobsPreview> {
            .success(AvailableJobsPreview(totalAvailableCount: 0, totalPotentialEarnings: 0))
        }

        func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
            employeeResult
        }
    }

    private var client: FakeDashboardClient!
    private var payrollClient: FakePayrollClient!
    private var snackbar: SnackbarController!

    private let held = [CashHeld(currencyCode: "CZK", amount: 3250.5, floatCap: 3000, cashJobsHidden: true)]

    override func setUp() {
        super.setUp()
        client = FakeDashboardClient()
        payrollClient = FakePayrollClient()
        snackbar = SnackbarController()
    }

    override func tearDown() {
        client = nil
        payrollClient = nil
        snackbar = nil
        super.tearDown()
    }

    private func makeViewModel() -> EarningsViewModel {
        EarningsViewModel(client: client, payrollClient: payrollClient, snackbar: snackbar)
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeViewModel().state.isLoading)
    }

    func testLoadMapsStatsToLoaded() async {
        client.statsResult = .success(.stub(weekEarnings: 6262, currencyCode: "CZK"))

        let vm = makeViewModel()
        await vm.load()

        guard let stats = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(stats.weekEarnings, 6262)
        XCTAssertEqual(stats.currencyCode, "CZK")
    }

    func testLoadResolvesOwnEmployeeIdForStats() async {
        client.employeeResult = .success(EmployeeItem(id: "emp-1"))
        client.statsResult = .success(.stub())

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
        client.statsResult = .success(.stub(weekEarnings: 100, currencyCode: "CZK"))
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

    // MARK: cash I hold

    func testLoadReadsTheCashTheCleanerHoldsBesideTheStats() async {
        payrollClient.cashHeldResult = .success(held)

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.cashHeld, held)
        XCTAssertNotNil(vm.state.loadedValue)
        XCTAssertNil(snackbar.current)
    }

    func testAFailedCashReadBesideLoadedStatsIsRaisedAndShowsNoCash() async {
        payrollClient.cashHeldResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.cashHeld, [])
        XCTAssertNotNil(vm.state.loadedValue)
        XCTAssertEqual(snackbar.current?.severity, .error)
    }

    /// The error state is the one telling; a snackbar on top of it would say the same thing twice.
    func testWhenBothReadsFailOnlyTheErrorStateSpeaks() async {
        client.statsResult = .failure(ApiError(httpStatus: 500))
        payrollClient.cashHeldResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNil(snackbar.current)
    }
}
