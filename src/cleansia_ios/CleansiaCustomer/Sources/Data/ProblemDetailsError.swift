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
        return ApiError(
            code: body?.firstErrorKey ?? body?.type,
            message: body?.detail ?? detail,
            httpStatus: status
        )
    }
}

private struct ProblemDetailsBody: Decodable {
    let type: String?
    let detail: String?
    let errors: [String: FlexibleStrings]?

    var firstErrorKey: String? {
        errors?.values.flatMap(\.values).first { !$0.isEmpty }
    }
}

// The backend emits `errors` values as single strings (business errors) or
// string arrays (ASP.NET ModelState); accept both.
private struct FlexibleStrings: Decodable {
    let values: [String]

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let single = try? container.decode(String.self) {
            values = [single]
        } else {
            values = try container.decode([String].self)
        }
    }
}
