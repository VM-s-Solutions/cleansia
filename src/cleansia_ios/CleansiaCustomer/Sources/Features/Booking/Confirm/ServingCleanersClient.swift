import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct ServingCleaner: Equatable, Identifiable {
    let id: String
    let fullName: String
}

protocol ServingCleanersClient {
    func myServingCleaners() async -> ApiResult<[ServingCleaner]>
}

struct LiveServingCleanersClient: ServingCleanersClient {
    func myServingCleaners() async -> ApiResult<[ServingCleaner]> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderMyServingCleaners()
        }
        return result.map { items in
            items.compactMap { item in
                guard let id = item.employeeId, !id.isBlank else { return nil }
                return ServingCleaner(id: id, fullName: item.fullName ?? "")
            }
        }
    }
}
