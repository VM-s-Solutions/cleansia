package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.customer.api.client.ReceivableApi as GenReceivableApi
import cz.cleansia.customer.api.model.MyReceivableDto as GenMyReceivableDto
import kotlinx.datetime.Instant
import retrofit2.Response

/**
 * What the customer owes on one of their orders and has not settled. While any is open, new cash
 * bookings are refused; card bookings are not. [kind] is the backend's `ReceivableKind` ordinal.
 */
data class Receivable(
    val id: String,
    val orderId: String,
    val displayOrderNumber: String,
    val kind: Int,
    val amount: Double,
    val currencyCode: String,
    val createdOn: Instant,
)

class ReceivableApi(
    private val receivableApi: GenReceivableApi,
) {
    /**
     * Refuses the page rather than dropping a row: a debt missing from this list reads as "nothing to
     * pay" while the same debt keeps refusing the customer's cash bookings.
     */
    suspend fun getMine(): Response<List<Receivable>> =
        receivableApi.receivableGetMine().mapWire { rows -> rows.required("MyReceivableDto[]").map { it.toAppDto() } }

    suspend fun createPayLink(receivableId: String): Response<String> =
        receivableApi.receivableCreatePayLink(receivableId).mapWire {
            it.required("CreateReceivablePayLinkResponse").checkoutUrl.required("checkoutUrl")
        }
}

private fun GenMyReceivableDto.toAppDto(): Receivable =
    Receivable(
        id = id.required("id"),
        orderId = orderId.required("orderId"),
        displayOrderNumber = displayOrderNumber.required("displayOrderNumber"),
        kind = kind.required("kind").value.required("kind.value"),
        amount = amount.required("amount"),
        currencyCode = currencyCode.required("currencyCode"),
        createdOn = createdOn.required("createdOn"),
    )
