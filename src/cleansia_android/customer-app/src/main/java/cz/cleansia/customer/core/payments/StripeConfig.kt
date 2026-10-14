package cz.cleansia.customer.core.payments

class StripeConfig(val publishableKey: String) {
    val isCardPaymentAvailable: Boolean = publishableKey.isNotBlank()
}
