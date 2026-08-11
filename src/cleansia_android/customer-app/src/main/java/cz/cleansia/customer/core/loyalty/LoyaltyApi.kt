package cz.cleansia.customer.core.loyalty

import cz.cleansia.customer.api.client.LoyaltyApi as GenLoyaltyApi
import cz.cleansia.customer.api.model.GetLoyaltyActivityActivityItem as GenLoyaltyActivityItem
import cz.cleansia.customer.api.model.GetLoyaltyTiersResponse as GenGetLoyaltyTiersResponse
import cz.cleansia.customer.api.model.GetLoyaltyTiersTierInfo as GenTierInfo
import cz.cleansia.customer.api.model.GetLoyaltyTiersTierPerk as GenTiersTierPerk
import cz.cleansia.customer.api.model.GetMyLoyaltyResponse as GenGetMyLoyaltyResponse
import cz.cleansia.customer.api.model.GetMyLoyaltyTierPerk as GenMyTierPerk
import cz.cleansia.customer.api.model.LoyaltyTier as GenLoyaltyTier
import cz.cleansia.customer.api.model.PagedDataOfGetLoyaltyActivityActivityItem as GenPagedActivity
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenLoyaltyApi]. The hand-written DTOs in
 * [LoyaltyDtos.kt] use int codes for the tier / transaction-type / source
 * enums while the generated wire shape uses the enum classes; we collapse
 * back to the int representation since UI branches on those.
 */
class LoyaltyApi(
    private val loyaltyApi: GenLoyaltyApi,
) {
    suspend fun getMy(): Response<LoyaltyAccountDto> {
        val raw = loyaltyApi.loyaltyGetMy()
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun getActivity(offset: Int = 0, limit: Int = 20): Response<LoyaltyActivityResponseDto> {
        val raw = loyaltyApi.loyaltyGetActivity(offset = offset, limit = limit)
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun getTiers(): Response<LoyaltyTiersResponseDto> {
        val raw = loyaltyApi.loyaltyGetTiers()
        return raw.mapBody { it.toAppDto() }
    }
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → app DTO mappers ───

/**
 * `currentDiscountPercent` is money: it is the standing discount the customer is told every future
 * booking gets, and zero says the tier they earned is worth nothing. The tier and the two counters
 * behind it are refused with it — a defaulted `currentTier = 1` demotes a Gold member on screen, and
 * `lifetimePoints = 0` erases the progress bar that explains the tier.
 *
 * `pointsToNextTier` and `currentDiscountMinOrderAmount` stay nullable: the top tier has no next tier
 * and an unconditional discount has no floor.
 */
private fun GenGetMyLoyaltyResponse?.toAppDto(): LoyaltyAccountDto? {
    if (this == null) return null
    return LoyaltyAccountDto(
        currentTier = currentTier?.value ?: return null,
        lifetimePoints = lifetimePoints ?: return null,
        completedBookingsCount = completedBookingsCount ?: return null,
        tierAchievedOn = tierAchievedOn?.toString(),
        pointsToNextTier = pointsToNextTier,
        nextTier = nextTier?.value,
        currentDiscountPercent = currentDiscountPercent ?: return null,
        currentDiscountMinOrderAmount = currentDiscountMinOrderAmount,
        currentPerks = currentPerks.orEmpty().map { it.toAppDto() },
    )
}

private fun GenMyTierPerk.toAppDto(): TierPerkDto = TierPerkDto(
    icon = icon,
    labelKey = labelKey,
)

private fun GenTiersTierPerk.toAppDto(): TierPerkDto = TierPerkDto(
    icon = icon,
    labelKey = labelKey,
)

private fun GenGetLoyaltyTiersResponse?.toAppDto(): LoyaltyTiersResponseDto? {
    if (this == null) return null
    return LoyaltyTiersResponseDto(
        tiers = tiers.orEmpty().map { it.toAppDto() ?: return null },
    )
}

/**
 * The tier ladder is a promise about what a customer gets for spending more, so a defaulted
 * `discountPercent` or `lifetimePointsThreshold` misstates the deal rather than merely omitting it.
 * Refusing a row refuses the ladder: a ladder with a rung missing reads as a complete one.
 */
private fun GenTierInfo.toAppDto(): TierInfoDto? {
    return TierInfoDto(
        tier = tier?.value ?: return null,
        lifetimePointsThreshold = lifetimePointsThreshold ?: return null,
        discountPercent = discountPercent ?: return null,
        minimumOrderAmountForDiscount = minimumOrderAmountForDiscount,
        perks = perks.orEmpty().map { it.toAppDto() },
    )
}

private fun GenPagedActivity?.toAppDto(): LoyaltyActivityResponseDto? {
    if (this == null) return null
    return LoyaltyActivityResponseDto(
        pageNumber = pageNumber ?: return null,
        pageSize = pageSize ?: return null,
        total = total ?: return null,
        data = `data`.orEmpty().map { it.toAppDto() ?: return null },
    )
}

/**
 * `points` is the ledger line for a transaction the customer already made, and a zeroed one reads as
 * "this booking earned you nothing".
 */
private fun GenLoyaltyActivityItem.toAppDto(): LoyaltyActivityItemDto? {
    return LoyaltyActivityItemDto(
        // Generated activity item doesn't carry an `id`; UI uses it only as a list
        // key with a fallback (item.hashCode), so null is safe.
        id = null,
        type = type?.value ?: return null,
        points = points ?: return null,
        source = source?.value ?: return null,
        orderId = orderId,
        orderDisplayNumber = orderDisplayNumber,
        occurredOn = occurredOn?.toString(),
    )
}
