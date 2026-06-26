import Foundation

public enum EmailValidator {
    private static let pattern =
        "[a-zA-Z0-9\\+\\.\\_\\%\\-\\+]{1,256}" +
        "\\@" +
        "[a-zA-Z0-9][a-zA-Z0-9\\-]{0,64}" +
        "(" +
        "\\." +
        "[a-zA-Z0-9][a-zA-Z0-9\\-]{0,25}" +
        ")+"

    private static let regex = try? NSRegularExpression(pattern: pattern)

    public static func isValid(_ email: String) -> Bool {
        guard let regex else { return false }
        let range = NSRange(email.startIndex..., in: email)
        guard let match = regex.firstMatch(in: email, range: range) else { return false }
        return match.range == range
    }
}
