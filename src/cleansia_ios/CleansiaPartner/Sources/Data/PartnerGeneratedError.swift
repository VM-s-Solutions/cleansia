import CleansiaCore
import CleansiaPartnerApi
import Foundation

extension ApiError {
    static func fromGenerated(_ error: Error) -> ApiError {
        guard case let .error(status, data, _, underlying) = error as? ErrorResponse else {
            return ApiError.from(error)
        }
        if ApiError.isCancellation(underlying) {
            return ApiError(code: ApiError.cancelledCode)
        }
        return ApiError.fromProblemDetails(
            httpStatus: status,
            body: data,
            fallbackMessage: underlying.localizedDescription
        )
    }
}
