import Foundation

public protocol ApiErrorLocalizing {
    func message(for error: ApiError) -> String
}

public struct ApiErrorLocalizer: ApiErrorLocalizing {
    public init() {}

    public func message(for error: ApiError) -> String {
        if let server = error.message?.trimmingCharacters(in: .whitespacesAndNewlines), !server.isEmpty {
            return server
        }
        return message(forStatus: error.httpStatus)
    }

    public func message(forStatus status: Int?) -> String {
        switch status {
        case 401, 403:
            return String(localized: "error.unauthorized", bundle: .module)
        case 404:
            return String(localized: "error.not_found", bundle: .module)
        case .some(400 ... 499):
            return String(localized: "error.request", bundle: .module)
        case .some(500 ... 599):
            return String(localized: "error.server", bundle: .module)
        case nil:
            return String(localized: "error.unreachable", bundle: .module)
        default:
            return String(localized: "error.generic", bundle: .module)
        }
    }
}
