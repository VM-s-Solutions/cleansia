import CleansiaCore
import Combine
import XCTest
@testable import CleansiaCustomer

/// A read still in flight when the session ends must not answer into the next one (S11): it must publish
/// nothing after `clear()`, mark nothing fresh, and leave no loading latch that swallows the next account's
/// first refresh. → /mobile-app/patterns#session-wipe
@MainActor
final class SessionCacheGuardTests: XCTestCase {
    private let cancelled = ApiError(code: ApiError.cancelledCode)

    // MARK: - Loyalty

    func testALoyaltyReadHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "loyalty read entered")
        let client = FakeLoyaltyClient()
        client.holdAccount = true
        client.accountStarted = { _ in entered.fulfill() }
        let repository = LoyaltyRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseAccount(call: 1, result: .success(LoyaltyFixtures.account(lifetimePoints: 900)))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertNil(repository.account)
        XCTAssertNil(repository.credit)
        XCTAssertFalse(repository.loaded)
        XCTAssertTrue(repository.staleness.isStale)
        XCTAssertFalse(repository.loading)
    }

    func testANewSessionsLoyaltyReadIsNotSwallowedByTheOldOne() async {
        let oldEntered = expectation(description: "old loyalty read entered")
        let newEntered = expectation(description: "new loyalty read entered")
        let client = FakeLoyaltyClient()
        client.holdAccount = true
        client.accountStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = LoyaltyRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()

        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseAccount(call: 2, result: .success(LoyaltyFixtures.account(lifetimePoints: 10)))
        _ = await new.value
        client.releaseAccount(call: 1, result: .success(LoyaltyFixtures.account(lifetimePoints: 900)))
        _ = await old.value

        XCTAssertEqual(client.accountCallCount, 2)
        XCTAssertEqual(repository.account?.lifetimePoints, 10)
        XCTAssertFalse(repository.staleness.isStale)
        XCTAssertFalse(repository.loading)
    }

    /// The account and the tiers have answered and the credit read is the one still in flight.
    func testALoyaltyCreditReadHeldAcrossClearRestoresNothing() async {
        let creditEntered = expectation(description: "credit read entered")
        let tiersPublished = expectation(description: "tiers published")
        let client = FakeLoyaltyClient()
        client.tiersResult = .success([LoyaltyFixtures.tier(1, threshold: 0)])
        client.holdCredit = true
        client.creditStarted = { _ in creditEntered.fulfill() }
        let repository = LoyaltyRepository(client: client)
        let tiers = repository.$tiers.first { !$0.isEmpty }.sink { _ in tiersPublished.fulfill() }
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [creditEntered, tiersPublished], timeout: 2)

        await repository.clear()
        client.releaseCredit(call: 1, result: .success(LoyaltyFixtures.credit(balance: 500)))
        let result = await refresh.value
        tiers.cancel()

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertNil(repository.credit)
        XCTAssertFalse(repository.loaded)
        XCTAssertTrue(repository.staleness.isStale)
        XCTAssertFalse(repository.loading)
    }

    // MARK: - Referrals

    func testAReferralReadHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "referral read entered")
        let client = FakeRewardsReferralClient()
        client.holdAccount = true
        client.accountStarted = { _ in entered.fulfill() }
        let repository = RewardsReferralRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseAccount(call: 1, result: .success(ReferralFixtures.account(code: "PREVIOUS")))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertNil(repository.account)
        XCTAssertFalse(repository.loaded)
        XCTAssertFalse(repository.loading)
    }

    func testANewSessionsReferralReadIsNotSwallowedByTheOldOne() async {
        let oldEntered = expectation(description: "old referral read entered")
        let newEntered = expectation(description: "new referral read entered")
        let client = FakeRewardsReferralClient()
        client.holdAccount = true
        client.accountStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = RewardsReferralRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()

        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseAccount(call: 2, result: .success(ReferralFixtures.account(code: "MINE")))
        _ = await new.value
        client.releaseAccount(call: 1, result: .success(ReferralFixtures.account(code: "PREVIOUS")))
        _ = await old.value

        XCTAssertEqual(repository.account?.code, "MINE")
        XCTAssertFalse(repository.loading)
    }

    /// The account has answered and the referrals page is the read still in flight.
    func testAReferralsPageHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "referrals page entered")
        let client = FakeRewardsReferralClient()
        client.holdReferrals = true
        client.referralsStarted = { _ in entered.fulfill() }
        let repository = RewardsReferralRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseReferrals(call: 1, result: .success(ReferralListPage(
            items: [ReferralFixtures.listItem(name: "Previous")],
            total: 1
        )))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertTrue(repository.referrals.isEmpty)
        XCTAssertNil(repository.account)
        XCTAssertFalse(repository.loaded)
        XCTAssertFalse(repository.loading)
    }

    // MARK: - Orders

    func testAnOrdersReadHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "orders read entered")
        let client = FakeOrderClient()
        client.holdPages = true
        client.pageStarted = { _ in entered.fulfill() }
        let repository = OrderRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releasePage(call: 1, result: .success(OrdersPage(
            items: [OrderFixtures.summary(id: "previous", statusValue: 2)],
            total: 1
        )))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertTrue(repository.orders.isEmpty)
        XCTAssertEqual(repository.totalRecords, 0)
        XCTAssertFalse(repository.loaded)
        XCTAssertTrue(repository.staleness.isStale)
        XCTAssertFalse(repository.loading)
    }

    func testANewSessionsOrdersReadIsNotSwallowedByTheOldOne() async {
        let oldEntered = expectation(description: "old orders read entered")
        let newEntered = expectation(description: "new orders read entered")
        let client = FakeOrderClient()
        client.holdPages = true
        client.pageStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = OrderRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()

        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releasePage(call: 2, result: .success(OrdersPage(
            items: [OrderFixtures.summary(id: "mine", statusValue: 2)],
            total: 1
        )))
        _ = await new.value
        client.releasePage(call: 1, result: .success(OrdersPage(
            items: [OrderFixtures.summary(id: "previous", statusValue: 2)],
            total: 1
        )))
        _ = await old.value

        XCTAssertEqual(repository.orders.map(\.id), ["mine"])
        XCTAssertFalse(repository.loading)
    }

    func testAnOrdersNextPageHeldAcrossClearAppendsNothing() async {
        let entered = expectation(description: "next page entered")
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "previous-1", statusValue: 2)], total: 5)]
        let repository = OrderRepository(client: client)
        await repository.refresh()
        client.holdPages = true
        client.pageStarted = { call in if call == 2 { entered.fulfill() } }
        let nextPage = Task { await repository.loadNextPage() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releasePage(call: 2, result: .success(OrdersPage(
            items: [OrderFixtures.summary(id: "previous-2", statusValue: 2)],
            total: 5
        )))
        _ = await nextPage.value

        XCTAssertTrue(repository.orders.isEmpty)
        XCTAssertEqual(repository.totalRecords, 0)
        XCTAssertFalse(repository.loadingMore)
    }

    // MARK: - Disputes

    func testADisputesReadHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "disputes read entered")
        let client = FakeDisputeClient()
        client.holdPages = true
        client.pageStarted = { _ in entered.fulfill() }
        let repository = DisputeRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releasePage(call: 1, result: .success(disputes("previous", total: 1)))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertTrue(repository.disputes.isEmpty)
        XCTAssertFalse(repository.loaded)
        XCTAssertFalse(repository.loading)
    }

    func testANewSessionsDisputesReadIsNotSwallowedByTheOldOne() async {
        let oldEntered = expectation(description: "old disputes read entered")
        let newEntered = expectation(description: "new disputes read entered")
        let client = FakeDisputeClient()
        client.holdPages = true
        client.pageStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = DisputeRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()

        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releasePage(call: 2, result: .success(disputes("mine", total: 1)))
        _ = await new.value
        client.releasePage(call: 1, result: .success(disputes("previous", total: 1)))
        _ = await old.value

        XCTAssertEqual(repository.disputes.map(\.id), ["mine"])
        XCTAssertFalse(repository.loading)
    }

    func testADisputesNextPageHeldAcrossClearAppendsNothing() async {
        let entered = expectation(description: "next page entered")
        let client = FakeDisputeClient()
        client.pages = [disputes("previous-1", total: 5)]
        let repository = DisputeRepository(client: client)
        await repository.refresh()
        client.holdPages = true
        client.pageStarted = { call in if call == 2 { entered.fulfill() } }
        let nextPage = Task { await repository.loadNextPage() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releasePage(call: 2, result: .success(disputes("previous-2", total: 5)))
        _ = await nextPage.value

        XCTAssertTrue(repository.disputes.isEmpty)
        XCTAssertEqual(repository.totalRecords, 0)
        XCTAssertFalse(repository.loadingMore)
    }

    // MARK: - Recurring schedules

    func testARecurringReadHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "schedules read entered")
        let client = FakeRecurringBookingClient()
        client.holdMine = true
        client.mineStarted = { _ in entered.fulfill() }
        let repository = RecurringBookingRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseMine(call: 1, result: .success([RecurringFixtures.template(id: "previous")]))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertTrue(repository.templates.isEmpty)
        XCTAssertFalse(repository.loaded)
        XCTAssertFalse(repository.loading)
    }

    func testANewSessionsRecurringReadIsNotSwallowedByTheOldOne() async {
        let oldEntered = expectation(description: "old schedules read entered")
        let newEntered = expectation(description: "new schedules read entered")
        let client = FakeRecurringBookingClient()
        client.holdMine = true
        client.mineStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = RecurringBookingRepository(client: client)
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()

        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseMine(call: 2, result: .success([RecurringFixtures.template(id: "mine")]))
        _ = await new.value
        client.releaseMine(call: 1, result: .success([RecurringFixtures.template(id: "previous")]))
        _ = await old.value

        XCTAssertEqual(repository.templates.map(\.id), ["mine"])
        XCTAssertFalse(repository.loading)
    }

    func testTheRereadAfterASaveHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "post-save read entered")
        let client = FakeRecurringBookingClient()
        client.holdMine = true
        client.mineStarted = { _ in entered.fulfill() }
        let repository = RecurringBookingRepository(client: client)
        let delete = Task { await repository.delete(templateId: "tpl-1") }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseMine(call: 1, result: .success([RecurringFixtures.template(id: "previous")]))
        _ = await delete.value

        XCTAssertTrue(repository.templates.isEmpty)
        XCTAssertFalse(repository.loaded)
    }

    // MARK: - Saved addresses

    func testASavedAddressReadHeldAcrossClearRestoresNothing() async throws {
        let entered = expectation(description: "addresses read entered")
        let client = FakeSavedAddressClient()
        client.holdMine = true
        client.mineStarted = { _ in entered.fulfill() }
        let repository = try SavedAddressRepository(client: client, defaults: isolatedDefaults())
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseMine(call: 1, result: .success([SavedAddressFixtures.address(id: "previous")]))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertTrue(repository.addresses.isEmpty)
        XCTAssertFalse(repository.loaded)
        XCTAssertFalse(repository.loading)
    }

    func testANewSessionsSavedAddressReadIsNotSwallowedByTheOldOne() async throws {
        let oldEntered = expectation(description: "old addresses read entered")
        let newEntered = expectation(description: "new addresses read entered")
        let client = FakeSavedAddressClient()
        client.holdMine = true
        client.mineStarted = { call in
            if call == 1 { oldEntered.fulfill() } else { newEntered.fulfill() }
        }
        let repository = try SavedAddressRepository(client: client, defaults: isolatedDefaults())
        let old = Task { await repository.refresh() }
        await fulfillment(of: [oldEntered], timeout: 2)
        await repository.clear()

        let new = Task { await repository.refresh() }
        await fulfillment(of: [newEntered], timeout: 2)
        client.releaseMine(call: 2, result: .success([SavedAddressFixtures.address(id: "mine")]))
        _ = await new.value
        client.releaseMine(call: 1, result: .success([SavedAddressFixtures.address(id: "previous")]))
        _ = await old.value

        XCTAssertEqual(repository.addresses.map(\.id), ["mine"])
        XCTAssertFalse(repository.loading)
    }

    func testTheRereadAfterAnAddressSaveHeldAcrossClearRestoresNothing() async throws {
        let entered = expectation(description: "post-save read entered")
        let client = FakeSavedAddressClient()
        client.holdMine = true
        client.mineStarted = { _ in entered.fulfill() }
        let repository = try SavedAddressRepository(client: client, defaults: isolatedDefaults())
        let setDefault = Task { await repository.setDefault(id: "addr-1") }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseMine(call: 1, result: .success([SavedAddressFixtures.address(id: "previous")]))
        _ = await setDefault.value

        XCTAssertTrue(repository.addresses.isEmpty)
        XCTAssertFalse(repository.loaded)
    }

    // MARK: - Profile

    func testAProfileReadHeldAcrossClearRestoresNothing() async {
        let entered = expectation(description: "profile read entered")
        let client = FakeUserProfileClient()
        client.holdCurrentUser = true
        client.currentUserStarted = { _ in entered.fulfill() }
        let repository = UserProfileRepository(client: client)
        let refresh = Task { await repository.refresh() }
        await fulfillment(of: [entered], timeout: 2)

        await repository.clear()
        client.releaseCurrentUser(call: 1, result: .success(ProfileFixtures.user(id: "previous")))
        let result = await refresh.value

        XCTAssertEqual(result.apiErrorOrNil, cancelled)
        XCTAssertNil(repository.currentUser)
    }

    private func disputes(_ id: String, total: Int) -> DisputesPage {
        DisputesPage(items: [DisputeFixtures.entry(id: id)], total: total)
    }

    private func isolatedDefaults() throws -> UserDefaults {
        let suite = "SessionCacheGuardTests.\(UUID().uuidString)"
        addTeardownBlock { UserDefaults().removePersistentDomain(forName: suite) }
        return try XCTUnwrap(UserDefaults(suiteName: suite))
    }
}
