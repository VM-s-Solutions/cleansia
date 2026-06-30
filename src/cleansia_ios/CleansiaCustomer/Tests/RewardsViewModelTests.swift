import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class RewardsViewModelTests: XCTestCase {
    private func makeVM(
        _ loyalty: FakeLoyaltyClient,
        _ referral: FakeRewardsReferralClient
    ) -> (RewardsViewModel, LoyaltyRepository, RewardsReferralRepository) {
        let loyaltyRepo = LoyaltyRepository(client: loyalty)
        let referralRepo = RewardsReferralRepository(client: referral)
        let vm = RewardsViewModel(
            loyaltyRepository: loyaltyRepo,
            referralRepository: referralRepo,
            snackbar: SnackbarController()
        )
        return (vm, loyaltyRepo, referralRepo)
    }

    func testLoadSurfacesLoadedWithTierProgressAndPerks() async {
        let loyalty = FakeLoyaltyClient()
        loyalty.accountResult = .success(LoyaltyFixtures.account(
            currentTier: 2,
            lifetimePoints: 600,
            pointsToNextTier: 400,
            nextTier: 3,
            perks: [TierPerk(icon: "star", labelKey: "loyalty.perks.priority_support")]
        ))
        loyalty.tiersResult = .success([LoyaltyFixtures.tier(1, threshold: 0), LoyaltyFixtures.tier(2, threshold: 500)])
        loyalty.activityPages = [LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem()], total: 1)]
        let (vm, _, _) = makeVM(loyalty, FakeRewardsReferralClient())

        await vm.load()

        guard case let .loaded(content) = vm.state else {
            return XCTFail("expected loaded state")
        }
        XCTAssertEqual(content.account.currentTier, 2)
        XCTAssertEqual(content.account.pointsToNextTier, 400)
        XCTAssertEqual(content.account.currentPerks.count, 1)
        XCTAssertEqual(content.tiers.count, 2)
        XCTAssertEqual(content.activityPreview.count, 1)
    }

    func testFirstLoadFailureFlipsToError() async {
        let loyalty = FakeLoyaltyClient()
        loyalty.accountResult = .failure(ApiError(httpStatus: 500))
        let (vm, _, _) = makeVM(loyalty, FakeRewardsReferralClient())

        await vm.load()

        if case .error = vm.state {} else { XCTFail("expected error state") }
    }

    func testEmptyActivityStillLoadsContent() async {
        let loyalty = FakeLoyaltyClient()
        loyalty.activityPages = [LoyaltyActivityPage(items: [], total: 0)]
        let (vm, _, _) = makeVM(loyalty, FakeRewardsReferralClient())

        await vm.load()

        guard case let .loaded(content) = vm.state else {
            return XCTFail("expected loaded state")
        }
        XCTAssertTrue(content.activityPreview.isEmpty)
    }

    func testReferralCodePresentAfterLoad() async {
        let referral = FakeRewardsReferralClient()
        referral.accountResult = .success(ReferralFixtures.account(code: "JOIN50"))
        let (vm, _, _) = makeVM(FakeLoyaltyClient(), referral)

        await vm.load()

        guard case let .loaded(content) = vm.state else {
            return XCTFail("expected loaded state")
        }
        XCTAssertEqual(content.referral?.code, "JOIN50")
    }

    func testRefreshFailureWhileLoadedStaysLoaded() async {
        let loyalty = FakeLoyaltyClient()
        let (vm, _, _) = makeVM(loyalty, FakeRewardsReferralClient())
        await vm.load()

        loyalty.accountResult = .failure(ApiError(httpStatus: 500))
        await vm.refresh()

        if case .loaded = vm.state {} else { XCTFail("expected to stay loaded") }
    }
}

@MainActor
final class RewardsActivityViewModelTests: XCTestCase {
    private func makeVM(_ client: FakeLoyaltyClient) -> RewardsActivityViewModel {
        let repo = LoyaltyRepository(client: client)
        return RewardsActivityViewModel(loyaltyRepository: repo, snackbar: SnackbarController(), pageSize: 1)
    }

    func testRefreshReplacesPageZeroAndSetsLoaded() async {
        let client = FakeLoyaltyClient()
        client.activityPages = [LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem(orderNumber: "a")], total: 2)]
        let vm = makeVM(client)

        await vm.refresh()

        XCTAssertEqual(vm.state.loadedValue?.count, 1)
        XCTAssertTrue(vm.hasMore)
        XCTAssertEqual(vm.refreshPhase, .idle)
        XCTAssertEqual(client.activityRequests.first?.offset, 0)
    }

    func testLoadNextPageAppendsAdditively() async {
        let client = FakeLoyaltyClient()
        client.activityPages = [
            LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem(orderNumber: "a")], total: 2),
            LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem(orderNumber: "b")], total: 2)
        ]
        let vm = makeVM(client)
        await vm.refresh()

        await vm.loadNextPage()

        XCTAssertEqual(vm.state.loadedValue?.count, 2)
        XCTAssertFalse(vm.hasMore)
        XCTAssertEqual(client.activityRequests.last?.offset, 1)
    }

    func testLoadNextPageNoOpWhenExhausted() async {
        let client = FakeLoyaltyClient()
        client.activityPages = [LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem()], total: 1)]
        let vm = makeVM(client)
        await vm.refresh()

        await vm.loadNextPage()

        XCTAssertEqual(client.activityRequests.count, 1)
    }

    func testEmptyLoadedRendersAsEmptyNotError() async {
        let client = FakeLoyaltyClient()
        client.activityPages = [LoyaltyActivityPage(items: [], total: 0)]
        let vm = makeVM(client)

        await vm.refresh()

        XCTAssertEqual(vm.state.loadedValue?.isEmpty, true)
    }

    func testFirstLoadFailureFlipsToError() async {
        let client = FakeLoyaltyClient()
        client.activityError = ApiError(httpStatus: 500)
        let vm = makeVM(client)

        await vm.refresh()

        if case .error = vm.state {} else { XCTFail("expected error state") }
    }

    func testPullToRefreshFailureWhileLoadedStaysLoaded() async {
        let client = FakeLoyaltyClient()
        client.activityPages = [LoyaltyActivityPage(items: [LoyaltyFixtures.activityItem()], total: 1)]
        let vm = makeVM(client)
        await vm.refresh()

        client.activityError = ApiError(httpStatus: 500)
        await vm.pullToRefresh()

        XCTAssertEqual(vm.state.loadedValue?.count, 1)
    }
}
