import CleansiaCore
import CleansiaPartnerApi
import Combine
import SwiftUI
import UIKit
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
        client.invoiceResult = .success(InvoiceDetail.stub(id: "inv-1", totalAmount: 4200))

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
        client.invoiceResult = .success(InvoiceDetail.stub(id: "inv-1"))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.state.loadedValue?.id, "inv-1")

        client.invoiceResult = .failure(ApiError(httpStatus: 500))
        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.id, "inv-1")
    }

    func testDownloadSuccessEmitsPresentEventAndReturnsToIdle() async {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent("inv.pdf")
        client.invoiceResult = .success(InvoiceDetail.stub(id: "inv-1"))
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
        client.invoiceResult = .success(InvoiceDetail.stub(id: "inv-1"))
        client.downloadResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.load()
        await vm.openPdf()

        XCTAssertFalse(vm.pdfState.isDownloading)
        XCTAssertNotNil(snackbar.current)
    }

    func testNoPdfPathNeverOffersOrDownloadsAMissingDocument() async throws {
        for path in [nil, "", "  \n "] as [String?] {
            client.invoiceResult = try .success(invoice(path: path, failed: false))
            let vm = makeViewModel()
            await vm.load()

            XCTAssertFalse(vm.canOpenPdf, "a healthy generation flag alone is not a PDF")
            await vm.openPdf()
        }
        XCTAssertEqual(client.downloadCallCount, 0)
    }

    func testFailedRegenerationStillOffersThePreviousDocument() async throws {
        client.invoiceResult = try .success(invoice(path: "generated-invoices/previous.pdf", failed: true))
        let vm = makeViewModel()
        await vm.load()

        XCTAssertTrue(vm.canOpenPdf, "failed regeneration must not hide the previous readable document")
        await vm.openPdf()
        XCTAssertEqual(client.downloadCallCount, 1)
    }

    func testPendingRefreshIntoReadyAndFailedPreviousCopyAreDistinct() async throws {
        client.invoiceResult = try .success(invoice(path: nil, failed: false))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.pdfMessage, L10n.Invoices.pdfPending)

        client.invoiceResult = try .success(invoice(path: "generated-invoices/new.pdf", failed: false))
        await vm.load()
        XCTAssertTrue(vm.canOpenPdf)
        XCTAssertNil(vm.pdfMessage)

        client.invoiceResult = try .success(invoice(path: "generated-invoices/previous.pdf", failed: true))
        await vm.load()
        XCTAssertTrue(vm.canOpenPdf)
        XCTAssertEqual(vm.pdfMessage, L10n.Invoices.pdfPreviousWarning)
    }

    func testDuplicateDownloadsAreCoalescedWhileTheFirstIsInFlight() async {
        let vm = makeViewModel()
        await vm.load()
        let started = expectation(description: "download started")
        var resume: CheckedContinuation<Void, Never>?
        client.beforeDownload = {
            await withCheckedContinuation { continuation in
                resume = continuation
                started.fulfill()
            }
        }
        let download = Task { await vm.openPdf() }
        await fulfillment(of: [started], timeout: 2)
        await vm.openPdf()
        XCTAssertEqual(client.downloadCallCount, 1)
        resume?.resume()
        await download.value
        XCTAssertFalse(vm.pdfState.isDownloading)
    }

    func testInvoiceReadinessVisualEvidence() async throws {
        let previous = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        let scene = try XCTUnwrap(previous?.windowScene)
        for (name, state) in [
            ("Pending", (path: nil, failed: false)),
            ("Unavailable", (path: nil, failed: true)),
            ("Previous PDF", (path: "generated-invoices/previous.pdf", failed: true))
        ] as [(String, (path: String?, failed: Bool))] {
            client.invoiceResult = try .success(invoice(path: state.path, failed: state.failed))
            let vm = makeViewModel()
            await vm.load()
            let host = try UIHostingController(rootView: InvoiceDetailContent(
                invoice: XCTUnwrap(vm.state.loadedValue), canOpenPdf: vm.canOpenPdf,
                isDownloading: false, onOpenPeriodPay: nil, onOpenPdf: {}, onCopy: { _ in },
                pdfMessage: vm.pdfMessage, onRefresh: {}
            ).environment(\.colorScheme, name == "Previous PDF" ? .dark : .light))
            let window = UIWindow(windowScene: scene)
            window.frame = CGRect(x: 0, y: 0, width: 375, height: 1100)
            window.rootViewController = host
            window.makeKeyAndVisible()
            host.view.layoutIfNeeded()
            try await Task.sleep(nanoseconds: 250_000_000)
            let image = UIGraphicsImageRenderer(bounds: window.bounds).image { _ in
                window.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
            }
            let attachment = XCTAttachment(image: image)
            attachment.name = "Invoice readiness 375pt \(name)"
            attachment.lifetime = .keepAlways
            add(attachment)
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
    }

    private func invoice(path: String?, failed: Bool) throws -> InvoiceDetail {
        var dto = EmployeeInvoiceDetailDto(
            id: "inv-1",
            employeeId: "emp-1",
            payPeriodId: "pp-1",
            totalOrders: 1,
            subTotal: 100,
            bonusAmount: 0,
            deductionAmount: 0,
            totalAmount: 100,
            currencyCode: "CZK",
            status: ._1,
            pdfGenerationFailed: failed
        )
        dto.pdfBlobName = path
        return try InvoiceDetail(dto)
    }

    func testCanOpenPdfFalseWhenGenerationFailed() async {
        client.invoiceResult = .success(InvoiceDetail.stub(id: "inv-1", pdfGenerationFailed: true, pdfBlobName: nil))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertFalse(vm.canOpenPdf)
    }

    func testCanOpenPdfTrueWhenGenerationOk() async {
        client.invoiceResult = .success(InvoiceDetail.stub(id: "inv-1", pdfGenerationFailed: false))
        let vm = makeViewModel()
        await vm.load()
        XCTAssertTrue(vm.canOpenPdf)
    }
}
