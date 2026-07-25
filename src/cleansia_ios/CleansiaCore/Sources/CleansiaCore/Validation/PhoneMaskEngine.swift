import Foundation

public protocol PhoneDisplayFormatting {
    func display(_ wireValue: String) -> String
}

public struct PhoneMaskEdit: Equatable {
    public let text: String
    public let caret: Int
    public let wireValue: String

    public init(text: String, caret: Int, wireValue: String) {
        self.text = text
        self.caret = caret
        self.wireValue = wireValue
    }
}

/// Turns a text-field edit into masked display text, a caret offset and the
/// value we submit. The submitted value is always `PhoneNumberSanitizer`'s
/// output — a leading `+` plus digits — so the mask stays display-only.
public struct PhoneMaskEngine {
    private let formatter: PhoneDisplayFormatting

    public init(formatter: PhoneDisplayFormatting) {
        self.formatter = formatter
    }

    public func reformat(_ value: String) -> PhoneMaskEdit {
        let wireValue = PhoneNumberSanitizer.sanitize(value)
        let text = display(for: wireValue)
        return PhoneMaskEdit(text: text, caret: text.count, wireValue: wireValue)
    }

    public func edit(current: String, range: Range<Int>, replacement: String) -> PhoneMaskEdit {
        let characters = Array(current)
        let upper = min(max(range.upperBound, 0), characters.count)
        var lower = min(max(range.lowerBound, 0), upper)

        // Backspacing a separator has to take the digit in front of it with it:
        // reformatting would put the separator straight back and the key would
        // look dead.
        if replacement.isEmpty, upper == lower + 1, !Self.isSignificant(characters[lower]) {
            var probe = lower
            while probe > 0, !Self.isSignificant(characters[probe - 1]) {
                probe -= 1
            }
            if probe > 0 { lower = probe - 1 }
        }

        let candidate = Array(characters[0 ..< lower]) + Array(replacement) + Array(characters[upper...])
        let caretInCandidate = lower + replacement.count

        var wireValue = ""
        var significantBeforeCaret = 0
        for (index, character) in candidate.enumerated() where PhoneNumberSanitizer.keeps(character, at: index) {
            wireValue.append(character)
            if index < caretInCandidate { significantBeforeCaret += 1 }
        }

        let text = display(for: wireValue)
        return PhoneMaskEdit(
            text: text,
            caret: Self.offset(in: text, afterSignificant: significantBeforeCaret),
            wireValue: wireValue
        )
    }

    /// The visible text is what the next edit is read from, so a formatter that
    /// adds, loses or moves a significant character would silently rewrite what
    /// we submit. Refuse it and show the bare value instead.
    private func display(for wireValue: String) -> String {
        let formatted = formatter.display(wireValue)
        return PhoneNumberSanitizer.sanitize(formatted) == wireValue ? formatted : wireValue
    }

    private static func isSignificant(_ character: Character) -> Bool {
        character.isNumber || character == "+"
    }

    private static func offset(in text: String, afterSignificant count: Int) -> Int {
        guard count > 0 else { return 0 }
        let characters = Array(text)
        guard count < characters.filter(isSignificant).count else { return characters.count }
        var seen = 0
        for (index, character) in characters.enumerated() where isSignificant(character) {
            seen += 1
            if seen == count { return index + 1 }
        }
        return characters.count
    }
}

public struct GroupedPhoneDisplayFormatter: PhoneDisplayFormatting {
    public init() {}

    public func display(_ wireValue: String) -> String {
        guard !wireValue.isEmpty else { return "" }
        let hasPlus = wireValue.hasPrefix("+")
        let digits = Array(wireValue.filter(\.isNumber))
        guard !digits.isEmpty else { return hasPlus ? "+" : "" }
        var groups: [String] = []
        var index = 0
        if hasPlus {
            let countryLength = min(3, digits.count)
            groups.append("+" + String(digits[0 ..< countryLength]))
            index = countryLength
        }
        while index < digits.count {
            let end = min(index + 3, digits.count)
            groups.append(String(digits[index ..< end]))
            index = end
        }
        return groups.joined(separator: " ")
    }
}
