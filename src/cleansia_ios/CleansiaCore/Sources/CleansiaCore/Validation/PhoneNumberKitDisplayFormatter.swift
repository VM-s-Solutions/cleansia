import Foundation
import PhoneNumberKit

/// Region-aware format-as-you-type, the `CleansiaPhoneInput.kt` counterpart of
/// Android's libphonenumber `AsYouTypeFormatter`.
public struct PhoneNumberKitDisplayFormatter: PhoneDisplayFormatting {
    private let partialFormatter: PartialFormatter

    public init(defaultRegion: String = PhoneNumberKitDisplayFormatter.deviceRegion) {
        partialFormatter = PartialFormatter(
            utility: Self.utility,
            defaultRegion: defaultRegion.uppercased(),
            withPrefix: true
        )
    }

    public func display(_ wireValue: String) -> String {
        guard !wireValue.isEmpty else { return "" }
        return partialFormatter.formatPartial(wireValue)
    }

    /// Android falls back to `US` when the device reports no country
    /// (`CleansiaPhoneInput.kt`), and a leading `+` overrides it anyway.
    public static let deviceRegion: String = {
        let region = Locale.current.region?.identifier ?? ""
        return region.isEmpty ? "US" : region.uppercased()
    }()

    /// The server is the authority on whether the number is valid. Its numbering data and this library's come from
    /// different releases, so the device checks only what does not drift between them: the `+` form and a length the
    /// number's country allows.
    public static func isPossibleNumber(_ wireValue: String) -> Bool {
        guard wireValue.hasPrefix("+"),
              let number = try? utility.parse(wireValue, ignorePrefixValidation: true) else { return false }
        // A country code no country has is read as a number of the handset's region, under that region's code.
        let digits = wireValue.compactMap(\.wholeNumberValue).map { String($0) }.joined()
        return digits.hasPrefix(String(number.countryCode))
    }

    /// Parsing the bundled metadata is expensive; one instance serves every field.
    private static let utility = PhoneNumberUtility()
}

public extension PhoneMaskEngine {
    static let phoneNumberKit = PhoneMaskEngine(formatter: PhoneNumberKitDisplayFormatter())
}
