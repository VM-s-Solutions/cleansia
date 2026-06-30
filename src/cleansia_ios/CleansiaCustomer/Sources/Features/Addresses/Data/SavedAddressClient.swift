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
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressGetMine()
        }
        return result.map { $0.compactMap { $0.toDomain() } }
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
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressAdd(addSavedAddressCommand: command)
        }
        return result.flatMap { dto in
            guard let address = dto.toDomain() else { return .failure(ApiError(code: "address.malformed")) }
            return .success(address)
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
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressUpdate(updateSavedAddressCommand: command)
        }
        return result.flatMap { dto in
            guard let address = dto.toDomain() else { return .failure(ApiError(code: "address.malformed")) }
            return .success(address)
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

private extension SavedAddressDto {
    func toDomain() -> SavedAddress? {
        guard let id, let label, let street, let city, let zipCode else { return nil }
        return SavedAddress(
            id: id,
            label: label,
            street: street,
            city: city,
            zipCode: zipCode,
            country: country ?? "",
            latitude: latitude,
            longitude: longitude,
            isDefault: isDefault ?? false
        )
    }
}
