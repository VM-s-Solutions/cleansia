import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol LoyaltyClient: Sendable {
    func getMy() async -> ApiResult<LoyaltyAccount>
    func getTiers() async -> ApiResult<[TierInfo]>
    func getActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage>
}

struct LiveLoyaltyClient: LoyaltyClient {
    func getMy() async -> ApiResult<LoyaltyAccount> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerLoyaltyAPI.loyaltyGetMy()
        }
        return result.map { $0.toDomain() }
    }

    func getTiers() async -> ApiResult<[TierInfo]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerLoyaltyAPI.loyaltyGetTiers()
        }
        return result.map { ($0.tiers ?? []).map { $0.toDomain() } }
    }

    func getActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerLoyaltyAPI.loyaltyGetActivity(offset: offset, limit: limit)
        }
        return result.map { paged in
            LoyaltyActivityPage(items: (paged.data ?? []).map { $0.toDomain() }, total: paged.total ?? 0)
        }
    }
}

private extension GetMyLoyaltyResponse {
    func toDomain() -> LoyaltyAccount {
        LoyaltyAccount(
            currentTier: currentTier?.rawValue ?? 1,
            lifetimePoints: lifetimePoints ?? 0,
            completedBookingsCount: completedBookingsCount ?? 0,
            tierAchievedOn: tierAchievedOn,
            pointsToNextTier: pointsToNextTier,
            nextTier: nextTier?.rawValue,
            currentDiscountPercent: currentDiscountPercent ?? 0,
            currentDiscountMinOrderAmount: currentDiscountMinOrderAmount,
            currentPerks: (currentPerks ?? []).map { TierPerk(icon: $0.icon, labelKey: $0.labelKey) }
        )
    }
}

private extension GetLoyaltyTiersTierInfo {
    func toDomain() -> TierInfo {
        TierInfo(
            tier: tier?.rawValue ?? 1,
            lifetimePointsThreshold: lifetimePointsThreshold ?? 0,
            discountPercent: discountPercent ?? 0,
            minimumOrderAmountForDiscount: minimumOrderAmountForDiscount,
            perks: (perks ?? []).map { TierPerk(icon: $0.icon, labelKey: $0.labelKey) }
        )
    }
}

private extension GetLoyaltyActivityActivityItem {
    func toDomain() -> LoyaltyActivityItem {
        LoyaltyActivityItem(
            type: type?.rawValue ?? 1,
            points: points ?? 0,
            source: source?.rawValue ?? 1,
            orderId: orderId,
            orderDisplayNumber: orderDisplayNumber,
            occurredOn: occurredOn
        )
    }
}
