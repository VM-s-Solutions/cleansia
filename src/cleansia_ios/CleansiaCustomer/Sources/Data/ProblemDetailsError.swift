import CleansiaCore
import CleansiaCustomerApi
import Foundation

enum ProblemDetailsError {
    static func map(_ error: Error) -> ApiError {
        guard case let .error(status, data, _, _) = error as? ErrorResponse else {
            return ApiError.fromGenerated(error)
        }
        let body = data.flatMap { try? JSONDecoder().decode(ProblemDetailsBody.self, from: $0) }
        let detail = data.flatMap { String(data: $0, encoding: .utf8) }
        return ApiError(code: body?.type, message: body?.detail ?? detail, httpStatus: status)
    }
}

private struct ProblemDetailsBody: Decodable {
    let type: String?
    let detail: String?
}
