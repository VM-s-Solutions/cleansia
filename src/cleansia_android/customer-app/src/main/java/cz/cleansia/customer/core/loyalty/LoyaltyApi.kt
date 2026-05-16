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

private fun GenGetMyLoyaltyResponse?.toAppDto(): LoyaltyAccountDto = LoyaltyAccountDto(
    currentTier = this?.currentTier?.value ?: 1,
    lifetimePoints = this?.lifetimePoints ?: 0,
    completedBookingsCount = this?.completedBookingsCount ?: 0,
    tierAchievedOn = this?.tierAchievedOn?.toString(),
    pointsToNextTier = this?.pointsToNextTier,
    nextTier = this?.nextTier?.value,
    currentDiscountPercent = this?.currentDiscountPercent ?: 0.0,
    currentDiscountMinOrderAmount = this?.currentDiscountMinOrderAmount,
    currentPerks = this?.currentPerks?.map { it.toAppDto() }.orEmpty(),
)

private fun GenMyTierPerk.toAppDto(): TierPerkDto = TierPerkDto(
    icon = icon,
    labelKey = labelKey,
)

private fun GenTiersTierPerk.toAppDto(): TierPerkDto = TierPerkDto(
    icon = icon,
    labelKey = labelKey,
)

private fun GenGetLoyaltyTiersResponse?.toAppDto(): LoyaltyTiersResponseDto = LoyaltyTiersResponseDto(
    tiers = this?.tiers?.map { it.toAppDto() }.orEmpty(),
)

private fun GenTierInfo.toAppDto(): TierInfoDto = TierInfoDto(
    tier = tier?.value ?: 1,
    lifetimePointsThreshold = lifetimePointsThreshold ?: 0,
    discountPercent = discountPercent ?: 0.0,
    minimumOrderAmountForDiscount = minimumOrderAmountForDiscount,
    perks = perks?.map { it.toAppDto() }.orEmpty(),
)

private fun GenPagedActivity?.toAppDto(): LoyaltyActivityResponseDto = LoyaltyActivityResponseDto(
    pageNumber = this?.pageNumber ?: 0,
    pageSize = this?.pageSize ?: 0,
    total = this?.total ?: 0,
    data = this?.`data`?.map { it.toAppDto() }.orEmpty(),
)

private fun GenLoyaltyActivityItem.toAppDto(): LoyaltyActivityItemDto = LoyaltyActivityItemDto(
    // Generated activity item doesn't carry an `id`; UI uses it only as a list
    // key with a fallback (item.hashCode), so null is safe.
    id = null,
    type = type?.value ?: 1,
    points = points ?: 0,
    source = source?.value ?: 1,
    orderId = orderId,
    orderDisplayNumber = orderDisplayNumber,
    occurredOn = occurredOn?.toString(),
)
