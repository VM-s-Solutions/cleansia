import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
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
        client.invoicesResult = .success([Invoice.stub(id: "inv-1", totalAmount: 4200)])

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

    /// The cleaner's invoices stay on screen when a refresh cannot reach the server, and the snackbar says why.
    func testRowsSurviveAFailedRefresh() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1", totalAmount: 4200)])
        let vm = makeViewModel()
        await vm.onAppear()

        client.employeeIdResult = .failure(ApiError(code: "network.unreachable"))
        client.invoicesResult = .failure(ApiError(code: "network.unreachable"))
        await vm.userRefresh()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["inv-1"])
        XCTAssertNotNil(snackbar.current)
    }

    func testTheOwnEmployeeIdIsReadOnceForTheScreen() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        await vm.userRefresh()

        XCTAssertEqual(client.employeeIdCallCount, 1)
        XCTAssertEqual(client.invoicesCallCount, 2)
    }

    /// No id, no read: the list is never asked for unscoped. Nothing loaded yet is the error with the snackbar,
    /// not an empty list that reads as "no invoices".
    func testAFailedFirstLookupIsAnErrorNotAnEmptyList() async {
        client.employeeIdResult = .failure(ApiError(code: "network.unreachable"))

        let vm = makeViewModel()
        await vm.onAppear()

        guard case .error = vm.state else { return XCTFail("expected error, got \(vm.state)") }
        XCTAssertNotNil(snackbar.current)
        XCTAssertEqual(client.invoicesCallCount, 0)
    }

    func testACancelledLookupSaysNothing() async {
        client.employeeIdResult = .failure(ApiError(code: ApiError.cancelledCode))

        let vm = makeViewModel()
        await vm.onAppear()

        XCTAssertNil(snackbar.current)
        XCTAssertEqual(client.invoicesCallCount, 0)
    }

    func testALookupThatFailedIsAskedAgainOnThePull() async {
        client.employeeIdResult = .failure(ApiError(code: "network.unreachable"))
        let vm = makeViewModel()
        await vm.onAppear()

        client.employeeIdResult = .success("emp-1")
        client.invoicesResult = .success([Invoice.stub(id: "inv-1")])
        await vm.userRefresh()

        XCTAssertEqual(vm.state.loadedValue?.map(\.id), ["inv-1"])
        XCTAssertEqual(client.employeeIdCallCount, 2)
    }

    /// `.refreshable` only reaches a scroll view, so the rows, the empty list and the error each rest in one.
    func testEveryRestingStateCanBePulled() async throws {
        let states: [(String, ApiResult<[Invoice]>)] = [
            ("rows", .success([Invoice.stub(id: "inv-1")])),
            ("empty", .success([])),
            ("error", .failure(ApiError(httpStatus: 500)))
        ]
        for (name, result) in states {
            client.invoicesResult = result
            let canvas = try HostedCanvas(size: CGSize(width: 393, height: 852)) {
                InvoicesListView(client: client, staleness: staleness, snackbar: snackbar, onOpenInvoice: { _ in })
            }
            defer { canvas.close() }
            await canvas.settle { canvas.views(of: UIScrollView.self).contains { $0.refreshControl != nil } }
            XCTAssertTrue(canvas.views(of: UIScrollView.self).contains { $0.refreshControl != nil }, name)
        }
    }

    func testApiErrorMapsToErrorAndShowsSnackbar() async {
        client.invoicesResult = .failure(ApiError(httpStatus: 500))

        let vm = makeViewModel()
        await vm.onAppear()

        guard case .error = vm.state else { return XCTFail("expected error") }
        XCTAssertNotNil(snackbar.current)
    }

    func testUserRefreshFailureKeepsPriorLoaded() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1", totalAmount: 100)])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(vm.state.loadedValue?.count, 1)

        client.invoicesResult = .failure(ApiError(httpStatus: 500))
        await vm.userRefresh()

        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }

    /// A warm cache makes the next on-appear (resume) a silent no-op —
    /// no second network round-trip while inside the freshness window.
    func testOnAppearSkipsNetworkWhileCacheIsWarm() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        // Still inside the 30s window — resume must not re-fetch.
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)
    }

    /// Once the watermark goes stale, the next on-appear silently
    /// re-fetches (the silent-stale resume).
    func testOnAppearRefetchesAfterWatermarkGoesStale() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        clock = clock.addingTimeInterval(31) // past the 30s window
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 2)
    }

    /// Invalidating the watermark forces a re-fetch even while warm.
    func testInvalidateForcesRefetchEvenWhileWarm() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        staleness.invalidate()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 2)
    }

    /// A user pull always re-fetches, bypassing the warm watermark.
    func testUserRefreshAlwaysFetchesEvenWhileWarm() async {
        client.invoicesResult = .success([Invoice.stub(id: "inv-1")])
        let vm = makeViewModel()
        await vm.onAppear()
        XCTAssertEqual(client.invoicesCallCount, 1)

        await vm.userRefresh()
        XCTAssertEqual(client.invoicesCallCount, 2)
    }
}
