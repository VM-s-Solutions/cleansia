import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// Read-only saved-address list for the recurring-create form. The full saved-
/// address CRUD surface is Slice E; the create form only needs to pick from the
/// user's existing saved addresses (`savedAddressGetMine`, own-only by server
/// construction).
protocol RecurringSavedAddressClient: Sendable {
    func getMine() async -> ApiResult<[RecurringSavedAddress]>
}

struct LiveRecurringSavedAddressClient: RecurringSavedAddressClient {
    func getMine() async -> ApiResult<[RecurringSavedAddress]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedAddressAPI.savedAddressGetMine()
        }
        return result.map { $0.compactMap { $0.toDomain() } }
    }
}

private extension SavedAddressDto {
    func toDomain() -> RecurringSavedAddress? {
        guard let id else { return nil }
        return RecurringSavedAddress(
            id: id,
            label: label,
            street: street,
            city: city,
            isDefault: isDefault ?? false
        )
    }
}
