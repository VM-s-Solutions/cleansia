import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class NotificationPreferencesViewModelTests: XCTestCase {
    private var client: FakeNotificationPreferencesClient!
    private var scheduler: TestScheduler<DispatchQueue.SchedulerTimeType, DispatchQueue.SchedulerOptions>!

    override func setUp() {
        super.setUp()
        client = FakeNotificationPreferencesClient()
        scheduler = .dispatch
    }

    private func makeVM() -> NotificationPreferencesViewModel {
        NotificationPreferencesViewModel(client: client, scheduler: scheduler.eraseToAnyScheduler())
    }

    func testLoadFetchesAndExposesPreferences() async {
        client.getMineResult = .success(ProfileFixtures.preferences(promo: true))
        let vm = makeVM()
        await vm.load()
        guard case let .loaded(prefs) = vm.state else { return XCTFail("expected loaded") }
        XCTAssertTrue(prefs.promo)
        XCTAssertEqual(client.getMineCallCount, 1)
    }

    func testLoadFailureTransitionsToError() async {
        client.getMineResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()
        guard case .error = vm.state else { return XCTFail("expected error") }
    }

    func testToggleAppliesOptimisticUpdateImmediately() async {
        client.getMineResult = .success(ProfileFixtures.preferences(promo: false))
        let vm = makeVM()
        await vm.load()

        vm.setCategory(.promo, enabled: true)

        guard case let .loaded(prefs) = vm.state else { return XCTFail("expected loaded") }
        XCTAssertTrue(prefs.promo)
        XCTAssertTrue(client.updatedPayloads.isEmpty)
    }

    func testRapidTogglesCoalesceIntoOnePut() async {
        client.getMineResult = .success(ProfileFixtures.preferences())
        let vm = makeVM()
        await vm.load()

        vm.setCategory(.promo, enabled: true)
        vm.setCategory(.orderUpdates, enabled: false)
        vm.setCategory(.disputeReply, enabled: false)

        XCTAssertTrue(client.updatedPayloads.isEmpty)

        scheduler.advance(by: .milliseconds(300))
        await settle()

        XCTAssertEqual(client.updatedPayloads.count, 1)
        let put = client.updatedPayloads[0]
        XCTAssertTrue(put.promo)
        XCTAssertFalse(put.orderUpdates)
        XCTAssertFalse(put.disputeReply)
    }

    func testSeparatedTogglesEachPut() async {
        client.getMineResult = .success(ProfileFixtures.preferences())
        let vm = makeVM()
        await vm.load()

        vm.setCategory(.promo, enabled: true)
        scheduler.advance(by: .milliseconds(300))
        await settle()

        vm.setCategory(.promo, enabled: false)
        scheduler.advance(by: .milliseconds(300))
        await settle()

        XCTAssertEqual(client.updatedPayloads.count, 2)
    }

    func testFailedPutRevertsTheOptimisticSnapshot() async {
        client.getMineResult = .success(ProfileFixtures.preferences(promo: false))
        client.updateResult = .failure(ApiError(httpStatus: 500))
        let vm = makeVM()
        await vm.load()

        vm.setCategory(.promo, enabled: true)
        scheduler.advance(by: .milliseconds(300))
        await settle()

        guard case let .loaded(prefs) = vm.state else { return XCTFail("expected loaded") }
        XCTAssertFalse(prefs.promo)
    }

    private func settle() async {
        for _ in 0 ..< 5 {
            await Task.yield()
        }
    }
}
