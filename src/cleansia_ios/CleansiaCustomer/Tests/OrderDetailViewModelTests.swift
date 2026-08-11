import CleansiaCore
import CleansiaCustomerApi
import Combine
import XCTest
@testable import CleansiaCustomer

private struct NoopLiveActivitySync: OrderLiveActivitySyncing {
    func start(orderId _: String, orderNumber _: String, status _: String, window _: EtaWindow) {}
    func update(orderId _: String, orderNumber _: String, status _: String, window _: EtaWindow) {}
    func end(orderId _: String, orderNumber _: String, status _: LiveActivityTerminalStatus) {}
}

@MainActor
final class OrderDetailViewModelTests: XCTestCase {
    private func makeVM(
        orderId: String = "o1",
        client: FakeOrderClient,
        membershipClient: FakeMembershipManagementClient = FakeMembershipManagementClient(),
        pollInterval: TimeInterval = 60
    ) -> OrderDetailViewModel {
        let repo = OrderRepository(client: client)
        return OrderDetailViewModel(
            orderId: orderId,
            client: client,
            repository: repo,
            membershipRepository: MembershipRepository(client: membershipClient),
            snackbar: SnackbarController(),
            eventBus: OrderEventBus(),
            liveActivity: NoopLiveActivitySync(),
            pollInterval: pollInterval
        )
    }

    func testLoadSuccessShowsLoaded() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let vm = makeVM(client: client)

        await vm.load()

        XCTAssertEqual(vm.state.loadedValue?.id, "o1")
    }

    func testLoadFailureShowsError() async {
        let client = FakeOrderClient()
        client.detailResults = [.failure(ApiError(httpStatus: 500))]
        let vm = makeVM(client: client)

        await vm.load()

        if case .error = vm.state {} else { XCTFail("expected error") }
    }

    func testBlankOrderIdIsErrorWithoutNetwork() async {
        let client = FakeOrderClient()
        let vm = makeVM(orderId: "", client: client)

        await vm.load()

        if case .error = vm.state {} else { XCTFail("expected error") }
        XCTAssertEqual(client.detailCallCount, 0)
    }

    func testRetryReloadsToLoaded() async {
        let client = FakeOrderClient()
        client.detailResults = [.failure(ApiError(httpStatus: 500)), .success(OrderFixtures.detail(statusValue: 2))]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.retry()

        XCTAssertEqual(vm.state.loadedValue?.id, "o1")
    }

    // MARK: Cancel

    func testCancelSuccessEmitsEffectAndRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 2)),
            .success(OrderFixtures.detail(statusValue: 6))
        ]
        client.cancelResult = .success(OrderCancellation(refundAmount: 0, refundInitiated: false))
        let vm = makeVM(client: client)
        await vm.load()

        var received: OrderCancellation?
        let cancellable = vm.cancelSucceeded.sink { received = $0 }
        await vm.cancel(reason: "schedule_changed")
        cancellable.cancel()

        XCTAssertNotNil(received)
        XCTAssertEqual(vm.cancelState, .idle)
        XCTAssertEqual(client.cancelCallCount, 1)
        XCTAssertEqual(client.lastCancelReason, .some("schedule_changed"))
    }

    func testCancelBlankReasonSendsNil() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 2)),
            .success(OrderFixtures.detail(statusValue: 6))
        ]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.cancel(reason: "   ")

        XCTAssertEqual(client.lastCancelReason, .some(nil))
    }

    func testCancelFailureSetsActionError() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 2))]
        client.cancelResult = .failure(ApiError(httpStatus: 400))
        let vm = makeVM(client: client)
        await vm.load()

        await vm.cancel(reason: nil)

        if case .error = vm.cancelState {} else { XCTFail("expected cancel error") }
        vm.dismissCancelError()
        XCTAssertEqual(vm.cancelState, .idle)
    }

    // MARK: Cancellation quote

    func testTheQuoteIsTheServersAnswer() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 2))]
        client.cancellationQuoteResults = [.success(OrderFixtures.quote(tier: .partial, fee: 250, refund: 750))]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.loadCancellationQuote()

        XCTAssertEqual(vm.cancellationQuote.loadedValue?.tier, .partial)
        XCTAssertEqual(vm.cancellationQuote.loadedValue?.feeAmount, 250)
        XCTAssertEqual(client.cancellationQuoteCallCount, 1)
    }

    /// A preview outage must never block a cancellation: the state resolves to `.error`, which the sheet
    /// renders as the neutral prompt with the confirm button live — it must not sit on `.loading`.
    func testAFailedQuoteResolvesRatherThanHangingOnLoading() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 2))]
        client.cancellationQuoteResults = [.failure(ApiError(httpStatus: 503))]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.loadCancellationQuote()

        XCTAssertFalse(vm.cancellationQuote.isLoading)
        XCTAssertNil(vm.cancellationQuote.loadedValue)
    }

    /// The tier depends on the clock, so a sheet re-opened later re-asks rather than showing the answer to
    /// a question asked at a different time.
    func testReopeningTheSheetReasksTheServer() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 2))]
        client.cancellationQuoteResults = [
            .success(OrderFixtures.quote(tier: .freeOutsideWindow)),
            .success(OrderFixtures.quote(tier: .partial, fee: 250, refund: 750))
        ]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.loadCancellationQuote()
        await vm.loadCancellationQuote()

        XCTAssertEqual(client.cancellationQuoteCallCount, 2)
        XCTAssertEqual(vm.cancellationQuote.loadedValue?.tier, .partial)
    }

    func testABlankOrderIdNeverAsksAndStillResolves() async {
        let client = FakeOrderClient()
        let vm = makeVM(orderId: "", client: client)

        await vm.loadCancellationQuote()

        XCTAssertEqual(client.cancellationQuoteCallCount, 0)
        XCTAssertFalse(vm.cancellationQuote.isLoading)
    }

    // MARK: Review

    func testReviewSuccessEmitsEffectAndRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 5)),
            .success(OrderFixtures.detail(statusValue: 5))
        ]
        client.reviewResult = .success(OrderReviewDto(rating: 4))
        let vm = makeVM(client: client)
        await vm.load()

        var received: OrderReviewDto?
        let cancellable = vm.reviewSucceeded.sink { received = $0 }
        await vm.submitReview(rating: 4, comment: "Great", isEdit: false)
        cancellable.cancel()

        XCTAssertEqual(received?.rating, 4)
        XCTAssertEqual(vm.reviewState, .idle)
        XCTAssertEqual(client.lastReview?.rating, 4)
    }

    func testReviewRejectsOutOfRangeRating() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.submitReview(rating: 0, comment: nil, isEdit: false)

        XCTAssertEqual(client.reviewCallCount, 0)
    }

    func testReviewFailureSetsActionError() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        client.reviewResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM(client: client)
        await vm.load()

        await vm.submitReview(rating: 5, comment: nil, isEdit: false)

        if case .error = vm.reviewState {} else { XCTFail("expected review error") }
    }

    // MARK: Receipt

    func testReceiptSuccessEmitsFileUrl() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let url = URL(fileURLWithPath: "/tmp/o1.pdf")
        client.receiptResult = .success(url)
        let vm = makeVM(client: client)
        await vm.load()

        var received: URL?
        let cancellable = vm.receiptReady.sink { received = $0 }
        await vm.downloadReceipt()
        cancellable.cancel()

        XCTAssertEqual(received, url)
        XCTAssertEqual(vm.receiptState, .idle)
    }

    func testReceiptFailureClearsSubmittingWithoutEffect() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        client.receiptResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM(client: client)
        await vm.load()

        var emitted = false
        let cancellable = vm.receiptReady.sink { _ in emitted = true }
        await vm.downloadReceipt()
        cancellable.cancel()

        XCTAssertFalse(emitted)
        XCTAssertEqual(vm.receiptState, .idle)
    }

    // MARK: Photos side-channel

    func testEnsurePhotosLoadsLazilyAndFreshEachOpen() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        client.photosResults = [.success(OrderFixtures.photos(before: 1, after: 2))]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.ensurePhotosLoaded()
        XCTAssertEqual(vm.photos.loadedResponse?.afterCount, 2)

        // Second call once loaded is a no-op (still one fetch).
        await vm.ensurePhotosLoaded()
        XCTAssertEqual(client.photosCallCount, 1)
    }

    func testEnsurePhotosErrorThenRetryFetchesAgain() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        client.photosResults = [
            .failure(ApiError(httpStatus: 500)),
            .success(OrderFixtures.photos())
        ]
        let vm = makeVM(client: client)
        await vm.load()

        await vm.ensurePhotosLoaded()
        if case .error = vm.photos {} else { XCTFail("expected photos error") }

        await vm.ensurePhotosLoaded()
        XCTAssertNotNil(vm.photos.loadedResponse)
        XCTAssertEqual(client.photosCallCount, 2)
    }

    // MARK: Confirm recurring

    func testConfirmRecurringCashNullSecretConfirmsAndRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 1)),
            .success(OrderFixtures.detail(statusValue: 2))
        ]
        client.confirmRecurringResult = .success(
            RecurringConfirmation(clientSecret: nil, stripeCustomerId: nil, ephemeralKey: nil)
        )
        let vm = makeVM(client: client)
        await vm.load()

        var presented: PaymentSheetPresentation?
        let cancellable = vm.recurringCardPayment.sink { presented = $0 }
        await vm.confirmRecurring()
        cancellable.cancel()

        XCTAssertNil(presented, "cash confirm must not present a PaymentSheet")
        XCTAssertEqual(client.confirmRecurringCallCount, 1)
        XCTAssertEqual(vm.confirmRecurringState, .idle)
    }

    func testConfirmRecurringCardNonNullSecretEmitsPaymentIntentPresentation() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 1))]
        client.confirmRecurringResult = .success(
            RecurringConfirmation(clientSecret: "pi_secret_1", stripeCustomerId: "cus_1", ephemeralKey: "ek_1")
        )
        let vm = makeVM(client: client)
        await vm.load()

        var presented: PaymentSheetPresentation?
        let cancellable = vm.recurringCardPayment.sink { presented = $0 }
        await vm.confirmRecurring()
        cancellable.cancel()

        XCTAssertEqual(presented?.intentKind, .payment)
        XCTAssertEqual(presented?.clientSecret, "pi_secret_1")
        XCTAssertEqual(presented?.stripeCustomerId, "cus_1")
        XCTAssertEqual(vm.confirmRecurringState, .idle)
    }

    func testConfirmRecurringFailureStaysIdle() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 1))]
        client.confirmRecurringResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM(client: client)
        await vm.load()

        await vm.confirmRecurring()

        XCTAssertEqual(vm.confirmRecurringState, .idle)
    }

    func testRecurringCardPaymentCompletedRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 1)),
            .success(OrderFixtures.detail(statusValue: 2))
        ]
        let vm = makeVM(client: client)
        await vm.load()
        let before = client.detailCallCount

        await vm.notifyRecurringPaymentResult(.completed)

        XCTAssertGreaterThan(client.detailCallCount, before, "completed PaymentSheet re-reads the order")
    }

    // MARK: Poller / events

    func testActiveOrderStartsPollerWhichRefetches() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 4)),
            .success(OrderFixtures.detail(id: "o1", statusValue: 5))
        ]
        let vm = makeVM(client: client, pollInterval: 0.01)
        await vm.load()

        try? await Task.sleep(nanoseconds: 60_000_000)

        XCTAssertGreaterThanOrEqual(client.detailCallCount, 2)
    }

    func testTerminalOrderDoesNotPoll() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let vm = makeVM(client: client, pollInterval: 0.01)
        await vm.load()

        try? await Task.sleep(nanoseconds: 40_000_000)

        XCTAssertEqual(client.detailCallCount, 1)
    }

    func testEventBusTriggersRefetch() async {
        let client = FakeOrderClient()
        client.detailResults = [
            .success(OrderFixtures.detail(statusValue: 5)),
            .success(OrderFixtures.detail(statusValue: 5))
        ]
        let bus = OrderEventBus()
        let repo = OrderRepository(client: client)
        let vm = OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: repo,
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
            snackbar: SnackbarController(),
            eventBus: bus,
            liveActivity: NoopLiveActivitySync(),
            pollInterval: 60
        )
        await vm.load()

        bus.emit(orderId: "o1")
        try? await Task.sleep(nanoseconds: 30_000_000)

        XCTAssertGreaterThanOrEqual(client.detailCallCount, 2)
    }

    // MARK: Make this recurring

    private func membershipClient(_ result: ApiResult<MyMembership>) -> FakeMembershipManagementClient {
        let client = FakeMembershipManagementClient()
        client.mineResults = [result]
        return client
    }

    private func completedVM(_ memClient: FakeMembershipManagementClient) -> OrderDetailViewModel {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        return makeVM(client: client, membershipClient: memClient)
    }

    func testAResolvedNonMemberLosesTheMakeRecurringShortcut() async {
        let vm = completedVM(membershipClient(.success(MembershipFixtures.inactive)))

        await vm.load()

        XCTAssertEqual(vm.recurringAuthoring, .upsell)
    }

    /// The defect: nothing on this screen fetched membership, so the shortcut was withheld
    /// from paid-up members whenever no other screen had warmed the cache. An answer that
    /// has not landed must not cost them the affordance — the server refuses an unentitled
    /// create with its own localized message.
    func testAnUnresolvedMembershipKeepsTheMakeRecurringShortcut() async {
        let vm = completedVM(membershipClient(.failure(ApiError(httpStatus: 500))))

        await vm.load()

        XCTAssertNil(vm.hasMembership)
        XCTAssertEqual(vm.recurringAuthoring, .allowed)
    }

    func testAMemberKeepsTheMakeRecurringShortcut() async {
        let vm = completedVM(membershipClient(.success(MembershipFixtures.active)))

        await vm.load()

        XCTAssertEqual(vm.recurringAuthoring, .allowed)
    }

    func testTheScreenFetchesMembershipItselfRatherThanTrustingAnotherScreensCache() async {
        let memClient = membershipClient(.success(MembershipFixtures.inactive))
        let vm = completedVM(memClient)

        await vm.load()

        XCTAssertEqual(memClient.mineCallCount, 1)
    }

    func testAMembershipAnswerStillInsideItsFreshnessWindowIsNotRefetched() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let memClient = FakeMembershipManagementClient()
        memClient.mineResults = [.success(MembershipFixtures.active)]
        let memRepo = MembershipRepository(client: memClient)
        await memRepo.refresh()
        let vm = OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: memRepo,
            snackbar: SnackbarController(),
            eventBus: OrderEventBus(),
            liveActivity: NoopLiveActivitySync(),
            pollInterval: 60
        )

        await vm.load()

        XCTAssertEqual(memClient.mineCallCount, 1)
        XCTAssertEqual(vm.recurringAuthoring, .allowed)
    }

    /// The binding has to replay a warm cache at init, not one turn later: the footer is
    /// built on the first body pass, and `.allowed` is also the fail-open default, so a
    /// binding that did not replay would look correct for a member and flash the shortcut
    /// at a resolved non-member. This is what makes a separate seed assignment redundant.
    func testAWarmMembershipCacheIsVisibleBeforeTheFirstLoad() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(OrderFixtures.detail(statusValue: 5))]
        let memRepo = MembershipRepository(client: membershipClient(.success(MembershipFixtures.inactive)))
        await memRepo.refresh()

        let vm = OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: memRepo,
            snackbar: SnackbarController(),
            eventBus: OrderEventBus(),
            liveActivity: NoopLiveActivitySync(),
            pollInterval: 60
        )

        XCTAssertEqual(vm.hasMembership, false)
        XCTAssertEqual(vm.recurringAuthoring, .upsell)
    }

    /// `deinit` cancelling `pollTask` is this view model's ONLY teardown for an order that
    /// stays active: the poller stops when the order goes non-active or `shouldStopPolling()`
    /// says so, and neither fires when the customer just navigates away. So anything that
    /// retains `self` here is not a leak — it is a `GET /orders/{id}` every poll interval,
    /// per screen open, for the life of the process. `assign(to: &$x)` does not retain self;
    /// `assign(to:on:)` does.
    func testAnActiveOrderViewModelIsReleasedWhenTheScreenGoesAway() async {
        weak var released: OrderDetailViewModel?

        func openAndLeaveTheScreen() async {
            let client = FakeOrderClient()
            client.detailResults = [.success(OrderFixtures.detail(statusValue: 4))]
            let vm = makeVM(client: client)
            released = vm
            await vm.load()
            XCTAssertNotNil(released, "the view model was released before the screen was left")
        }

        await openAndLeaveTheScreen()

        XCTAssertNil(released, "the view model outlived its screen, so its active-order poller never stops")
    }
}
