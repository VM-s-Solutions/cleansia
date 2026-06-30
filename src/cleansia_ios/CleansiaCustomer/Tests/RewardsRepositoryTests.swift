import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class LoyaltyRepositoryTests: XCTestCase {
    func testRefreshCachesAccountAndTiers() async {
        let client = FakeLoyaltyClient()
        client.accountResult = .success(LoyaltyFixtures.account(currentTier: 3, lifetimePoints: 1200))
        client.tiersResult = .success([LoyaltyFixtures.tier(1, threshold: 0), LoyaltyFixtures.tier(2, threshold: 500)])
        let repo = LoyaltyRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(repo.account?.currentTier, 3)
        XCTAssertEqual(repo.tiers.count, 2)
        XCTAssertTrue(repo.loaded)
    }

    func testRefreshAccountFailureReturnsErrorAndStaysUnloaded() async {
        let client = FakeLoyaltyClient()
        client.accountResult = .failure(ApiError(httpStatus: 500))
        let repo = LoyaltyRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNotNil(result.apiErrorOrNil)
        XCTAssertNil(repo.account)
        XCTAssertFalse(repo.loaded)
    }

    func testTiersFailureDoesNotFailWholeRefresh() async {
        let client = FakeLoyaltyClient()
        client.accountResult = .success(LoyaltyFixtures.account())
        client.tiersResult = .failure(ApiError(httpStatus: 500))
        let repo = LoyaltyRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertNotNil(repo.account)
        XCTAssertTrue(repo.tiers.isEmpty)
        XCTAssertTrue(repo.loaded)
    }

    func testLoadActivityReturnsPage() async {
        let client = FakeLoyaltyClient()
        client.activityPages = [LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem()], total: 1)]
        let repo = LoyaltyRepository(client: client)

        let result = await repo.loadActivity(offset: 0, limit: 5)

        XCTAssertEqual(result.successOrNil?.items.count, 1)
        XCTAssertEqual(client.activityRequests.first?.limit, 5)
    }

    func testClearWipesCache() async {
        let client = FakeLoyaltyClient()
        let repo = LoyaltyRepository(client: client)
        await repo.refresh()

        await repo.clear()

        XCTAssertNil(repo.account)
        XCTAssertTrue(repo.tiers.isEmpty)
        XCTAssertFalse(repo.loaded)
    }
}

@MainActor
final class RewardsReferralRepositoryTests: XCTestCase {
    func testRefreshCachesAccountAndReferrals() async {
        let client = FakeRewardsReferralClient()
        client.accountResult = .success(ReferralFixtures.account(code: "ZZZ999", accepted: 2, qualified: 1))
        client.referralsResult = .success(ReferralListPage(items: [ReferralFixtures.listItem()], total: 1))
        let repo = RewardsReferralRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertEqual(repo.account?.code, "ZZZ999")
        XCTAssertEqual(repo.referrals.count, 1)
        XCTAssertTrue(repo.loaded)
    }

    func testReferralsFailureDoesNotFailWholeRefresh() async {
        let client = FakeRewardsReferralClient()
        client.accountResult = .success(ReferralFixtures.account())
        client.referralsResult = .failure(ApiError(httpStatus: 500))
        let repo = RewardsReferralRepository(client: client)

        let result = await repo.refresh()

        XCTAssertNil(result.apiErrorOrNil)
        XCTAssertNotNil(repo.account)
        XCTAssertTrue(repo.referrals.isEmpty)
    }

    func testClearWipesCache() async {
        let client = FakeRewardsReferralClient()
        let repo = RewardsReferralRepository(client: client)
        await repo.refresh()

        await repo.clear()

        XCTAssertNil(repo.account)
        XCTAssertTrue(repo.referrals.isEmpty)
        XCTAssertFalse(repo.loaded)
    }
}

private extension Result where Success == LoyaltyActivityPage, Failure == ApiError {
    var successOrNil: LoyaltyActivityPage? {
        if case let .success(value) = self { return value }
        return nil
    }
}
