import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The iOS twin of Android's `GuestOrderViewModelTest`, case for case.
@MainActor
final class GuestOrderViewModelTests: XCTestCase {
    private var client: FakeGuestOrderClient!
    private var settings: FakeAppSettingsStore!
    private var vm: GuestOrderViewModel!

    override func setUp() {
        super.setUp()
        client = FakeGuestOrderClient()
        settings = FakeAppSettingsStore()
        settings.languageTag = "sk"
        vm = GuestOrderViewModel(client: client, settings: settings, localizer: KeyEchoLocalizer())
    }

    override func tearDown() {
        vm = nil
        client = nil
        settings = nil
        super.tearDown()
    }

    private func drain() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }

    private func lookupAndOpen() async {
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")
        await vm.openCancellation()
    }

    // MARK: Lookup

    func testABlankFieldNeverReachesTheTransportAndAValidLookupTrimsEveryCredential() async {
        await vm.lookup(number: " ", email: "guest@example.test", code: "code")

        XCTAssertEqual(vm.state, .error(L10n.GuestOrder.required))
        XCTAssertTrue(client.lookupKeys.isEmpty)

        await vm.lookup(number: " CZ-123 ", email: " guest@example.test ", code: " secret ")

        XCTAssertEqual(vm.state, .loaded(GuestOrderFixtures.order()))
        XCTAssertEqual(client.lookupKeys, [GuestOrderFixtures.key].compactMap { $0 })
    }

    func testEditingTheCredentialsClearsTheOrderAndALateLookupCannotRestoreIt() async {
        let gate = AsyncGate()
        client.lookupGate = gate
        client.lookupResult = .success(GuestOrderFixtures.order(id: "stale"))
        let stale = Task { await vm.lookup(number: "old", email: "guest@example.test", code: "secret") }
        await drain()
        XCTAssertEqual(vm.state, .loading)

        vm.onCredentialsChanged()
        XCTAssertEqual(vm.state, .empty)

        client.lookupGate = nil
        client.lookupResult = .success(GuestOrderFixtures.order())
        await vm.lookup(number: "new", email: "guest@example.test", code: "new-secret")
        gate.open()
        await stale.value

        XCTAssertEqual(vm.state, .loaded(GuestOrderFixtures.order()))
    }

    func testALookupFailureReplacesThePreviousDetailsAndIsRetryable() async {
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")
        vm.onCredentialsChanged()
        client.lookupResult = .failure(ApiError(code: "order.not_found", httpStatus: 400))

        await vm.lookup(number: "wrong", email: "guest@example.test", code: "secret")

        XCTAssertEqual(vm.state, .error("order.not_found"))

        client.lookupResult = .success(GuestOrderFixtures.order())
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")

        XCTAssertNotNil(vm.state.loadedOrder)
    }

    // MARK: Quote

    func testAFailedUnknownOrMismatchedQuoteCannotSubmitACancellation() async {
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")
        let rejected: [ApiResult<GuestCancellationQuote>] = [
            .failure(ApiError(code: "order.not_found", httpStatus: 400)),
            .success(GuestOrderFixtures.quote(orderId: "other")),
            .success(GuestOrderFixtures.quote(currencyCode: "CZK")),
            .success(GuestOrderFixtures.quote(currencyCode: nil))
        ]

        for result in rejected {
            client.quoteResult = result
            await vm.openCancellation()
            XCTAssertNil(vm.quote.loadedValue?.currencyCode, "\(result) must not price the sheet")
            await vm.cancel(reason: "schedule_changed")
        }

        XCTAssertTrue(client.cancelCalls.isEmpty)

        client.quoteResult = .success(GuestOrderFixtures.quote())
        await vm.loadQuote()

        XCTAssertEqual(vm.quote.loadedValue, GuestOrderFixtures.quote().quote)
    }

    func testAQuoteRefusalSurfacesTheServersReasonOnTheSheet() async {
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")
        client.quoteResult = .failure(ApiError(code: "order.in_progress_cannot_cancel", httpStatus: 400))

        await vm.openCancellation()

        XCTAssertEqual(vm.cancelState, .error("order.in_progress_cannot_cancel"))
        XCTAssertNil(vm.quote.loadedValue)
    }

    func testALateQuoteAfterEditingOrDismissingCannotExposeAFee() async {
        let gate = AsyncGate()
        client.quoteGate = gate
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")
        let late = Task { await vm.openCancellation() }
        await drain()
        XCTAssertTrue(vm.isCancellationPresented)

        vm.dismissCancellation()
        vm.onCredentialsChanged()
        gate.open()
        await late.value

        XCTAssertEqual(vm.state, .empty)
        XCTAssertFalse(vm.isCancellationPresented)
        XCTAssertTrue(vm.quote.isLoading)
        await vm.cancel(reason: "schedule_changed")
        XCTAssertTrue(client.cancelCalls.isEmpty)
    }

    // MARK: Cancel

    func testSubmitIsGuardedSynchronouslyAndSendsTheLookupCredentialsWithTheSelectedLanguage() async throws {
        let gate = AsyncGate()
        client.cancelGate = gate
        await vm.lookup(number: " CZ-123 ", email: " guest@example.test ", code: " secret ")
        await vm.openCancellation()

        let first = Task { await vm.cancel(reason: "schedule_changed") }
        await drain()
        XCTAssertEqual(vm.cancelState, .submitting)
        await vm.cancel(reason: "schedule_changed")
        vm.dismissCancellation()
        XCTAssertTrue(vm.isCancellationPresented, "a submit in flight pins the sheet")
        gate.open()
        await first.value

        XCTAssertEqual(client.cancelCalls.count, 1)
        let call = try XCTUnwrap(client.cancelCalls.first)
        XCTAssertEqual(call.key, GuestOrderFixtures.key)
        XCTAssertEqual(call.reason, "schedule_changed")
        XCTAssertEqual(call.language, "sk")
        XCTAssertFalse(vm.isCancellationPresented)
        XCTAssertEqual(vm.cancelState, .idle)
    }

    func testSuccessQuotesTheActualRefundInTheOrdersCurrencyAndNeverThePolicyFigure() async {
        client.cancelResult = .success(GuestOrderFixtures.receipt(refundInitiated: true, actualRefundAmount: 12))
        await lookupAndOpen()

        await vm.cancel(reason: "schedule_changed")

        let expected = L10n.OrderCancel.successWithRefund(OrdersFormat.price(12, currencyCode: "EUR"))
        XCTAssertEqual(vm.state, .cancelled(expected))
        if case let .cancelled(message) = vm.state {
            XCTAssertFalse(message.contains(OrdersFormat.price(67.5, currencyCode: "EUR")))
        }
    }

    func testANilOrZeroActualRefundUsesTheNoRefundCopyWithoutAMadeUpAmount() async {
        for amount in [nil, 0.0] {
            let receipt = GuestOrderFixtures.receipt(refundInitiated: true, actualRefundAmount: amount)
            client.cancelResult = .success(receipt)
            await lookupAndOpen()

            await vm.cancel(reason: "schedule_changed")

            XCTAssertEqual(vm.state, .cancelled(L10n.OrderCancel.successNoRefund), "amount \(amount ?? -1)")
        }
    }

    func testAFailedCancellationKeepsTheSheetShowsTheErrorAndNeedsAFreshQuoteBeforeRetry() async {
        client.cancelResult = .failure(ApiError(code: "order.already_cancelled", httpStatus: 400))
        await lookupAndOpen()

        await vm.cancel(reason: "schedule_changed")

        XCTAssertEqual(vm.cancelState, .error("order.already_cancelled"))
        XCTAssertTrue(vm.isCancellationPresented)
        XCTAssertNotNil(vm.state.loadedOrder)

        await vm.cancel(reason: "schedule_changed")

        XCTAssertEqual(client.cancelCalls.count, 1, "no retry without a fresh quote")
    }

    /// The over-limit reason is also written in astral text: 251 emoji are 251 to `String.count` and 502
    /// to the server's `MaximumLength(500)`, so a `count` guard would let them through to a refusal.
    func testTheReasonLimitAndAMissingQuoteAreEnforcedBeyondTheButton() async {
        let gate = AsyncGate()
        client.quoteGate = gate
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")
        await vm.cancel(reason: "schedule_changed")
        let opening = Task { await vm.openCancellation() }
        await drain()
        XCTAssertTrue(vm.isCancellationPresented)
        await vm.cancel(reason: "schedule_changed")
        gate.open()
        await opening.value
        await vm.cancel(reason: String(repeating: "x", count: 501))
        await vm.cancel(reason: String(repeating: "😀", count: 251))
        await vm.cancel(reason: " ")
        await vm.cancel(reason: nil)

        XCTAssertTrue(client.cancelCalls.isEmpty)

        await vm.cancel(reason: String(repeating: "x", count: 500))

        XCTAssertEqual(client.cancelCalls.count, 1)
    }

    func testLeavingTheScreenPreventsALateReceiptFromExposingThePreviousOrder() async {
        let gate = AsyncGate()
        client.cancelGate = gate
        await lookupAndOpen()
        let late = Task { await vm.cancel(reason: "schedule_changed") }
        await drain()

        vm.clear()
        gate.open()
        await late.value

        XCTAssertEqual(vm.state, .empty)
        XCTAssertFalse(vm.isCancellationPresented)
        XCTAssertEqual(vm.cancelState, .idle)
    }

    func testTerminalAndUnknownOrdersNeverOpenCancellation() async {
        for status in [4, 5, 6, 99] {
            client.lookupResult = .success(GuestOrderFixtures.order(statusValue: status))
            await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")

            await vm.openCancellation()
            await vm.loadQuote()

            XCTAssertFalse(vm.isCancellationPresented, "status \(status)")
            vm.onCredentialsChanged()
        }
        XCTAssertEqual(client.quoteCallCount, 0)
    }

    func testAnOnTheWayGuestBookingIsStillCancellable() async {
        client.lookupResult = .success(GuestOrderFixtures.order(statusValue: 3))
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")

        await vm.openCancellation()

        XCTAssertTrue(vm.isCancellationPresented)
    }

    /// The view has no trigger of its own: opening the sheet is what loads the quote, exactly once.
    func testOpeningTheCancellationLoadsTheQuoteItself() async {
        await vm.lookup(number: "CZ-123", email: "guest@example.test", code: "secret")

        await vm.openCancellation()

        XCTAssertTrue(vm.isCancellationPresented)
        XCTAssertEqual(client.quoteCallCount, 1)
        XCTAssertEqual(vm.quote.loadedValue, GuestOrderFixtures.quote().quote)
    }
}
