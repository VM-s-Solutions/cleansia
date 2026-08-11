package cz.cleansia.customer.core.user

import cz.cleansia.customer.api.client.SavedAddressApi as GenSavedAddressApi
import cz.cleansia.customer.api.model.AddSavedAddressCommand as GenAddSavedAddressCommand
import cz.cleansia.customer.api.model.SavedAddressDto as GenSavedAddressDto
import cz.cleansia.customer.api.model.SetDefaultSavedAddressCommand as GenSetDefaultSavedAddressCommand
import cz.cleansia.customer.api.model.UpdateSavedAddressCommand as GenUpdateSavedAddressCommand
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenSavedAddressApi]. Backend route
 * layout mirrors `Cleansia.Web.Customer.Controllers.SavedAddressController`.
 *
 * The hand-written [SavedAddressDto] keeps `id`, `label`, `street`, `city`,
 * `zipCode`, `countryId` as non-null so the address list view can render
 * without null-guards. A wire item missing any of these refuses the list.
 */
class SavedAddressApi(
    private val savedAddressApi: GenSavedAddressApi,
) {
    /**
     * The body is refused rather than defaulted to empty. Of ADR-0048 amendment B1's three
     * conditions, the first and third fail outright: an empty saved-address list is the app telling
     * a returning customer they have never saved an address, and [AddressRepository.refreshFromServer]
     * writes that answer to DataStore — so the default does not merely render wrong, it **deletes the
     * addresses off the handset**. The second condition holds (nothing sums or paginates them) and
     * does not rescue it; B1 needs all three.
     */
    suspend fun getMine(): Response<List<SavedAddressDto>> {
        val raw = savedAddressApi.savedAddressGetMine()
        return raw.mapWire { list -> list.required("SavedAddressDto[]").map { it.toAppDto() } }
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
        return raw.mapWire { it.required("SavedAddressDto").toAppDto() }
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
        return raw.mapWire { it.required("SavedAddressDto").toAppDto() }
    }

    suspend fun setDefault(command: SetDefaultSavedAddressCommand): Response<Unit> =
        savedAddressApi.savedAddressSetDefault(
            setDefaultSavedAddressCommand = GenSetDefaultSavedAddressCommand(
                savedAddressId = command.savedAddressId,
            ),
        )

    suspend fun delete(id: String): Response<Unit> = savedAddressApi.savedAddressDelete(id = id)
}

// ─── Generated → app DTO mappers ───
//
// Refuses the list rather than dropping the row. Both `HomeTab` and `BookingBottomSheet` preselect
// `addresses.firstOrNull { it.isDefault } ?: addresses.firstOrNull()`, so a dropped default does not
// leave the picker empty — the fallback chain guarantees a plausible substitute, and the customer
// books a cleaning to a different home than the one the screen has always defaulted to. `isDefault`
// is refused for the same reason: at `false` on every row the same fallback picks whichever address
// happens to be first.

private fun GenSavedAddressDto.toAppDto(): SavedAddressDto =
    SavedAddressDto(
        id = id.required("id"),
        label = label.required("label"),
        street = street.required("street"),
        city = city.required("city"),
        zipCode = zipCode.required("zipCode"),
        state = state,
        countryId = countryId.required("countryId"),
        country = country,
        latitude = latitude,
        longitude = longitude,
        isDefault = isDefault.required("isDefault"),
    )
