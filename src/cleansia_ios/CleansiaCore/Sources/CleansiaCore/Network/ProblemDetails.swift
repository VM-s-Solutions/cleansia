import Foundation

struct ProblemDetails: Decodable {
    let title: String?
    let detail: String?
    /// The API base controller writes the business error code into `type`
    /// (`CreateProblemDetails`: `Type = error.Code`).
    let type: String?
    let errorCode: String?
    let errors: [String: [String]]?

    enum CodingKeys: String, CodingKey {
        case title
        case detail
        case type
        case errorCode
        case code
        case errors
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        title = try container.decodeIfPresent(String.self, forKey: .title)
        detail = try container.decodeIfPresent(String.self, forKey: .detail)
        type = try container.decodeIfPresent(String.self, forKey: .type)
        errorCode = try container.decodeIfPresent(String.self, forKey: .errorCode)
            ?? container.decodeIfPresent(String.self, forKey: .code)
        // A malformed `errors` shape must not discard title/detail.
        let decoded = try? container.decodeIfPresent([String: StringOrStringArray].self, forKey: .errors)
        errors = decoded.flatMap { $0 }?.mapValues(\.values)
    }

    /// JSON object keys are unordered, so "first" is whichever entry the decoder
    /// yields; screens surface one error at a time, matching Android.
    var firstErrorKey: String? {
        errors?.values.flatMap { $0 }.first { !$0.isEmpty }
    }
}

/// The backend emits `errors` values as single strings (business errors) or
/// string arrays (ASP.NET ModelState); accept both.
struct StringOrStringArray: Decodable {
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
