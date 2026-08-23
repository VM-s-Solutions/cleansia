import CleansiaCore
import Foundation

/// The two free-text notes on a booking — special requests and how to get in.
/// The cap stops an over-long note at the keystroke instead of at submit.
enum BookingInstructions {
    /// `CreateOrder` validates both notes with `MaximumLength(2000)`, which
    /// measures .NET `string.Length` — UTF-16 code units, not characters. A
    /// `String.count` cap would be more permissive than the server (2000 emoji
    /// count as 2000 to Swift and 4000 to the validator).
    static let maxUtf16Length = 2000

    static func capped(_ value: String) -> String {
        value.cappedToUtf16(maxUtf16Length)
    }

    static func trimmedOrNil(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
