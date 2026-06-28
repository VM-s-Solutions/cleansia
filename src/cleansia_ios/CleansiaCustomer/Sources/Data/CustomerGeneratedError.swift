import CleansiaCore
import CleansiaCustomerApi
import Foundation

extension ApiError {
    static func fromGenerated(_ error: Error) -> ApiError {
        guard case let .error(status, data, _, underlying) = error as? ErrorResponse else {
            return ApiError.from(error)
        }
        let detail = data.flatMap { String(data: $0, encoding: .utf8) }
        return ApiError(code: nil, message: detail ?? underlying.localizedDescription, httpStatus: status)
    }
}
