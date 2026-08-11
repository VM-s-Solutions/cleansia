package cz.cleansia.customer.core.referral
import cz.cleansia.core.auth.AuthInterceptor

import cz.cleansia.customer.api.client.ReferralApi as GenReferralApi
import cz.cleansia.customer.api.model.GetMyReferralResponse as GenGetMyReferralResponse
import cz.cleansia.customer.api.model.GetMyReferralsReferralListItem as GenReferralListItem
import cz.cleansia.customer.api.model.PagedDataOfGetMyReferralsReferralListItem as GenPagedReferralList
import cz.cleansia.customer.api.model.ValidateReferralQuery as GenValidateReferralQuery
import cz.cleansia.customer.api.model.ValidateReferralResponse as GenValidateReferralResponse
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenReferralApi] (Loyalty Phase C). The
 * GET endpoints need auth; the Validate POST is `[AllowAnonymous]` — the
 * shared AuthInterceptor skips the bearer header when no token is present.
 */
class ReferralApi(
    private val referralApi: GenReferralApi,
) {
    suspend fun getMy(): Response<ReferralAccountDto> {
        val raw = referralApi.referralGetMy()
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun getMyReferrals(offset: Int = 0, limit: Int = 20): Response<ReferralListResponseDto> {
        val raw = referralApi.referralGetMyReferrals(offset = offset, limit = limit)
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun validate(body: ValidateReferralRequest): Response<ValidateReferralResponse> {
        val raw = referralApi.referralValidate(
            validateReferralQuery = GenValidateReferralQuery(code = body.code),
        )
        return raw.mapBody { it.toAppDto() }
    }
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → app DTO mappers ───

/**
 * The counters are what `InviteFriendsCard` renders — "3 joined · 2 qualified" — so a defaulted zero
 * tells a customer that nobody they invited ever signed up, over a programme that already owes them
 * points. `pointsPerReferral` is the payout the same card advertises. `code` is refused rather than
 * blanked because the card is gated on `code.isNotBlank()`: an empty one removes the invite feature
 * from the screen instead of reporting that it could not be loaded.
 */
private fun GenGetMyReferralResponse?.toAppDto(): ReferralAccountDto? {
    if (this == null) return null
    return ReferralAccountDto(
        code = code ?: return null,
        timesUsed = timesUsed ?: return null,
        qualifiedCount = qualifiedCount ?: return null,
        acceptedCount = acceptedCount ?: return null,
        pointsPerReferral = pointsPerReferral ?: return null,
    )
}

/**
 * Refuses the page, where the orders list drops the row, because this list is the **itemisation of a
 * figure rendered beside it**. The accepted/qualified counters on the Rewards tab come from
 * `GetMy` — a different endpoint, computed server-side over every referral, not over this page — so a
 * silently dropped row leaves the breakdown contradicting a count on the same screen, with no
 * "showing N of M" affordance to absorb the difference. The orders list's drop ruling turns on the
 * opposite fact: there the badges count the rows actually shown.
 *
 * That inverts the identity half with it — a referral with no id fails the page rather than vanishing
 * from the breakdown — and it matches the loyalty ledger, which is the same "here is what you earned,
 * line by line" surface on the same tab.
 */
private fun GenPagedReferralList?.toAppDto(): ReferralListResponseDto? {
    if (this == null) return null
    return ReferralListResponseDto(
        pageNumber = pageNumber ?: return null,
        pageSize = pageSize ?: return null,
        total = total ?: return null,
        data = `data`.orEmpty().map { it.toAppDto() ?: return null },
    )
}

/**
 * `status` is the payout state of one referral and `1` is `Accepted` — a real state meaning "joined,
 * has not qualified yet" — so a default reports an unpaid invite over one the server already
 * qualified. `pointsAwardedToReferrer` stays nullable: `nullable: true` in the spec, because a
 * referral that has not qualified has genuinely been awarded nothing.
 */
private fun GenReferralListItem.toAppDto(): ReferralListItemDto? {
    return ReferralListItemDto(
        id = id ?: return null,
        // Generated field is `referredFirstName`; surface under the hand-written
        // name (UI calls it referredUserName, but the value is still anonymised).
        referredUserName = referredFirstName,
        status = status?.value ?: return null,
        acceptedOn = acceptedOn?.toString(),
        firstQualifyingOrderOn = firstQualifyingOrderOn?.toString(),
        pointsAwardedToReferrer = pointsAwardedToReferrer,
    )
}

/**
 * `isValid = false` is the answer that rejects a code, so defaulting to it turns a broken wire field
 * into "that code is not valid" on the signup form — the customer loses the joining bonus and the
 * referrer loses theirs, and the screen states a reason the server never gave.
 */
private fun GenValidateReferralResponse?.toAppDto(): ValidateReferralResponse? {
    if (this == null) return null
    return ValidateReferralResponse(
        isValid = isValid ?: return null,
        referrerFirstName = referrerFirstName,
        errorCode = errorCode,
    )
}
