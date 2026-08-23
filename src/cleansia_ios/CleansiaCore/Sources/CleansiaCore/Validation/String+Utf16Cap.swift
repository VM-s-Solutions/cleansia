import Foundation

public extension String {
    /// Truncate to at most `maxUtf16Length` UTF-16 code units, never splitting a character.
    ///
    /// **Every server-side length cap is UTF-16, and `String.count` is not.** FluentValidation's
    /// `MaximumLength(n)` measures .NET `string.Length` — UTF-16 code units. Swift's `count` measures
    /// grapheme clusters, so a client cap written with `count` / `prefix` is strictly MORE permissive
    /// than the server: an emoji is one to Swift and two to the validator, so 600 of them pass a
    /// 1000-character field and are then refused at submit, after the customer has typed them.
    ///
    /// Truncating on the code-unit boundary directly would be worse than wrong — it can split a
    /// surrogate pair and produce a replacement character — so the walk accumulates whole characters
    /// and stops before the one that would cross the limit.
    func cappedToUtf16(_ maxUtf16Length: Int) -> String {
        guard utf16.count > maxUtf16Length else { return self }

        var capped = ""
        var width = 0
        for character in self {
            let next = width + character.utf16.count
            if next > maxUtf16Length { break }
            capped.append(character)
            width = next
        }
        return capped
    }
}
