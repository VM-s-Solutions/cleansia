package cz.cleansia.customer.core.user

import kotlinx.serialization.Serializable

@Serializable
data class SavedAddressDto(
    val id: String,
    val label: String,
    val street: String,
    val city: String,
    val zipCode: String,
    val state: String? = null,
    val countryId: String,
    val country: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val isDefault: Boolean = false,
)

@Serializable
data class AddSavedAddressCommand(
    val label: String,
    val street: String,
    val city: String,
    val zipCode: String,
    val countryId: String? = null,
    val setAsDefault: Boolean,
    // Backend declares double Latitude / double Longitude (non-nullable). If we
    // send null, ASP.NET binds it to 0.0 and the address ends up at (0,0) off
    // the African coast — see Wave 1 Finding 2. Required at the call site;
    // AddressManagerScreen guards against this via its picker flow.
    val latitude: Double,
    val longitude: Double,
)

@Serializable
data class UpdateSavedAddressCommand(
    val savedAddressId: String,
    val label: String,
    val street: String,
    val city: String,
    val zipCode: String,
    val countryId: String? = null,
    val latitude: Double,
    val longitude: Double,
)

@Serializable
data class SetDefaultSavedAddressCommand(
    val savedAddressId: String,
)
