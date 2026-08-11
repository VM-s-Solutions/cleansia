import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol SavedAddressClient: Sendable {
    func getMine() async -> ApiResult<[SavedAddress]>
    func add(_ draft: SavedAddressDraft) async -> ApiResult<SavedAddress>
    func update(id: String, draft: SavedAddressDraft) async -> ApiResult<SavedAddress>
    func setDefault(id: String) async -> ApiResult<Void>
    func delete(id: String) async -> ApiResult<Void>
}

struct LiveSavedAddressClient: SavedAddressClient {
    func getMine() async -> ApiResult<[SavedAddress]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressGetMine().map { try $0.toDomain() }
        }
    }

    func add(_ draft: SavedAddressDraft) async -> ApiResult<SavedAddress> {
        let command = AddSavedAddressCommand(
            label: draft.label,
            street: draft.street,
            city: draft.city,
            zipCode: draft.zipCode,
            countryId: nil,
            setAsDefault: draft.setAsDefault,
            latitude: draft.latitude,
            longitude: draft.longitude
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressAdd(addSavedAddressCommand: command).toDomain()
        }
    }

    func update(id: String, draft: SavedAddressDraft) async -> ApiResult<SavedAddress> {
        let command = UpdateSavedAddressCommand(
            savedAddressId: id,
            label: draft.label,
            street: draft.street,
            city: draft.city,
            zipCode: draft.zipCode,
            countryId: nil,
            latitude: draft.latitude,
            longitude: draft.longitude
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressUpdate(updateSavedAddressCommand: command).toDomain()
        }
    }

    func setDefault(id: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressSetDefault(
                setDefaultSavedAddressCommand: SetDefaultSavedAddressCommand(savedAddressId: id)
            )
        }
    }

    func delete(id: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressDelete(id: id)
        }
    }
}

/// **Refuse the page.** The saved addresses are alternatives to each other — the list IS the picker,
/// so a dropped row is a home the customer can no longer book to rather than a shorter list — and
/// `isDefault` decides which one the booking flow pre-selects, so a coerced `false` can send a
/// cleaner to the wrong address of theirs.
///
/// `country` is `string?` on the server and stays coerced: null and empty both render as no country
/// line, and the address is keyed by id rather than by its text.
extension SavedAddressDto {
    func toDomain() throws -> SavedAddress {
        try SavedAddress(
            id: id.requireNonBlank("id"),
            label: label.requireNonBlank("label"),
            street: street.requireNonBlank("street"),
            city: city.requireNonBlank("city"),
            zipCode: zipCode.requireNonBlank("zipCode"),
            country: country ?? "",
            latitude: latitude,
            longitude: longitude,
            isDefault: isDefault.require("isDefault")
        )
    }
}
