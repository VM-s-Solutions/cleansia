import Foundation

public enum JwtDecoder {
    public static func expiry(of jwt: String) -> Date? {
        guard let exp = payload(of: jwt)?["exp"] else { return nil }
        let seconds: Double? = switch exp {
        case let value as Double: value
        case let value as Int: Double(value)
        case let value as NSNumber: value.doubleValue
        default: nil
        }
        guard let seconds, seconds > 0 else { return nil }
        return Date(timeIntervalSince1970: seconds)
    }

    public static func email(of jwt: String) -> String? {
        claim(
            of: jwt,
            keys: ["email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"]
        )
    }

    public static func userId(of jwt: String) -> String? {
        claim(
            of: jwt,
            keys: [
                "sub",
                "nameid",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
            ]
        )
    }

    private static func claim(of jwt: String, keys: [String]) -> String? {
        guard let payload = payload(of: jwt) else { return nil }
        for key in keys {
            if let value = payload[key] as? String, !value.isEmpty {
                return value
            }
        }
        return nil
    }

    private static func payload(of jwt: String) -> [String: Any]? {
        let parts = jwt.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3, let data = base64URLDecode(String(parts[1])) else { return nil }
        return try? JSONSerialization.jsonObject(with: data) as? [String: Any]
    }

    private static func base64URLDecode(_ value: String) -> Data? {
        var base64 = value
            .replacingOccurrences(of: "-", with: "+")
            .replacingOccurrences(of: "_", with: "/")
        let remainder = base64.count % 4
        if remainder > 0 {
            base64 += String(repeating: "=", count: 4 - remainder)
        }
        return Data(base64Encoded: base64)
    }
}
