import Foundation

/// The wire format for every phone field: a leading `+` plus digits, nothing
/// else. Phone uniqueness is an exact string match server-side, so this is the
/// one place that decides which characters survive.
public enum PhoneNumberSanitizer {
    public static func sanitize(_ input: String) -> String {
        var result = ""
        for character in input where keeps(character, after: result) {
            result.append(character)
        }
        return result
    }

    /// A `+` counts until the first digit, wherever it sits in the text: a copied number often has a
    /// space, a label or an invisible direction mark in front of it.
    static func keeps(_ character: Character, after kept: String) -> Bool {
        character.isNumber || (character == "+" && kept.isEmpty)
    }
}
