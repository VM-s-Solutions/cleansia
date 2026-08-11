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
        guard value.utf16.count > maxUtf16Length else { return value }
        var capped = ""
        var width = 0
        for character in value {
            let next = width + character.utf16.count
            if next > maxUtf16Length { break }
            capped.append(character)
            width = next
        }
        return capped
    }

    static func trimmedOrNil(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
