package cz.cleansia.customer.core.loyalty

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the customer Loyalty endpoints. Backend lives under
 * `/api/Loyalty/...` (Customer API). Phase A surface is read-only — no
 * redemption, no referrals (those land in Phase B/C).
 *
 * Tier values mirror backend `Cleansia.Core.Domain.Loyalty.LoyaltyTier`:
 *   1 = BronzeCleaner, 2 = SilverMopper, 3 = GoldPolisher, 4 = PlatinumSparkler.
 * `[SwaggerEnumAsInt]` on the backend means tier comes over as a raw int; UI
 * branches on the [LoyaltyTier] enum below for the localized label resource.
 */

/** Mirrors backend `GetMyLoyalty.Response`. */
@Serializable
data class LoyaltyAccountDto(
    /** Backend tier int — see [LoyaltyTier]. */
    val currentTier: Int = 1,
    val lifetimePoints: Int = 0,
    val completedBookingsCount: Int = 0,
    /** ISO-8601 UTC. */
    val tierAchievedOn: String? = null,
    val pointsToNextTier: Int? = null,
    /** Backend tier int when present, null when already at top tier. */
    val nextTier: Int? = null,
    /** 0..1 — UI multiplies by 100 for display. */
    val currentDiscountPercent: Double = 0.0,
    val currentDiscountMinOrderAmount: Double? = null,
    val currentPerks: List<TierPerkDto> = emptyList(),
)

/** Mirrors backend `GetLoyaltyTiers.Response`. */
@Serializable
data class LoyaltyTiersResponseDto(
    val tiers: List<TierInfoDto> = emptyList(),
)

@Serializable
data class TierInfoDto(
    val tier: Int = 1,
    val lifetimePointsThreshold: Int = 0,
    val discountPercent: Double = 0.0,
    val minimumOrderAmountForDiscount: Double? = null,
    val perks: List<TierPerkDto> = emptyList(),
)

@Serializable
data class TierPerkDto(
    val icon: String? = null,
    val labelKey: String? = null,
)

/** Mirrors backend `GetLoyaltyActivity` — paged transactions. */
@Serializable
data class LoyaltyActivityResponseDto(
    val pageNumber: Int = 0,
    val pageSize: Int = 0,
    val total: Int = 0,
    val data: List<LoyaltyActivityItemDto> = emptyList(),
)

@Serializable
data class LoyaltyActivityItemDto(
    val id: String? = null,
    /** 1 = Earn, 2 = Revoke. */
    val type: Int = 1,
    /** Signed — positive on Earn, negative on Revoke. */
    val points: Int = 0,
    /** 1 = OrderCompleted, 2 = OrderCancelled, 3 = Referral, 4 = ManualGrant. */
    val source: Int = 1,
    val orderId: String? = null,
    val orderDisplayNumber: String? = null,
    val occurredOn: String? = null,
)

/**
 * Local enum for branching UI on tier; mirror of backend `LoyaltyTier` int values.
 * Decoupled from the wire so we can add UI-only states (Loading/Unknown) later.
 */
enum class LoyaltyTier(val value: Int) {
    BronzeCleaner(1),
    SilverMopper(2),
    GoldPolisher(3),
    PlatinumSparkler(4);

    companion object {
        fun fromValue(v: Int?): LoyaltyTier? = entries.firstOrNull { it.value == v }
    }
}

enum class LoyaltyTransactionType(val value: Int) {
    Earn(1),
    Revoke(2);

    companion object {
        fun fromValue(v: Int?): LoyaltyTransactionType? = entries.firstOrNull { it.value == v }
    }
}

enum class LoyaltyEarnSource(val value: Int) {
    OrderCompleted(1),
    OrderCancelled(2),
    Referral(3),
    ManualGrant(4);

    companion object {
        fun fromValue(v: Int?): LoyaltyEarnSource? = entries.firstOrNull { it.value == v }
    }
}
