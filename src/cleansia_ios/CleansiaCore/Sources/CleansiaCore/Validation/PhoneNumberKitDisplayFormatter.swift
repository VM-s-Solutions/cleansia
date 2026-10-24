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

    /// The international form and a length the number's country allows, all the device checks: whether the
    /// range is in use is the server's call, on newer metadata than this library's, so a newly allocated one
    /// is not refused here with no way past it.
    public static func isPossibleNumber(_ wireValue: String) -> Bool {
        wireValue.hasPrefix("+") && utility.isValidPhoneNumber(wireValue, ignorePrefixValidation: true)
    }

    /// Parsing the bundled metadata is expensive; one instance serves every field.
    private static let utility = PhoneNumberUtility()
}

public extension PhoneMaskEngine {
    static let phoneNumberKit = PhoneMaskEngine(formatter: PhoneNumberKitDisplayFormatter())
}
