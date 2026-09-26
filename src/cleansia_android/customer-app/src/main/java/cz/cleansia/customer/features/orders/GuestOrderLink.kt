package cz.cleansia.customer.features.orders

private val TOKEN_PARAMETER = Regex("[?&]token=([^&#\\s]+)")

/**
 * What the guest pastes is whatever their mail app gave them — usually the whole tracking link,
 * sometimes just the token out of it. Only the token opens the booking.
 */
fun guestAccessTokenFrom(pasted: String): String {
    val trimmed = pasted.trim()
    return TOKEN_PARAMETER.find(trimmed)?.groupValues?.get(1) ?: trimmed
}
