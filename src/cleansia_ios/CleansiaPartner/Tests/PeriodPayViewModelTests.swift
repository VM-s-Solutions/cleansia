import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class PeriodPayViewModelTests: XCTestCase {
    private final class FakePayrollClient: PartnerPayrollClient {
        var employeeIdResult: ApiResult<String> = .success("emp-1")
        var periodPaysResult: ApiResult<PeriodPaySummaryDto> = .success(PeriodPaySummaryDto())
        private(set) var periodPaysCallCount = 0
        private(set) var lastEmployeeId: String?
        private(set) var lastPayPeriodId: String?

        func currentEmployeeId() async -> ApiResult<String> {
            employeeIdResult
        }

        func getPeriodPays(employeeId: String, payPeriodId: String) async -> ApiResult<PeriodPaySummaryDto> {
            periodPaysCallCount += 1
            lastEmployeeId = employeeId
            lastPayPeriodId = payPeriodId
            return periodPaysResult
        }

        func getPagedInvoices(employeeId _: String) async -> ApiResult<[EmployeeInvoiceDto]> {
            .success([])
        }

        func getInvoice(id _: String) async
            -> ApiResult<EmployeeInvoiceDetailDto>
        {
            .success(EmployeeInvoiceDetailDto())
        }

        func downloadInvoicePdf(id _: String) async -> ApiResult<URL> {
            .success(URL(fileURLWithPath: "/tmp/x"))
        }
    }

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

    private func makeViewModel() -> PeriodPayViewModel {
        PeriodPayViewModel(
            payPeriodId: "pp-1",
            currencyCode: "CZK",
            client: client,
            snackbar: snackbar
        )
    }

    func testInitialStateIsLoading() {
        XCTAssertTrue(makeViewModel().state.isLoading)
    }

    func testLoadResolvesOwnEmployeeIdAndMapsToLoaded() async {
        client.employeeIdResult = .success("emp-1")
        client.periodPaysResult = .success(PeriodPaySummaryDto(payPeriodId: "pp-1", grandTotal: 4200))

        let vm = makeViewModel()
        await vm.load()

        guard let summary = vm.state.loadedValue else { return XCTFail("expected loaded") }
        XCTAssertEqual(summary.grandTotal, 4200)
        XCTAssertEqual(client.periodPaysCallCount, 1)
        XCTAssertEqual(client.lastEmployeeId, "emp-1")
        XCTAssertEqual(client.lastPayPeriodId, "pp-1")
    }

    func testMissingEmployeeIdGoesToErrorWithoutNetworkCall() async {
        client.employeeIdResult = .failure(ApiError(code: "payroll.employee_id_missing"))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertEqual(client.periodPaysCallCount, 0)
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
