package cz.cleansia.customer.core.booking

import kotlinx.serialization.EncodeDefault
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.Serializable

@Serializable
data class QuoteOrderCommand(
    val selectedServiceIds: List<String>,
    val selectedPackageIds: List<String>,
    val rooms: Int,
    val bathrooms: Int,
    val currencyId: String? = null,
)

@Serializable
data class QuoteOrderResponse(
    val totalPrice: Double,
    val currencyId: String,
    val currencyCode: String,
    val servicesSubtotal: Double,
    val packagesSubtotal: Double,
    val exchangeRate: Double,
)

@Serializable
data class CreateOrderAddressDto(
    val street: String,
    val city: String,
    val zipCode: String,
    val countryId: String? = null,
    val state: String? = null,
)

@OptIn(ExperimentalSerializationApi::class)
@Serializable
data class CreateOrderCommand(
    val customerName: String,
    val customerEmail: String,
    val customerPhone: String,
    val customerAddress: CreateOrderAddressDto? = null,
    val savedAddressId: String? = null,
    val selectedPackageIds: List<String>,
    val selectedServiceIds: List<String>,
    val rooms: Int,
    val bathrooms: Int,
    // Backend validator requires `extras` on the wire even when empty.
    // @EncodeDefault forces serialization so the default {} round-trips.
    @EncodeDefault(EncodeDefault.Mode.ALWAYS)
    val extras: Map<String, Boolean> = emptyMap(),
    // ISO-8601 UTC — kotlinx-datetime's Instant.toString() format.
    val cleaningDate: String,
    // 1 = Cash, 2 = Card. Matches backend PaymentType enum.
    val paymentType: Int,
    val currencyId: String? = null,
    /**
     * Loyalty Phase B — uppercase canonical code (e.g. "WELCOME20") when the
     * user entered one and the client-side `Validate` call returned isValid.
     * Null otherwise. Backend re-validates inside the handler — this field is
     * not authoritative, just the user's intent.
     */
    val promoCode: String? = null,
    /**
     * Loyalty Phase C — optional late-acceptance referral code. Backend's
     * `CreateOrder.Handler` checks for an existing Referral row first and
     * silently skips when the user has already been referred or the code is
     * invalid, so the client always sends the raw user-entered value when
     * non-blank — no client-side gating beyond trim/uppercase.
     */
    val referralCode: String? = null,
    val totalPrice: Double,
    /**
     * Plus-only — id of an employee the user has worked with before. Backend
     * validates the relationship (must have a Completed order with this
     * cleaner) and boosts the matching score so they're more likely to be
     * assigned. Null means no preference.
     */
    val preferredEmployeeId: String? = null,
    @EncodeDefault(EncodeDefault.Mode.ALWAYS)
    val language: String = "en",
)

@Serializable
data class CreateOrderResponse(
    val id: String,
    val confirmationCode: String,
    val stripeSessionId: String? = null,
)
