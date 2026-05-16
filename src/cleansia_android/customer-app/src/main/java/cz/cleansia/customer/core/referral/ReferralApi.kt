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

private fun GenGetMyReferralResponse?.toAppDto(): ReferralAccountDto = ReferralAccountDto(
    code = this?.code.orEmpty(),
    timesUsed = this?.timesUsed ?: 0,
    qualifiedCount = this?.qualifiedCount ?: 0,
    acceptedCount = this?.acceptedCount ?: 0,
    pointsPerReferral = this?.pointsPerReferral ?: 0,
)

private fun GenPagedReferralList?.toAppDto(): ReferralListResponseDto = ReferralListResponseDto(
    pageNumber = this?.pageNumber ?: 0,
    pageSize = this?.pageSize ?: 0,
    total = this?.total ?: 0,
    data = this?.`data`?.map { it.toAppDto() }.orEmpty(),
)

private fun GenReferralListItem.toAppDto(): ReferralListItemDto = ReferralListItemDto(
    id = id,
    // Generated field is `referredFirstName`; surface under the hand-written
    // name (UI calls it referredUserName, but the value is still anonymised).
    referredUserName = referredFirstName,
    status = status?.value ?: 1,
    acceptedOn = acceptedOn?.toString(),
    firstQualifyingOrderOn = firstQualifyingOrderOn?.toString(),
    pointsAwardedToReferrer = pointsAwardedToReferrer,
)

private fun GenValidateReferralResponse?.toAppDto(): ValidateReferralResponse = ValidateReferralResponse(
    isValid = this?.isValid ?: false,
    referrerFirstName = this?.referrerFirstName,
    errorCode = this?.errorCode,
)
