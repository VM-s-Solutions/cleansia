import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class PeriodPayViewModelTests: XCTestCase {
    private var client: FakePayrollClient!
    private var snackbar: SnackbarController!

    override func setUp() {
        super.setUp()
        client = FakePayrollClient()
        snackbar = SnackbarController()
    }

    override func tearDown() {
        client = nil
        snackbar = nil
        super.tearDown()
    }

    private func makeViewModel(currencyId: String? = nil) -> PeriodPayViewModel {
        PeriodPayViewModel(
            payPeriodId: "pp-1",
            currencyCode: "CZK",
            currencyId: currencyId,
            client: client,
            snackbar: snackbar
        )
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeViewModel().state.isLoading)
    }

    func testLoadResolvesOwnEmployeeIdAndMapsToLoaded() async {
        client.employeeIdResult = .success("emp-1")
        client.periodPaysResult = .success(.stub(grandTotal: 4200))

        let vm = makeViewModel()
        await vm.load()

        guard let summary = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(summary.grandTotal, 4200)
        XCTAssertEqual(client.periodPaysCallCount, 1)
        XCTAssertEqual(client.periodPaysEmployeeId, "emp-1")
        XCTAssertEqual(client.periodPaysPayPeriodId, "pp-1")
    }

    /// One invoice per (period, currency): a period the cleaner was paid in EUR and CZK holds two,
    /// and the server answers the cleaner's resolved currency unless told which. Opened from an
    /// invoice, My Pay asks for that invoice's currency view; from the Earnings tab it asks for none.
    func testOpenedFromAnInvoiceMyPayAsksForThatInvoicesCurrencyView() async {
        let vm = makeViewModel(currencyId: "cur-eur")
        await vm.load()

        XCTAssertEqual(client.periodPaysCurrencyId, "cur-eur")
    }

    func testOpenedFromTheEarningsTabMyPayNamesNoCurrencyView() async {
        let vm = makeViewModel(currencyId: nil)
        await vm.load()

        XCTAssertEqual(client.periodPaysCallCount, 1)
        XCTAssertNil(client.periodPaysCurrencyId)
    }

    func testAnUnresolvableEmployeeIdStopsBeforeTheNetworkCall() async {
        client.employeeIdResult = .failure(ApiError(code: ApiError.wireContractCode, httpStatus: 200))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertEqual(client.periodPaysCallCount, 0)
    }

    /// The two failures reach this screen through the same guard and used to leave it as the same
    /// invented code, so an expired session was reported as a payroll fault and neither reached the
    /// cleaner or an investigator correctly.
    func testASessionFailureAndAWireFailureStayDistinguishable() async {
        client.employeeIdResult = .failure(ApiError(code: "auth.invalid_refresh_token", httpStatus: 401))
        let expired = makeViewModel()
        await expired.load()

        client.employeeIdResult = .failure(ApiError(code: ApiError.wireContractCode, httpStatus: 200))
        let broken = makeViewModel()
        await broken.load()

        guard case let .error(sessionError) = expired.state,
              case let .error(wireError) = broken.state
        else { return XCTFail("expected both to be errors") }

        XCTAssertEqual(sessionError.httpStatus, 401)
        XCTAssertEqual(sessionError.code, "auth.invalid_refresh_token")
        XCTAssertEqual(wireError.code, ApiError.wireContractCode)
        XCTAssertNotEqual(sessionError, wireError)
    }

    func testAFailureToResolveTheCallerIsSurfacedNotSwallowed() async {
        client.employeeIdResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertNotNil(snackbar.current)
    }

    func testApiErrorGoesToErrorAndShowsSnackbar() async {
        client.employeeIdResult = .success("emp-1")
        client.periodPaysResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }
}
