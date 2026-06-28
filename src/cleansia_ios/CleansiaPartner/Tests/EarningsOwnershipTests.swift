import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

/// TC-IOS-EARNINGS-OWNERSHIP — the own-data read invariants for the earnings
/// surface (security E1/E3; E4 cleanup is covered by QuickLookPreviewTests).
@MainActor
final class EarningsOwnershipTests: XCTestCase {
    private var client: FakePayrollClient!
    private var snackbar: SnackbarController!
    private var staleness: InvoicesStaleness!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakePayrollClient()
        snackbar = SnackbarController()
        staleness = InvoicesStaleness()
        cancellables = []
    }

    override func tearDown() {
        client = nil
        snackbar = nil
        staleness = nil
        cancellables = nil
        super.tearDown()
    }

    // E1: the invoices list queries ONLY the caller's server-derived id.
    func testInvoicesListSendsOnlyOwnServerDerivedEmployeeId() async {
        client.employeeIdResult = .success("emp-own")
        client.invoicesResult = .success([])

        let vm = InvoicesListViewModel(client: client, staleness: staleness, snackbar: snackbar)
        await vm.onAppear()

        XCTAssertEqual(client.employeeIdCallCount, 1)
        XCTAssertEqual(client.invoicesEmployeeId, "emp-own")
    }

    // E1: an unresolvable id never hits the network — no blind/foreign query.
    func testInvoicesListNeverQueriesWhenIdUnresolvable() async {
        client.employeeIdResult = .failure(ApiError(code: "payroll.employee_id_missing"))

        let vm = InvoicesListViewModel(client: client, staleness: staleness, snackbar: snackbar)
        await vm.onAppear()

        XCTAssertEqual(client.invoicesCallCount, 0)
    }

    // E1: period pay sends ONLY the caller's server-derived id.
    func testPeriodPaySendsOnlyOwnServerDerivedEmployeeId() async {
        client.employeeIdResult = .success("emp-own")
        client.periodPaysResult = .success(PeriodPaySummaryDto())

        let vm = PeriodPayViewModel(
            payPeriodId: "pp-1",
            currencyCode: "CZK",
            client: client,
            snackbar: snackbar
        )
        await vm.load()

        XCTAssertEqual(client.periodPaysEmployeeId, "emp-own")
    }

    // E3: the detail download acts on the loaded invoice's OWN id — the same id
    // the VM received, never a synthesized/foreign one.
    func testDownloadActsOnlyOnTheLoadedInvoiceId() async {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent("own.pdf")
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-own"))
        client.downloadResult = .success(fileURL)

        let vm = InvoiceDetailViewModel(invoiceId: "inv-own", client: client, snackbar: snackbar)
        await vm.load()
        await vm.openPdf()

        XCTAssertEqual(client.lastDownloadId, "inv-own")
    }
}
