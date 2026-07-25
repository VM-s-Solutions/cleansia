import Foundation

/// The wire format for every phone field: a leading `+` plus digits, nothing
/// else. Phone uniqueness is an exact string match server-side, so this is the
/// one place that decides which characters survive.
public enum PhoneNumberSanitizer {
    public static func sanitize(_ input: String) -> String {
        guard !input.isEmpty else { return "" }
        var result = ""
        for (index, character) in input.enumerated() where keeps(character, at: index) {
            result.append(character)
        }
        return result
    }

    static func keeps(_ character: Character, at index: Int) -> Bool {
        character.isNumber || (character == "+" && index == 0)
    }
}
