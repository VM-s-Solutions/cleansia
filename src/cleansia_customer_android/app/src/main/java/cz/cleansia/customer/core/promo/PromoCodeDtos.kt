package cz.cleansia.customer.core.promo

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the customer Promo-code endpoints. Backend lives at
 * `POST /api/PromoCode/Validate` (Customer API) — see Loyalty Phase B.
 *
 * Promo validation is pure request/response — there is no long-lived state
 * worth caching, so this module exposes the API directly without a repository
 * (contrast with [cz.cleansia.customer.core.loyalty.LoyaltyRepository] which
 * caches the tier snapshot).
 */

/** Mirrors backend `ValidatePromoCode.Command` — `UserId` is filled server-side from JWT. */
@Serializable
data class ValidatePromoCodeRequest(
    val code: String,
    val orderSubtotal: Double,
)

/** Mirrors backend `ValidatePromoCode.Response`. */
@Serializable
data class ValidatePromoCodeResponse(
    val isValid: Boolean = false,
    val discountAmount: Double? = null,
    /** Backend returns the PromoCodeError enum stringified, e.g. "NotFound", "Expired". Null when isValid=true. */
    val errorCode: String? = null,
)

/**
 * Mirrors the backend `PromoCodeError` enum for client-side branching when
 * mapping to user-facing string resources. Decoded case-insensitively from
 * [ValidatePromoCodeResponse.errorCode].
 */
enum class PromoCodeError {
    NotFound,
    Inactive,
    Expired,
    NotYetValid,
    GlobalLimitReached,
    PerUserLimitReached,
    BelowMinimumOrderAmount,
    CurrencyMismatch;

    companion object {
        fun fromString(s: String?): PromoCodeError? =
            entries.firstOrNull { it.name.equals(s, ignoreCase = true) }
    }
}
