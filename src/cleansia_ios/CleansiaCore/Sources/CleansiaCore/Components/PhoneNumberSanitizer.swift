import Foundation

public enum PhoneNumberSanitizer {
    public static func sanitize(_ input: String) -> String {
        guard !input.isEmpty else { return "" }
        var result = ""
        var seenPlus = false
        for (index, character) in input.enumerated() {
            if character == "+", index == 0, !seenPlus {
                result.append("+")
                seenPlus = true
            } else if character.isNumber {
                result.append(character)
            }
        }
        return result
    }
}
