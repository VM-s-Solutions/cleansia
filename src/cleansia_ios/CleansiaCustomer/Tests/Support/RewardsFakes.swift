import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeLoyaltyClient: LoyaltyClient, @unchecked Sendable {
    var accountResult: ApiResult<LoyaltyAccount> = .success(LoyaltyFixtures.account())
    var tiersResult: ApiResult<[TierInfo]> = .success([])
    var activityPages: [LoyaltyActivityPage] = []
    var activityError: ApiError?
    private(set) var activityRequests: [(offset: Int, limit: Int)] = []
    private(set) var accountCallCount = 0
    private(set) var tiersCallCount = 0

    func getMy() async -> ApiResult<LoyaltyAccount> {
        accountCallCount += 1
        return accountResult
    }

    func getTiers() async -> ApiResult<[TierInfo]> {
        tiersCallCount += 1
        return tiersResult
    }

    func getActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage> {
        activityRequests.append((offset, limit))
        if let activityError { return .failure(activityError) }
        let index = min(activityRequests.count - 1, activityPages.count - 1)
        guard index >= 0 else { return .success(LoyaltyActivityPage(items: [], total: 0)) }
        return .success(activityPages[index])
    }
}

final class FakeRewardsReferralClient: RewardsReferralClient, @unchecked Sendable {
    var accountResult: ApiResult<ReferralAccount> = .success(ReferralFixtures.account())
    var referralsResult: ApiResult<ReferralListPage> = .success(ReferralListPage(items: [], total: 0))
    private(set) var accountCallCount = 0
    private(set) var referralsCallCount = 0

    func getMy() async -> ApiResult<ReferralAccount> {
        accountCallCount += 1
        return accountResult
    }

    func getMyReferrals(offset _: Int, limit _: Int) async -> ApiResult<ReferralListPage> {
        referralsCallCount += 1
        return referralsResult
    }
}

enum LoyaltyFixtures {
    static func account(
        currentTier: Int = 2,
        lifetimePoints: Int = 600,
        completedBookings: Int = 6,
        pointsToNextTier: Int? = 400,
        nextTier: Int? = 3,
        discountPercent: Double = 0.05,
        perks: [TierPerk] = [TierPerk(icon: "discount", labelKey: "loyalty.perks.priority_support")]
    ) -> LoyaltyAccount {
        LoyaltyAccount(
            currentTier: currentTier,
            lifetimePoints: lifetimePoints,
            completedBookingsCount: completedBookings,
            tierAchievedOn: nil,
            pointsToNextTier: pointsToNextTier,
            nextTier: nextTier,
            currentDiscountPercent: discountPercent,
            currentDiscountMinOrderAmount: nil,
            currentPerks: perks
        )
    }

    static func tier(_ value: Int, threshold: Int, discount: Double = 0, minOrder: Double? = nil) -> TierInfo {
        TierInfo(
            tier: value,
            lifetimePointsThreshold: threshold,
            discountPercent: discount,
            minimumOrderAmountForDiscount: minOrder,
            perks: []
        )
    }

    static func activityItem(
        points: Int = 100,
        source: Int = 1,
        orderNumber: String? = "1042",
        occurredOn: Date? = Date(timeIntervalSince1970: 1_700_000_000)
    ) -> LoyaltyActivityItem {
        LoyaltyActivityItem(
            type: points >= 0 ? 1 : 2,
            points: points,
            source: source,
            orderId: "o1",
            orderDisplayNumber: orderNumber,
            occurredOn: occurredOn
        )
    }
}

enum ReferralFixtures {
    static func account(
        code: String = "ABC123",
        accepted: Int = 0,
        qualified: Int = 0,
        pointsPerReferral: Int = 150
    ) -> ReferralAccount {
        ReferralAccount(
            code: code,
            timesUsed: 0,
            qualifiedCount: qualified,
            acceptedCount: accepted,
            pointsPerReferral: pointsPerReferral
        )
    }

    static func listItem(name: String = "Jana", status: Int = 1) -> ReferralListItem {
        ReferralListItem(
            id: UUID().uuidString,
            referredUserName: name,
            status: status,
            acceptedOn: nil,
            firstQualifyingOrderOn: nil,
            pointsAwardedToReferrer: nil
        )
    }
}
