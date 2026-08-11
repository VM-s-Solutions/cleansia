package cz.cleansia.customer.core.booking

import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.api.model.AddressDto as GenAddressDto
import cz.cleansia.customer.api.model.CreateOrderCommand as GenCreateOrderCommand
import cz.cleansia.customer.api.model.CreateOrderResponse as GenCreateOrderResponse
import cz.cleansia.customer.api.model.PaymentType as GenPaymentType
import cz.cleansia.customer.api.model.QuoteOrderCommand as GenQuoteOrderCommand
import cz.cleansia.customer.api.model.QuoteOrderResponse as GenQuoteOrderResponse
import kotlinx.datetime.Instant
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenOrderApi] for the booking flow's
 * Quote + CreateOrder calls. The hand-written DTOs in [BookingDtos.kt] keep
 * load-bearing non-null fields (e.g. [QuoteOrderResponse.currencyId]) so
 * downstream booking screens don't need to thread `?` types.
 */
class BookingApi(
    private val orderApi: GenOrderApi,
) {
    suspend fun quote(command: QuoteOrderCommand): Response<QuoteOrderResponse> {
        val raw = orderApi.orderQuote(command.toWire())
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun create(command: CreateOrderCommand): Response<CreateOrderResponse> {
        val raw = orderApi.orderCreateOrder(command.toWire())
        return raw.mapBody { it?.toAppDto() }
    }
}

/**
 * Re-wrap a [Response] preserving status + headers but mapping the body.
 * Mapping may legally produce `null` (server gave a malformed payload —
 * caller treats that as a soft-failure and shows a generic error).
 */
private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── App → generated mappers (request side) ───

private fun QuoteOrderCommand.toWire(): GenQuoteOrderCommand = GenQuoteOrderCommand(
    selectedServiceIds = selectedServiceIds,
    selectedPackageIds = selectedPackageIds,
    rooms = rooms,
    bathrooms = bathrooms,
    currencyId = currencyId,
    selectedExtraSlugs = selectedExtraSlugs,
    cleaningDate = cleaningDate?.let { Instant.parse(it) },
)

private fun CreateOrderCommand.toWire(): GenCreateOrderCommand = GenCreateOrderCommand(
    customerName = customerName,
    customerEmail = customerEmail,
    customerPhone = customerPhone,
    customerAddress = customerAddress?.toWire(),
    savedAddressId = savedAddressId,
    selectedPackageIds = selectedPackageIds,
    selectedServiceIds = selectedServiceIds,
    rooms = rooms,
    bathrooms = bathrooms,
    extras = extras,
    cleaningDate = Instant.parse(cleaningDate),
    paymentType = paymentType.toWirePaymentType(),
    currencyId = currencyId,
    totalPrice = totalPrice,
    language = language,
    promoCode = promoCode,
    referralCode = referralCode,
    preferredEmployeeId = preferredEmployeeId,
    specialInstructions = specialInstructions,
    accessInstructions = accessInstructions,
)

private fun CreateOrderAddressDto.toWire(): GenAddressDto = GenAddressDto(
    street = street,
    city = city,
    zipCode = zipCode,
    countryId = countryId,
    state = state,
)

private fun Int.toWirePaymentType(): GenPaymentType? = when (this) {
    1 -> GenPaymentType._1
    2 -> GenPaymentType._2
    else -> null
}

// ─── Generated → app mappers (response side) ───
//
// Drop the response entirely (return null → caller sees a 200-with-null body)
// when a load-bearing required field is missing.

/**
 * `QuoteOrder_Response` declares no `required` array, so the generator types every property
 * optional-with-null regardless of `nullable: false` and the whole contract lands here. Nothing on
 * this response is defaulted: it is the number the customer agrees to before they pay, and a zero
 * this client invented is a price the server never sent.
 *
 * `exchangeRate` is the one that looked harmless — it defaulted to `1.0`, which is not neutral but a
 * silent claim of parity, off by 24.75× on a CZK order priced in EUR. `appliedDiscountSource`
 * defaulted to `0` = None, reporting no discount over a total that already had one deducted.
 *
 * The three nullable-by-design discounts stay nullable: absent means "no tier discount", which is a
 * different sentence from "a 0 Kč tier discount applied".
 */
private fun GenQuoteOrderResponse.toAppDto(): QuoteOrderResponse? {
    return QuoteOrderResponse(
        totalPrice = totalPrice ?: return null,
        finalPriceAfterDiscount = finalPriceAfterDiscount ?: return null,
        originalSubtotal = originalSubtotal ?: return null,
        appliedDiscountSource = appliedDiscountSource?.value ?: return null,
        tierDiscountAmount = tierDiscountAmount,
        membershipDiscountAmount = membershipDiscountAmount,
        tierDiscountMinOrderAmount = tierDiscountMinOrderAmount,
        currencyId = currencyId ?: return null,
        currencyCode = currencyCode ?: return null,
        servicesSubtotal = servicesSubtotal ?: return null,
        packagesSubtotal = packagesSubtotal ?: return null,
        extrasSubtotal = extrasSubtotal ?: return null,
        expressSurchargeApplied = expressSurchargeApplied ?: return null,
        expressSurchargeAmount = expressSurchargeAmount ?: return null,
        expressSurchargeWaivedByMembership = expressSurchargeWaivedByMembership ?: return null,
        exchangeRate = exchangeRate ?: return null,
    )
}

private fun GenCreateOrderResponse.toAppDto(): CreateOrderResponse? {
    val id = id ?: return null
    val confirmationCode = confirmationCode ?: return null
    return CreateOrderResponse(
        id = id,
        confirmationCode = confirmationCode,
        stripeSessionId = stripeSessionId,
    )
}
