package cz.cleansia.customer.core.data

import cz.cleansia.customer.core.user.AddSavedAddressCommand
import cz.cleansia.customer.core.user.SavedAddressDto
import cz.cleansia.customer.core.user.UpdateSavedAddressCommand
import kotlinx.serialization.Serializable

/**
 * Canonical user-owned address. Used by the home top bar, the booking flow,
 * and the profile's "Manage addresses" screen. Persisted in DataStore as JSON
 * via [AddressRepository].
 *
 * [id] is a stable local identity used by Flow diffing and the selection UI
 * (it never changes once an entry is created locally). [serverId] is populated
 * after the entry has been persisted to the backend — null means "not yet
 * synced" (guest mode, or a signed-in user whose POST has not completed).
 */
@Serializable
data class UserAddress(
    val id: String,
    val serverId: String? = null,
    val label: String,
    val street: String,
    val city: String,
    val zipCode: String,
    val country: String = "",
    val latitude: Double? = null,
    val longitude: Double? = null,
    val isDefault: Boolean = false,
) {
    /** Single-line rendering for compact rows. */
    val oneLine: String
        get() = listOfNotNull(
            street.takeIf { it.isNotBlank() },
            city.takeIf { it.isNotBlank() },
        ).joinToString(", ")

    /** Secondary line (zip + country), empty string when both are blank. */
    val secondLine: String
        get() = listOfNotNull(
            zipCode.takeIf { it.isNotBlank() },
            country.takeIf { it.isNotBlank() },
        ).joinToString(" · ")
}

internal fun SavedAddressDto.toUserAddress(): UserAddress = UserAddress(
    id = id,
    serverId = id,
    label = label,
    street = street,
    city = city,
    zipCode = zipCode,
    country = country.orEmpty(),
    latitude = latitude,
    longitude = longitude,
    isDefault = isDefault,
)

internal fun UserAddress.toAddCommand(setAsDefault: Boolean): AddSavedAddressCommand =
    AddSavedAddressCommand(
        label = label,
        street = street,
        city = city,
        zipCode = zipCode,
        state = null,
        countryId = null,
        setAsDefault = setAsDefault,
        latitude = latitude,
        longitude = longitude,
    )

internal fun UserAddress.toUpdateCommand(savedAddressId: String): UpdateSavedAddressCommand =
    UpdateSavedAddressCommand(
        savedAddressId = savedAddressId,
        label = label,
        street = street,
        city = city,
        zipCode = zipCode,
        state = null,
        countryId = null,
        latitude = latitude,
        longitude = longitude,
    )
