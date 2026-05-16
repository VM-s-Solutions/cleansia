package cz.cleansia.customer.core.validation

/**
 * Shared address field validation.
 * Rules mirror the web:
 *  - Street: 5–255 chars, required
 *  - City: 2–100 chars, required
 *  - ZIP: 3–20 chars alphanumeric/dash, required (Czech format e.g. "120 00" or "12000")
 */
object AddressValidation {
    fun streetError(value: String): AddressError? = when {
        value.isBlank() -> AddressError.Required
        value.length < 5 -> AddressError.TooShort
        value.length > 255 -> AddressError.TooLong
        else -> null
    }

    fun cityError(value: String): AddressError? = when {
        value.isBlank() -> AddressError.Required
        value.length < 2 -> AddressError.TooShort
        value.length > 100 -> AddressError.TooLong
        else -> null
    }

    private val zipRegex = Regex("^[A-Za-z0-9 -]{3,20}$")

    fun zipError(value: String): AddressError? = when {
        value.isBlank() -> AddressError.Required
        !zipRegex.matches(value) -> AddressError.InvalidZip
        else -> null
    }

    fun isValid(street: String, city: String, zip: String): Boolean =
        streetError(street) == null && cityError(city) == null && zipError(zip) == null
}

enum class AddressError {
    Required,
    TooShort,
    TooLong,
    InvalidZip,
}
