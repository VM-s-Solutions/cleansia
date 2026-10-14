import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

@MainActor
final class PartnerOrderDashboardInvalidationTests: XCTestCase {
    override func tearDown() {
        GenMockURLProtocol.handler = nil
        GenMockURLProtocol.deferredResponse = nil
        super.tearDown()
    }

    func testEachMaterialWriterInvalidatesDashboardOnlyAfterGeneratedSuccess() async {
        for writer in Writer.allCases {
            for status in [200, 400] {
                let shared = OrdersStaleness()
                shared.markDashboardFresh()
                let revision = shared.dashboardRevision
                let bodies = WireBodies()
                GeneratedWireSpine.install(recording: bodies) { _ in (status, Data("{}".utf8)) }
                let result = await writer.run(LivePartnerOrderClient(staleness: shared))
                XCTAssertEqual(bodies.paths, [writer.path], "\(writer), HTTP \(status)")
                XCTAssertEqual(shared.isDashboardStale, status == 200, "\(writer), HTTP \(status)")
                XCTAssertEqual(
                    shared.dashboardRevision,
                    revision + (status == 200 ? 1 : 0),
                    "\(writer), HTTP \(status)"
                )
                if status == 200 {
                    guard case .success = result else { return XCTFail("\(writer) should succeed") }
                } else {
                    guard case .failure = result else { return XCTFail("\(writer) should fail") }
                }
            }
        }
    }

    func testALaterSameSessionSuccessStillInvalidatesAfterAnotherWriter() async throws {
        let shared = OrdersStaleness()
        let entered = expectation(description: "older writer entered generated transport")
        let held = HeldResponse(entered: entered)
        GeneratedWireSpine.install(recording: WireBodies()) { _ in (200, Data("{}".utf8)) }
        GenMockURLProtocol.deferredResponse = { _, respond in held.capture(respond) }
        let client = LivePartnerOrderClient(staleness: shared)
        let old = Task { try await client.startOrder(orderId: "older").get() }
        await fulfillment(of: [entered], timeout: 2)
        GenMockURLProtocol.deferredResponse = nil
        _ = try await client.notifyOnTheWay(orderId: "newer").get()
        let afterNewer = shared.dashboardRevision
        shared.markDashboardFresh()
        GenMockURLProtocol.deferredResponse = nil
        held.release()
        _ = try await old.value
        XCTAssertTrue(shared.isDashboardStale)
        XCTAssertEqual(shared.dashboardRevision, afterNewer + 1)
    }

    func testOldSessionLateSuccessCannotRestaleTheNextAccountsDashboard() async throws {
        let shared = OrdersStaleness()
        let registry = SessionScopedCacheRegistry()
        registry.register(shared)
        let entered = expectation(description: "old session writer entered generated transport")
        let held = HeldResponse(entered: entered)
        GeneratedWireSpine.install(recording: WireBodies()) { _ in (200, Data("{}".utf8)) }
        GenMockURLProtocol.deferredResponse = { _, respond in held.capture(respond) }
        let old = Task { try await LivePartnerOrderClient(staleness: shared).startOrder(orderId: "old-account").get() }
        await fulfillment(of: [entered], timeout: 2)
        let oldEpoch = shared.sessionEpoch
        await registry.clearAll()
        XCTAssertNotEqual(shared.sessionEpoch, oldEpoch)
        shared.markDashboardFresh()
        let nextRevision = shared.dashboardRevision
        GenMockURLProtocol.deferredResponse = nil
        held.release()
        _ = try await old.value
        XCTAssertFalse(shared.isDashboardStale)
        XCTAssertEqual(shared.dashboardRevision, nextRevision)
    }

    private enum Writer: CaseIterable {
        case take, notify, start, complete, cashNotPaid, cashCollected, decline

        var path: String {
            switch self {
            case .take: "/api/Order/TakeOrder"
            case .notify: "/api/Order/NotifyOnTheWay"
            case .start: "/api/Order/StartOrder"
            case .complete: "/api/Order/CompleteOrder"
            case .cashNotPaid: "/api/Order/ReportCashNotPaid"
            case .cashCollected: "/api/Order/MarkCashCollected"
            case .decline: "/api/Order/DeclinePreferredOffer"
            }
        }

        func run(_ client: LivePartnerOrderClient) async -> ApiResult<Void> {
            switch self {
            case .take: await client.takeOrder(orderId: "order-1", acceptedWorkContractTextId: "text-1")
            case .notify: await client.notifyOnTheWay(orderId: "order-1")
            case .start: await client.startOrder(orderId: "order-1")
            case .complete: await client.completeOrder(orderId: "order-1", actualMinutes: nil, notes: nil)
            case .cashNotPaid: await client.reportCashNotPaid(orderId: "order-1")
            case .cashCollected: await client.markCashCollected(orderId: "order-1")
            case .decline: await client.declinePreferredOffer(orderId: "order-1")
            }
        }
    }
}

private final class HeldResponse: @unchecked Sendable {
    private let lock = NSLock()
    private let entered: XCTestExpectation
    private var response: ((Int, Data) -> Void)?

    init(entered: XCTestExpectation) {
        self.entered = entered
    }

    func capture(_ response: @escaping (Int, Data) -> Void) {
        lock.withLock { self.response = response }
        entered.fulfill()
    }

    func release() {
        let respond = lock.withLock {
            let respond = response
            response = nil
            return respond
        }
        respond?(200, Data("{}".utf8))
    }
}
