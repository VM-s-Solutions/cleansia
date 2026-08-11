package cz.cleansia.customer.core.promo

import cz.cleansia.customer.api.client.PromoCodeApi as GenPromoCodeApi
import cz.cleansia.customer.api.model.ValidatePromoCodeCommand as GenValidatePromoCodeCommand
import cz.cleansia.customer.api.model.ValidatePromoCodeResponse as GenValidatePromoCodeResponse
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenPromoCodeApi]. Pure preview-validate
 * surface; the redeem step happens server-side inside `CreateOrder.Handler`.
 * Auth-only — gated on the backend by `Permission(CanRedeemPromoCode)`.
 */
class PromoCodeApi(
    private val promoCodeApi: GenPromoCodeApi,
) {
    suspend fun validate(body: ValidatePromoCodeRequest): Response<ValidatePromoCodeResponse> {
        val raw = promoCodeApi.promoCodeValidate(
            validatePromoCodeCommand = GenValidatePromoCodeCommand(
                code = body.code,
                orderSubtotal = body.orderSubtotal,
            ),
        )
        return raw.mapWire { it.toAppDto() }
    }
}

/**
 * `isValid = false` is the verdict that rejects the code, so defaulting to it charges the customer
 * full price for a discount the server just granted, and the sheet states a reason the server never
 * sent. `discountAmount` stays nullable — `nullable: true` in the spec, since an invalid code has no
 * discount — and `BookingViewModel` already requires it to be non-null before applying one.
 */
private fun GenValidatePromoCodeResponse?.toAppDto(): ValidatePromoCodeResponse {
    val verdict = required("ValidatePromoCodeResponse")
    return ValidatePromoCodeResponse(
        isValid = verdict.isValid.required("isValid"),
        discountAmount = verdict.discountAmount,
        errorCode = verdict.errorCode,
    )
}
