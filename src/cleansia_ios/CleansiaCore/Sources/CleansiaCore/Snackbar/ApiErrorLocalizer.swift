import Foundation

public protocol ApiErrorLocalizing {
    func message(for error: ApiError) -> String
}

public struct ApiErrorLocalizer: ApiErrorLocalizing {
    public init() {}

    // Mirrors Android's ApiErrorParser: catalog lookup by error key → the raw
    // key text → the server message → a status-based generic. Transport-level
    // "network.*" codes are never shown raw.
    public func message(for error: ApiError) -> String {
        if let key = error.code, key.contains(".") {
            if let localized = catalogString("error." + key) {
                return localized
            }
            if !key.hasPrefix("network.") {
                return key
            }
        }
        if let server = error.message?.trimmingCharacters(in: .whitespacesAndNewlines), !server.isEmpty {
            return server
        }
        return message(forStatus: error.httpStatus)
    }

    // String(localized:) cannot signal a miss, so probe with a sentinel default.
    private func catalogString(_ key: String) -> String? {
        let sentinel = "\u{1}"
        let value = Bundle.module.localizedString(forKey: key, value: sentinel, table: nil)
        return value == sentinel ? nil : value
    }

    public func message(forStatus status: Int?) -> String {
        switch status {
        case 401, 403:
            String(localized: "error.unauthorized", bundle: .module)
        case 404:
            String(localized: "error.not_found", bundle: .module)
        case .some(400 ... 499):
            String(localized: "error.request", bundle: .module)
        case .some(500 ... 599):
            String(localized: "error.server", bundle: .module)
        case nil:
            String(localized: "error.unreachable", bundle: .module)
        default:
            String(localized: "error.generic", bundle: .module)
        }
    }
}
