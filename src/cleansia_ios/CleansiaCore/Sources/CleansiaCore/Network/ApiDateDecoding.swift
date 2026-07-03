import Foundation

/// The generated clients' ISO-8601 chain only accepts date-times with an
/// explicit offset, so a single offset-less value (.NET `Kind=Unspecified`)
/// would fail an entire response decode. Wrap the chain with a UTC fallback.
public enum ApiDateDecoding {
    public static func decoder(primary: @escaping (String) -> Date?) -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let raw = try container.decode(String.self)
            if let date = primary(raw) ?? offsetlessUtcDate(from: raw) {
                return date
            }
            throw DecodingError.dataCorruptedError(
                in: container,
                debugDescription: "Unparseable date: \(raw)"
            )
        }
        return decoder
    }

    public static func offsetlessUtcDate(from string: String) -> Date? {
        withFraction.date(from: string) ?? withoutFraction.date(from: string)
    }

    private static let withFraction = utcFormatter("yyyy-MM-dd'T'HH:mm:ss.SSS")
    private static let withoutFraction = utcFormatter("yyyy-MM-dd'T'HH:mm:ss")

    private static func utcFormatter(_ format: String) -> DateFormatter {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .iso8601)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = format
        return formatter
    }
}
