package cz.cleansia.customer.core.referral
import cz.cleansia.core.auth.AuthInterceptor

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the customer Referral endpoints (Loyalty Phase C).
 *
 * Backend lives under `/api/Referral/...` (Customer API). Three operations:
 *  - `GetMy` returns the user's lifetime code + denormalised counters.
 *  - `GetMyReferrals` pages who-I-invited rows (anonymised first names only).
 *  - `Validate` is `[AllowAnonymous]` — usable from the signup form before the
 *    first JWT exists, so the [cz.cleansia.core.auth.AuthInterceptor]'s
 *    no-token tolerance carries the call through with no Authorization header.
 *
 * `[SwaggerEnumAsInt]` on the backend means [ReferralStatus] arrives as a raw
 * int; UI branches on the local enum below.
 */

/** Mirrors backend `GetMyReferral.Response`. */
@Serializable
data class ReferralAccountDto(
    val code: String,
    val timesUsed: Int,
    val qualifiedCount: Int,
    val acceptedCount: Int,
    val pointsPerReferral: Int,
)

/** Mirrors backend `GetMyReferrals` paged list item. */
@Serializable
data class ReferralListItemDto(
    val id: String,
    /** First name only — backend anonymises last names. */
    val referredUserName: String? = null,
    /** 1=Accepted, 2=Qualified, 3=Expired, 4=Reversed. */
    val status: Int,
    val acceptedOn: String? = null,
    val firstQualifyingOrderOn: String? = null,
    val pointsAwardedToReferrer: Int? = null,
)

@Serializable
data class ReferralListResponseDto(
    val pageNumber: Int,
    val pageSize: Int,
    val total: Int,
    val data: List<ReferralListItemDto> = emptyList(),
)

@Serializable
data class ValidateReferralRequest(
    val code: String,
)

@Serializable
data class ValidateReferralResponse(
    val isValid: Boolean,
    val referrerFirstName: String? = null,
    /** Backend `ReferralValidationError` enum stringified. Null when isValid=true. */
    val errorCode: String? = null,
)

/** Local enum mirror for client-side branching on [ReferralListItemDto.status]. */
enum class ReferralStatus(val value: Int) {
    Accepted(1),
    Qualified(2),
    Expired(3),
    Reversed(4);

    companion object {
        fun fromValue(v: Int?): ReferralStatus? = entries.firstOrNull { it.value == v }
    }
}

/**
 * Mirrors backend `ReferralValidationError` enum. Decoded case-insensitively
 * from [ValidateReferralResponse.errorCode] for mapping to localized strings.
 */
enum class ReferralValidationError {
    NotFound,
    SelfReferral,
    AlreadyReferred,
    Inactive;

    companion object {
        fun fromString(s: String?): ReferralValidationError? =
            entries.firstOrNull { it.name.equals(s, ignoreCase = true) }
    }
}
