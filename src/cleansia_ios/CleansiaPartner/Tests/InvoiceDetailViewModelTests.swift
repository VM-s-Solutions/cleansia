import CleansiaCore
import CleansiaPartnerApi
import Combine
import XCTest
@testable import CleansiaPartner

@MainActor
final class InvoiceDetailViewModelTests: XCTestCase {
    private var client: FakePayrollClient!
    private var snackbar: SnackbarController!
    private var cancellables: Set<AnyCancellable>!

    override func setUp() {
        super.setUp()
        client = FakePayrollClient()
        snackbar = SnackbarController()
        cancellables = []
    }

    override func tearDown() {
        client = nil
        snackbar = nil
        cancellables = nil
        super.tearDown()
    }

    private func makeViewModel(invoiceId: String = "inv-1") -> InvoiceDetailViewModel {
        InvoiceDetailViewModel(invoiceId: invoiceId, client: client, snackbar: snackbar)
    }

    func testInitLoadsInvoiceToLoaded() async {
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-1", totalAmount: 4200))

        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.id, "inv-1")
        XCTAssertEqual(client.lastInvoiceId, "inv-1")
    }

    func testInitFailureMapsToErrorAndSnackbars() async {
        client.invoiceResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testRefreshFailureKeepsLoadedInvoice() async {
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-1"))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.id, "inv-1")

        client.invoiceResult = .failure(ApiError(httpStatus: 500))
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.id, "inv-1")
    }

    func testDownloadSuccessEmitsPresentEventAndReturnsToIdle() async {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent("inv.pdf")
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-1"))
        client.downloadResult = .success(fileURL)

        let vm = makeViewModel()
        await vm.load()

        var presented: URL?
        vm.presentPdf.sink { presented = $0 }.store(in: &cancellables)

        await vm.openPdf()

        XCTAssertEqual(presented, fileURL)
        XCTAssertEqual(client.lastDownloadId, "inv-1")
        XCTAssertFalse(vm.pdfState.isDownloading)
    }

    func testDownloadFailureSnackbarsAndReturnsToIdle() async {
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-1"))
        client.downloadResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()
        await vm.openPdf()

        XCTAssertFalse(vm.pdfState.isDownloading)
        XCTAssertNotNil(snackbar.current)
    }

    func testCanOpenPdfFalseWhenGenerationFailed() async {
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-1", pdfGenerationFailed: true))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertFalse(vm.canOpenPdf)
    }

    func testCanOpenPdfTrueWhenGenerationOk() async {
        client.invoiceResult = .success(EmployeeInvoiceDetailDto(id: "inv-1", pdfGenerationFailed: false))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertTrue(vm.canOpenPdf)
    }
}
