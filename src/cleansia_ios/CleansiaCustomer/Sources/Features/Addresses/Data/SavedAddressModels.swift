import CleansiaCore
import Foundation

struct SavedAddress: Equatable, Identifiable {
    let id: String
    let label: String
    let street: String
    let city: String
    let zipCode: String
    let country: String
    let latitude: Double?
    let longitude: Double?
    let isDefault: Bool

    var oneLine: String {
        [street, city].filter { !$0.isEmpty }.joined(separator: ", ")
    }

    var secondLine: String {
        [zipCode, country].filter { !$0.isEmpty }.joined(separator: " · ")
    }
}

struct SavedAddressDraft {
    var label: String
    let street: String
    let city: String
    let zipCode: String
    let country: String
    let countryIsoCode: String
    let latitude: Double
    let longitude: Double
    var setAsDefault: Bool
}

extension GeocodedAddress {
    func toDraft(label: String, setAsDefault: Bool) -> SavedAddressDraft {
        SavedAddressDraft(
            label: label,
            street: street.isEmpty ? String(formatted.prefix(while: { $0 != "," }))
                .trimmingCharacters(in: .whitespaces) : street,
            city: city,
            zipCode: zipCode,
            country: country,
            countryIsoCode: countryIsoCode,
            latitude: latitude,
            longitude: longitude,
            setAsDefault: setAsDefault
        )
    }
}
