package cz.cleansia.customer.core.orders

import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.CancelGuestOrderCommand
import cz.cleansia.customer.api.model.GetGuestCancellationFeePreviewQuery
import cz.cleansia.customer.api.model.LookupOrderQuery
import retrofit2.Response

data class GuestOrderDto(
    val id: String,
    val displayOrderNumber: String,
    val cleaningDateTime: String?,
    val totalPrice: Double,
    val currencyCode: String,
    val status: Int,
)

class GuestOrderApi(private val api: GenOrderApi) {
    suspend fun lookup(number: String, email: String, code: String): Response<GuestOrderDto> =
        api.orderLookup(LookupOrderQuery(number, email, code)).mapWire { body ->
            val order = body.required("LookupOrderResponse")
            GuestOrderDto(
                id = order.id.required("id"),
                displayOrderNumber = order.displayOrderNumber.required("displayOrderNumber"),
                cleaningDateTime = order.cleaningDateTime?.toString(),
                totalPrice = order.totalPrice.required("totalPrice"),
                currencyCode = order.currency?.code?.takeIf { it.isNotBlank() }.required("currency.code"),
                status = order.orderStatus?.value.required("orderStatus.value"),
            )
        }

    suspend fun preview(number: String, email: String, code: String): Response<CancellationFeePreviewDto> =
        api.orderGuestCancellationPreview(
            GetGuestCancellationFeePreviewQuery(number, email, code),
        ).mapWire { it.toAppDto() }

    suspend fun cancel(
        number: String,
        email: String,
        code: String,
        reason: String?,
        language: String,
    ): Response<CancelOrderResponse> =
        api.orderCancelGuest(
            CancelGuestOrderCommand(
                displayOrderNumber = number,
                email = email,
                confirmationCode = code,
                reason = reason,
                language = language,
            ),
        ).mapWire { it.toAppDto() }
}
