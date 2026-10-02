package cz.cleansia.customer.core.payments

import com.stripe.android.paymentsheet.PaymentSheet
import cz.cleansia.customer.BuildConfig

/**
 * Bundle of values PaymentSheet needs to render: the PaymentIntent client_secret it confirms against,
 * plus the Stripe customer id + ephemeral key only when the card is to be saved. Given the customer,
 * PaymentSheet draws its own save box on an intent that keeps nothing, and a card saved through it
 * gets no SavedCards row and no consent.
 */
data class PaymentSheetParams(
    val clientSecret: String,
    val ephemeralKey: String?,
    val customerId: String?,
    /** The currency the PaymentIntent is minted in, which Google Pay is told up front. */
    val currencyCode: String?,
)

fun CreatePaymentIntentResponse.toPaymentSheetParams(saveCard: Boolean, currencyCode: String?) =
    PaymentSheetParams(
        clientSecret = clientSecret,
        ephemeralKey = ephemeralKey.takeIf { saveCard },
        customerId = stripeCustomerId.takeIf { saveCard },
        currencyCode = currencyCode,
    )

fun PaymentSheetParams.toConfiguration(): PaymentSheet.Configuration = PaymentSheet.Configuration(
    merchantDisplayName = "Cleansia",
    customer = customerId?.let { id ->
        ephemeralKey?.let { key -> PaymentSheet.CustomerConfiguration(id = id, ephemeralKeySecret = key) }
    },
    googlePay = PaymentSheet.GooglePayConfiguration(
        // Follows the Stripe key, not the build type — a release build on a pk_test_ key must still ask
        // Google Pay for Test, or the sheet fails after the user has committed. Derived in
        // build.gradle.kts from the key prefix so the two cannot desync.
        environment = if (BuildConfig.GOOGLE_PAY_PRODUCTION) {
            PaymentSheet.GooglePayConfiguration.Environment.Production
        } else {
            PaymentSheet.GooglePayConfiguration.Environment.Test
        },
        // Stripe: "The two-letter ISO 3166 code of the country of your business" — the merchant
        // account, not the order.
        countryCode = "CZ",
        currencyCode = currencyCode,
    ),
    allowsDelayedPaymentMethods = false,
)
