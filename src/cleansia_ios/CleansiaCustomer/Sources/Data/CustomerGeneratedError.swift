import CleansiaCore
import CleansiaCustomerApi
import Foundation

extension ApiError {
    static func fromGenerated(_ error: Error) -> ApiError {
        guard case let .error(status, data, _, underlying) = error as? ErrorResponse else {
            return ApiError.from(error)
        }
        return ApiError.fromProblemDetails(
            httpStatus: status,
            body: data,
            fallbackMessage: underlying.localizedDescription
        )
    }
}
