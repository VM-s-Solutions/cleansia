package cz.cleansia.customer.core.user

import cz.cleansia.customer.api.client.SavedAddressApi as GenSavedAddressApi
import cz.cleansia.customer.api.model.AddSavedAddressCommand as GenAddSavedAddressCommand
import cz.cleansia.customer.api.model.SavedAddressDto as GenSavedAddressDto
import cz.cleansia.customer.api.model.SetDefaultSavedAddressCommand as GenSetDefaultSavedAddressCommand
import cz.cleansia.customer.api.model.UpdateSavedAddressCommand as GenUpdateSavedAddressCommand
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenSavedAddressApi]. Backend route
 * layout mirrors `Cleansia.Web.Customer.Controllers.SavedAddressController`.
 *
 * The hand-written [SavedAddressDto] keeps `id`, `label`, `street`, `city`,
 * `zipCode`, `countryId` as non-null so the address list view can render
 * without null-guards. We drop wire items missing any of these.
 */
class SavedAddressApi(
    private val savedAddressApi: GenSavedAddressApi,
) {
    suspend fun getMine(): Response<List<SavedAddressDto>> {
        val raw = savedAddressApi.savedAddressGetMine()
        return raw.mapBody { list -> list?.mapNotNull { it.toAppDto() }.orEmpty() }
    }

    suspend fun add(command: AddSavedAddressCommand): Response<SavedAddressDto> {
        val raw = savedAddressApi.savedAddressAdd(
            addSavedAddressCommand = GenAddSavedAddressCommand(
                label = command.label,
                street = command.street,
                city = command.city,
                zipCode = command.zipCode,
                countryId = command.countryId,
                setAsDefault = command.setAsDefault,
                latitude = command.latitude,
                longitude = command.longitude,
            ),
        )
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun update(command: UpdateSavedAddressCommand): Response<SavedAddressDto> {
        val raw = savedAddressApi.savedAddressUpdate(
            updateSavedAddressCommand = GenUpdateSavedAddressCommand(
                savedAddressId = command.savedAddressId,
                label = command.label,
                street = command.street,
                city = command.city,
                zipCode = command.zipCode,
                countryId = command.countryId,
                latitude = command.latitude,
                longitude = command.longitude,
            ),
        )
        return raw.mapBody { it?.toAppDto() }
    }

    suspend fun setDefault(command: SetDefaultSavedAddressCommand): Response<Unit> =
        savedAddressApi.savedAddressSetDefault(
            setDefaultSavedAddressCommand = GenSetDefaultSavedAddressCommand(
                savedAddressId = command.savedAddressId,
            ),
        )

    suspend fun delete(id: String): Response<Unit> = savedAddressApi.savedAddressDelete(id = id)
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → app DTO mappers ───
//
// Drop list items missing a load-bearing field. The list view's row component
// reads id/label/street/city/zipCode/countryId directly without null-guards.

private fun GenSavedAddressDto.toAppDto(): SavedAddressDto? {
    val id = id ?: return null
    val label = label ?: return null
    val street = street ?: return null
    val city = city ?: return null
    val zipCode = zipCode ?: return null
    val countryId = countryId ?: return null
    return SavedAddressDto(
        id = id,
        label = label,
        street = street,
        city = city,
        zipCode = zipCode,
        state = state,
        countryId = countryId,
        country = country,
        latitude = latitude,
        longitude = longitude,
        isDefault = isDefault ?: false,
    )
}
