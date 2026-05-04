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
    val state: String? = null,
    val countryId: String? = null,
    val setAsDefault: Boolean,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val userId: String = "",
)

@Serializable
data class UpdateSavedAddressCommand(
    val savedAddressId: String,
    val label: String,
    val street: String,
    val city: String,
    val zipCode: String,
    val state: String? = null,
    val countryId: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val userId: String = "",
)

@Serializable
data class SetDefaultSavedAddressCommand(
    val savedAddressId: String,
    val userId: String = "",
)
