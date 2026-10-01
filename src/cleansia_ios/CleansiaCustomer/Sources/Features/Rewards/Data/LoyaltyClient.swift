import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol LoyaltyClient: Sendable {
    func getMy() async -> ApiResult<LoyaltyAccount>
    func getTiers() async -> ApiResult<[TierInfo]>
    func getActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage>
    func getCredit() async -> ApiResult<CustomerCredit>
}

struct LiveLoyaltyClient: LoyaltyClient {
    func getMy() async -> ApiResult<LoyaltyAccount> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerLoyaltyAPI.loyaltyGetMy().toDomain()
        }
    }

    func getTiers() async -> ApiResult<[TierInfo]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerLoyaltyAPI.loyaltyGetTiers().tiers?.map { try $0.toDomain() } ?? []
        }
    }

    func getActivity(offset: Int, limit: Int) async -> ApiResult<LoyaltyActivityPage> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let paged = try await CustomerLoyaltyAPI.loyaltyGetActivity(offset: offset, limit: limit)
            return try LoyaltyActivityPage(
                items: (paged.data ?? []).map { try $0.toDomain() },
                total: paged.total.require("total")
            )
        }
    }

    func getCredit() async -> ApiResult<CustomerCredit> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerCreditAPI.creditGetMy().toDomain()
        }
    }
}

/// **Refuse.** One object, no page and no row. `currentTier` coerced to `1` demotes a Gold customer
/// to Bronze on screen and re-labels every perk under it, and `currentDiscountPercent` coerced to `0`
/// withdraws a rate they earned. `pointsToNextTier` and `nextTier` are nullable by design — the top
/// tier has no next one, which the progress row already renders.
private extension GetMyLoyaltyResponse {
    func toDomain() throws -> LoyaltyAccount {
        try LoyaltyAccount(
            currentTier: currentTier.require("currentTier").rawValue,
            lifetimePoints: lifetimePoints.require("lifetimePoints"),
            completedBookingsCount: completedBookingsCount.require("completedBookingsCount"),
            tierAchievedOn: tierAchievedOn,
            pointsToNextTier: pointsToNextTier,
            nextTier: nextTier?.rawValue,
            currentDiscountPercent: currentDiscountPercent.require("currentDiscountPercent"),
            currentDiscountMinOrderAmount: currentDiscountMinOrderAmount,
            currentPerks: (currentPerks ?? []).map { TierPerk(icon: $0.icon, labelKey: $0.labelKey) }
        )
    }
}

/// **Refuse the page.** The tiers are one ordered ladder read against each other — a dropped rung
/// silently moves where the next one starts, and a `0` threshold puts a rung at the bottom of a
/// ladder it does not belong to.
private extension GetLoyaltyTiersTierInfo {
    func toDomain() throws -> TierInfo {
        try TierInfo(
            tier: tier.require("tier").rawValue,
            lifetimePointsThreshold: lifetimePointsThreshold.require("lifetimePointsThreshold"),
            discountPercent: discountPercent.require("discountPercent"),
            minimumOrderAmountForDiscount: minimumOrderAmountForDiscount,
            perks: (perks ?? []).map { TierPerk(icon: $0.icon, labelKey: $0.labelKey) }
        )
    }
}

/// **Refuse the page.** The ledger carries no identity to drop a row by, so a broken row can only
/// refuse — and `type` decides whether a row reads as points earned or points spent, which a default
/// of `1` answers for the customer.
private extension GetLoyaltyActivityActivityItem {
    func toDomain() throws -> LoyaltyActivityItem {
        try LoyaltyActivityItem(
            type: type.require("type").rawValue,
            points: points.require("points"),
            source: source.require("source").rawValue,
            orderId: orderId,
            orderDisplayNumber: orderDisplayNumber,
            occurredOn: occurredOn
        )
    }
}

/// **Refuse.** A balance is money owed, and a coerced `0` tells a customer holding 250 Kč that they hold
/// nothing; a blank currency states an amount in no unit. The share is refused too — the copy states it
/// as the cap, and a `0` would say credit never applies. A refusal leaves the credit unread, so the
/// screens hide it rather than state it wrong. → /decisions/adr-0048
extension GetMyCreditResponse {
    func toDomain() throws -> CustomerCredit {
        try CustomerCredit(
            primary: CustomerCredit.Balance(
                amount: balance.require("balance"),
                currencyCode: currencyCode.requireNonBlank("currencyCode"),
                expiresOn: expiresOn
            ),
            balances: (balances ?? []).map { try $0.toDomain() },
            maxShareOfOrder: maxShareOfOrder.require("maxShareOfOrder")
        )
    }
}

extension GetMyCreditCurrencyBalance {
    func toDomain() throws -> CustomerCredit.Balance {
        try CustomerCredit.Balance(
            amount: balance.require("balance"),
            currencyCode: currencyCode.requireNonBlank("currencyCode"),
            expiresOn: expiresOn
        )
    }
}
