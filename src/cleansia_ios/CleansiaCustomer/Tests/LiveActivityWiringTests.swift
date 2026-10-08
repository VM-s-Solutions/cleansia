import ActivityKit
import CleansiaCore
import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

private struct StubDeviceIdProvider: DeviceIdProviding {
    let deviceId: String
}

private final class SpyLiveActivityApi: LiveActivityApi, @unchecked Sendable {
    private(set) var registered: [RegisterLiveActivityTokenCommand] = []
    private(set) var unregistered: [(orderId: String, deviceId: String)] = []
    var registerError: Error?
    var unregisterError: Error?
    var onUnregister: ((String) -> Void)?

    func register(_ command: RegisterLiveActivityTokenCommand) async throws {
        registered.append(command)
        if let registerError { throw registerError }
    }

    func unregister(orderId: String, deviceId: String) async throws {
        unregistered.append((orderId, deviceId))
        onUnregister?(orderId)
        if let unregisterError { throw unregisterError }
    }
}

private final class SpyLiveActivitySync: OrderLiveActivitySyncing, @unchecked Sendable {
    struct Call: Equatable {
        let orderId: String
        let orderNumber: String
        let status: String
        let window: EtaWindow
    }

    struct EndCall: Equatable {
        let orderId: String
        let orderNumber: String
        let status: LiveActivityTerminalStatus
    }

    private(set) var started: [Call] = []
    private(set) var updated: [Call] = []
    private(set) var ended: [EndCall] = []

    func start(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        started.append(Call(orderId: orderId, orderNumber: orderNumber, status: status, window: window))
    }

    func update(orderId: String, orderNumber: String, status: String, window: EtaWindow) {
        updated.append(Call(orderId: orderId, orderNumber: orderNumber, status: status, window: window))
    }

    func end(orderId: String, orderNumber: String, status: LiveActivityTerminalStatus) {
        ended.append(EndCall(orderId: orderId, orderNumber: orderNumber, status: status))
    }
}

@MainActor
final class LiveActivityRegistrarTests: XCTestCase {
    private func makeSUT(deviceId: String = "device-1") -> (CustomerLiveActivityRegistrar, SpyLiveActivityApi) {
        let api = SpyLiveActivityApi()
        let sut = CustomerLiveActivityRegistrar(deviceIdProvider: StubDeviceIdProvider(deviceId: deviceId), api: api)
        return (sut, api)
    }

    func testRegisterMapsDeviceTokenAndOrderId() async throws {
        let (sut, api) = makeSUT()

        await sut.register(orderId: "order-9", orderNumber: "1042", token: "abc123")

        let command = try XCTUnwrap(api.registered.first)
        XCTAssertEqual(api.registered.count, 1)
        XCTAssertEqual(command.deviceId, "device-1")
        XCTAssertEqual(command.token, "abc123")
        XCTAssertEqual(command.orderId, "order-9")
    }

    func testRegisterPushToStartSendsNilOrderId() async throws {
        let (sut, api) = makeSUT()

        await sut.registerPushToStart(token: "start-token")

        let command = try XCTUnwrap(api.registered.first)
        XCTAssertNil(command.orderId)
        XCTAssertEqual(command.token, "start-token")
        XCTAssertEqual(command.deviceId, "device-1")
    }

    func testDeregisterSendsOrderIdAndDeviceId() async throws {
        let (sut, api) = makeSUT()

        await sut.deregister(orderId: "order-9")

        let call = try XCTUnwrap(api.unregistered.first)
        XCTAssertEqual(call.orderId, "order-9")
        XCTAssertEqual(call.deviceId, "device-1")
    }

    func testFailedRegistrationIsSwallowed() async {
        let (sut, api) = makeSUT()
        api.registerError = ApiError(httpStatus: 409)

        await sut.register(orderId: "o", orderNumber: "n", token: "t")

        XCTAssertEqual(api.registered.count, 1)
    }
}

@MainActor
final class OrderLiveActivitySyncTests: XCTestCase {
    private let start = Date(timeIntervalSince1970: 1_700_000_000)

    private func order(statusValue: Int, history: [OrderStatusTrackDto] = []) -> CustomerOrderDetail {
        OrderFixtures.detail(
            id: "o1",
            statusCode: Code(type: "OrderStatus", name: nil, value: statusValue),
            displayOrderNumber: "1042",
            cleaningDateTime: start,
            estimatedMinutes: 90,
            statusHistory: history
        )
    }

    private func makeVM(_ client: FakeOrderClient, sync: SpyLiveActivitySync) -> OrderDetailViewModel {
        OrderDetailViewModel(
            orderId: "o1",
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
            marketStore: MarketFixtures.store().0,
            snackbar: SnackbarController(),
            eventBus: OrderEventBus(),
            liveActivity: sync,
            onCreditMoved: {},
            pollInterval: 3600
        )
    }

    func testActiveOrderStartsWithTheAppointmentWindow() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 3))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.started.count, 1)
        XCTAssertEqual(sync.started.first?.orderId, "o1")
        XCTAssertEqual(sync.started.first?.orderNumber, "1042")
        XCTAssertEqual(sync.started.first?.status, "onTheWay")
        XCTAssertEqual(sync.started.first?.window.scheduledStart, start)
        XCTAssertEqual(sync.started.first?.window.scheduledEnd, start.addingTimeInterval(90 * 60))
        XCTAssertTrue(sync.ended.isEmpty)
    }

    /// Confirmed means a cleaner has taken the job, not that anyone has set off — the service can still be
    /// days away. The card that used to open here claimed "your cleaner is heading over" and counted down
    /// to the appointment as if it were an arrival. The backend never starts one at Confirmed either
    /// (`LiveActivityEventKeys.ForStatus`), so a card opened here could only ever disagree with it.
    func testAConfirmedOrderOpensNoCardBecauseNobodyHasSetOffYet() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(
            statusValue: 2,
            history: [OrderFixtures.track(statusValue: 2, createdOn: start.addingTimeInterval(-2 * 86400))]
        ))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertTrue(sync.started.isEmpty, "a card opened before the cleaner was on the way")
        XCTAssertTrue(sync.updated.isEmpty)
        XCTAssertTrue(sync.ended.isEmpty)
    }

    func testInProgressOrderStartsAndUpdatesToCleaning() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 4))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        // The activity is started AND updated with "inProgress" so an already-cleaning order (or an
        // OnTheWay → InProgress transition on a re-fetch) renders "Cleaning in progress", not "On the way".
        XCTAssertEqual(sync.started.first?.status, "inProgress")
        XCTAssertEqual(sync.updated.first?.status, "inProgress")
        XCTAssertTrue(sync.ended.isEmpty)
    }

    func testInProgressWindowCarriesTheActualStartOffTheStatusHistory() async {
        let startedAt = start.addingTimeInterval(15 * 60)
        let client = FakeOrderClient()
        client.detailResults = [.success(order(
            statusValue: 4,
            history: [OrderFixtures.track(statusValue: 4, createdOn: startedAt)]
        ))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.started.first?.window.phaseStart, startedAt)
        XCTAssertEqual(sync.started.first?.window.phaseEnd, startedAt.addingTimeInterval(90 * 60))
    }

    /// The terminal status must travel with the end — it is what the ended card is left showing. Ending
    /// without one leaves the card on its last in-service state, which the system then draws as a stale
    /// placeholder.
    func testCompletedOrderEndsWithTheCompletedStatus() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 5))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended, [.init(orderId: "o1", orderNumber: "1042", status: .completed)])
        XCTAssertTrue(sync.started.isEmpty)
    }

    func testCancelledOrderEndsWithTheCancelledStatus() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 6))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended, [.init(orderId: "o1", orderNumber: "1042", status: .cancelled)])
    }

    /// The order number is the only identity a system-restored / server-started card carries
    /// (`CleanOrderAttributes` holds no order id), so the end must pass it through or such a card is never
    /// resolved and never ended.
    func testEndCarriesTheOrderNumberSoARestoredCardCanBeResolved() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 5))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertEqual(sync.ended.first?.orderNumber, "1042")
    }

    func testPendingOrderNeitherStartsNorEnds() async {
        let client = FakeOrderClient()
        client.detailResults = [.success(order(statusValue: 1))]
        let sync = SpyLiveActivitySync()

        await makeVM(client, sync: sync).load()

        XCTAssertTrue(sync.started.isEmpty)
        XCTAssertTrue(sync.ended.isEmpty)
    }
}

@MainActor
final class LiveActivitySessionCleanupTests: XCTestCase {
    func testLateAdoptionCannotResumeAfterLogoutIntoANewSession() async throws {
        guard #available(iOS 16.2, *) else { throw XCTSkip("ActivityKit cleanup requires iOS 16.2") }
        let container = try makeContainer()
        let activity = try makeOSOnlyActivity()
        let resolving = expectation(description: "The old session resolver is suspended")
        let staleRegistration = expectation(description: "Old adoption must not install a new session observer")
        staleRegistration.isInverted = true
        var continuation: CheckedContinuation<String?, Never>?
        defer {
            continuation?.resume(returning: nil)
            container.updatePushSession(hasSession: false)
            Task { await activity.end(nil, dismissalPolicy: .immediate) }
        }
        let api = SpyLiveActivityApi()
        api.onUnregister = { orderId in
            if orderId == "old-session-order" { staleRegistration.fulfill() }
        }
        LiveActivityCoordinator.shared.install(
            registrar: CustomerLiveActivityRegistrar(
                deviceIdProvider: StubDeviceIdProvider(deviceId: "synthetic-device"), api: api
            ),
            orderResolver: UnresolvedSessionTestOrder { _ in
                await withCheckedContinuation { pending in
                    continuation = pending
                    resolving.fulfill()
                }
            }
        )
        container.updatePushSession(hasSession: true)
        LiveActivityCoordinator.shared.beginActivityAdoption()
        await fulfillment(of: [resolving], timeout: 3)
        let pending = try XCTUnwrap(continuation)
        continuation = nil

        await makeAuth(container: container, tokens: tokensWithEmptyRefresh()).logout()
        container.updatePushSession(hasSession: true)
        // This resolver deliberately ignores task cancellation: the old result still arrives.
        pending.resume(returning: "old-session-order")
        await Task.yield()
        await activity.end(nil, dismissalPolicy: .immediate)
        await fulfillment(of: [staleRegistration], timeout: 1)

        XCTAssertFalse(api.unregistered.contains { $0.orderId == "old-session-order" })
        print("LIVE_ACTIVITY_RACE lateResolver oldSessionDeregisterCount=\(api.unregistered.count)")
        container.updatePushSession(hasSession: false)
    }

    func testSessionFalseEndsCapturedCardsWithoutEndingANewSessionCard() async throws {
        guard #available(iOS 16.2, *) else { throw XCTSkip("ActivityKit cleanup requires iOS 16.2") }
        let container = try makeContainer()
        container.updatePushSession(hasSession: true)
        let oldActivity = try makeOSOnlyActivity()
        defer {
            container.updatePushSession(hasSession: false)
            Task { await oldActivity.end(nil, dismissalPolicy: .immediate) }
        }
        let started = Date()

        container.updatePushSession(hasSession: false)
        container.updatePushSession(hasSession: true)
        // No suspension before this request: any queued wipe may end only its captured old set.
        let newActivity = try makeOSOnlyActivity()
        defer { Task { await newActivity.end(nil, dismissalPolicy: .immediate) } }
        await assertEnded(oldActivity, trigger: "sessionFalseCaptured", sample: 1, started: started)

        XCTAssertEqual(newActivity.activityState, .active)
        XCTAssertTrue(Activity<CleanOrderAttributes>.activities.contains { $0.id == newActivity.id })
        print("LIVE_ACTIVITY_RACE capturedOldSet newSessionRetainedActive=\(newActivity.activityState == .active)")
        await oldActivity.end(nil, dismissalPolicy: .immediate)
        await newActivity.end(nil, dismissalPolicy: .immediate)
        container.updatePushSession(hasSession: false)
    }

    func testNormalLogoutEndsAnOSOnlyActivity() async throws {
        guard #available(iOS 16.2, *) else { throw XCTSkip("ActivityKit cleanup requires iOS 16.2") }
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let tokens = tokensWithEmptyRefresh()
            let auth = makeAuth(container: container, tokens: tokens)
            let activity = try makeOSOnlyActivity()

            let started = Date()
            await auth.logout()

            XCTAssertNil(tokens.current())
            await assertEnded(activity, trigger: "normalLogout", sample: sample, started: started)
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }

    func testTerminalRefreshEndsAnOSOnlyActivity() async throws {
        guard #available(iOS 16.2, *) else { throw XCTSkip("ActivityKit cleanup requires iOS 16.2") }
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let tokens = FakeTokenStore.signedIn()
            let refresher = SessionRefresher(
                tokenStore: tokens, refreshClient: LiveActivityRejectedRefresh(),
                sessionManager: container.sessionManager, sessionScopedCaches: container.sessionScopedCaches
            )
            let activity = try makeOSOnlyActivity()

            let started = Date()
            let outcome = await refresher.refresh(triggeredBy: "token-1")

            XCTAssertEqual(outcome, .signedOut)
            XCTAssertNil(tokens.current())
            await assertEnded(activity, trigger: "terminalRefresh", sample: sample, started: started)
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }

    func testSuccessfulDeletionEndsAnOSOnlyActivity() async throws {
        guard #available(iOS 16.2, *) else { throw XCTSkip("ActivityKit cleanup requires iOS 16.2") }
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let tokens = FakeTokenStore.signedIn()
            let client = FakeGdprDeleteClient()
            let viewModel = DeleteAccountViewModel(
                client: client, authClient: makeAuth(container: container, tokens: tokens),
                snackbar: SnackbarController()
            )
            let activity = try makeOSOnlyActivity()

            let started = Date()
            await viewModel.confirmDelete()

            XCTAssertNil(tokens.current())
            XCTAssertEqual(client.deleteCallCount, 1)
            await assertEnded(activity, trigger: "successfulDeletion", sample: sample, started: started)
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }

    func testFailedDeletionPreservesTheSessionAndActivity() async throws {
        guard #available(iOS 16.2, *) else { throw XCTSkip("ActivityKit cleanup requires iOS 16.2") }
        for sample in 1 ... 5 {
            let container = try makeContainer()
            let tokens = FakeTokenStore.signedIn()
            let client = FakeGdprDeleteClient()
            client.deleteResult = .failure(ApiError(code: "gdpr.deletion_blocked_by_order", httpStatus: 400))
            let viewModel = DeleteAccountViewModel(
                client: client, authClient: makeAuth(container: container, tokens: tokens),
                snackbar: SnackbarController()
            )
            let activity = try makeOSOnlyActivity()

            await viewModel.confirmDelete()

            XCTAssertNotNil(tokens.current())
            XCTAssertEqual(activity.activityState, .active)
            XCTAssertTrue(Activity<CleanOrderAttributes>.activities.contains { $0.id == activity.id })
            print("LIVE_ACTIVITY_SESSION trigger=failedDeletion sample=\(sample) retainedActive=true")
            await activity.end(nil, dismissalPolicy: .immediate)
        }
    }

    private func makeContainer() throws -> CustomerAppContainer {
        if #available(iOS 16.2, *) {
            LiveActivityCoordinator.shared.install(
                registrar: NoopLiveActivityRegistering(), orderResolver: UnresolvedSessionTestOrder()
            )
        }
        return try CustomerAppContainer(
            snackbar: SnackbarController(), apiBaseURL: XCTUnwrap(URL(string: "http://127.0.0.1:1/"))
        )
    }

    private func tokensWithEmptyRefresh() -> FakeTokenStore {
        let future = Date(timeIntervalSinceNow: 600)
        return FakeTokenStore(AuthTokens(
            accessToken: "synthetic-access", accessTokenExpiresAt: future,
            refreshToken: "", refreshTokenExpiresAt: future
        ))
    }

    private func makeAuth(container: CustomerAppContainer, tokens: FakeTokenStore) -> AuthApiClient {
        AuthApiClient(
            apiBaseURL: container.apiBaseURL, tokenStore: tokens,
            headerAdapter: HeaderAdapter(deviceIdProvider: StubDeviceIdProvider(deviceId: "synthetic-device")),
            sessionScopedCaches: container.sessionScopedCaches
        )
    }

    @available(iOS 16.2, *)
    private func makeOSOnlyActivity() throws -> Activity<CleanOrderAttributes> {
        guard ActivityAuthorizationInfo().areActivitiesEnabled else {
            throw XCTSkip("Native ActivityKit is disabled for this test host; OS dismissal is unverified")
        }
        let orderNumber = "session-test-" + UUID().uuidString
        let now = Date()
        let state = CleanOrderAttributes.ContentState(
            v: 1, status: "inProgress", orderNumber: orderNumber,
            scheduledStart: now, scheduledEnd: now.addingTimeInterval(3600), phaseStart: nil, phaseEnd: nil
        )
        let activity: Activity<CleanOrderAttributes>
        do {
            activity = try Activity.request(
                attributes: CleanOrderAttributes(orderNumber: orderNumber),
                content: ActivityContent(state: state, staleDate: nil), pushType: nil
            )
        } catch {
            throw XCTSkip("Local ActivityKit request rejected without APNs: \(error); OS dismissal is unverified")
        }
        XCTAssertEqual(activity.activityState, .active)
        XCTAssertTrue(Activity<CleanOrderAttributes>.activities.contains { $0.id == activity.id })
        // Created through the native API, outside the coordinator's started map.
        return activity
    }

    @available(iOS 16.2, *)
    private func assertEnded(
        _ activity: Activity<CleanOrderAttributes>, trigger: String, sample: Int, started: Date
    ) async {
        let teardownSeconds = Date().timeIntervalSince(started)
        for _ in 0 ..< 20 {
            let listed = Activity<CleanOrderAttributes>.activities.contains { $0.id == activity.id }
            if !listed, activity.activityState == .ended || activity.activityState == .dismissed { break }
            try? await Task.sleep(nanoseconds: 100_000_000)
        }
        let active = Activity<CleanOrderAttributes>.activities.contains { $0.id == activity.id }
        print("LIVE_ACTIVITY_SESSION trigger=\(trigger) sample=\(sample) state=\(activity.activityState) "
            + "listed=\(active) teardownSeconds=\(teardownSeconds)")
        XCTAssertTrue(activity.activityState == .ended || activity.activityState == .dismissed)
        XCTAssertFalse(active)
    }
}

private struct LiveActivityRejectedRefresh: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .rejected
    }
}

private struct UnresolvedSessionTestOrder: LiveActivityOrderResolving {
    var resolve: @MainActor @Sendable (String) async -> String? = { _ in nil }

    func orderId(forOrderNumber orderNumber: String) async -> String? {
        await resolve(orderNumber)
    }
}
