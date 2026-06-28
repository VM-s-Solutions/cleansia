import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class InvoicesListViewModelTests: XCTestCase {
    private var client: FakePayrollClient!
    private var snackbar: SnackbarController!
    private var clock: Date!
    private var staleness: InvoicesStaleness!

    override func setUp() {
        super.setUp()
        client = FakePayrollClient()
        snackbar = SnackbarController()
        clock = Date(timeIntervalSince1970: 1_000_000)
        staleness = InvoicesStaleness(window: 30, now: { self.clock })
    }

    override func tearDown() {
        client = nil
        snackbar = nil
        clock = nil
        staleness = nil
        super.tearDown()
    }

    private func makeViewModel() -> InvoicesListViewModel {
        InvoicesListViewModel(client: client, staleness: staleness, snackbar: snackbar)
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeViewModel().state.isLoading)
    }

    func testOnAppearResolvesOwnEmployeeIdAndMapsToLoaded() async {
        client.employeeIdResult = .success("emp-1")
        client.invoicesResult = .success([EmployeeInvoiceDto(id: "inv-1", totalAmount: 4200)])

        let vm = makeViewModel()
        await vm.onAppear()

        guard let invoices = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(invoices.count, 1)
        XCTAssertEqual(client.invoicesEmployeeId, "emp-1")
    }

    func testEmptyResultMapsToLoadedEmpty() async {
        client.invoicesResult = .success([])

        let vm = makeViewModel()
        await vm.onAppear()

        XCTAssertEqual(vm.state.loadedValue?.isEmpty, true)
    }

    func testMissingEmployeeIdMapsToEmptyWithoutNetworkCall() async {
        client.employeeIdResult = .failure(ApiError(code: "payroll.employee_id_missing"))

        let vm = makeViewModel()
        await vm.onAppear()

        XCTAssertEqual(vm.state.loadedValue?.isEmpty, true)
        XCTAssertEqual(client.invoicesCallCount, 0)
    }

    func testApiErrorMapsToErrorAndShowsSnackbar() async {
        client.invoicesResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.onAppear()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testUserRefreshFailureKeepsPriorLoaded() async {
        client.invoicesResult = .success([EmployeeInvoiceDto(id: "inv-1", totalAmount: 100)])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(vm.state.loadedValue?.count, 1)

        client.invoicesResult = .failure(ApiError(httpStatus: 500))
        await vm.userRefresh()

        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }

    // A warm cache makes the next on-appear (resume) a silent no-op —
    // no second network round-trip while inside the freshness window.
    func testOnAppearSkipsNetworkWhileCacheIsWarm() async {
        client.invoicesResult = .success([EmployeeInvoiceDto(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        // Still inside the 30s window — resume must not re-fetch.
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)
    }

    // Once the watermark goes stale, the next on-appear silently
    // re-fetches (the silent-stale resume).
    func testOnAppearRefetchesAfterWatermarkGoesStale() async {
        client.invoicesResult = .success([EmployeeInvoiceDto(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        clock = clock.addingTimeInterval(31) // past the 30s window
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 2)
    }

    // Invalidating the watermark forces a re-fetch even while warm.
    func testInvalidateForcesRefetchEvenWhileWarm() async {
        client.invoicesResult = .success([EmployeeInvoiceDto(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        staleness.invalidate()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 2)
    }

    /// A user pull always re-fetches, bypassing the warm watermark.
    func testUserRefreshAlwaysFetchesEvenWhileWarm() async {
        client.invoicesResult = .success([EmployeeInvoiceDto(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        await vm.userRefresh()
        XCTAssertEqual(client.invoicesCallCount, 2)
    }
}
