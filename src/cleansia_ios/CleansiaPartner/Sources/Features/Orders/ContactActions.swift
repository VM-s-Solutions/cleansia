import CleansiaCore
import Foundation

enum ContactActions {
    /// RFC 3966 lets a `tel:` number carry the visual separators `-.()` next to
    /// digits and a leading `+`. Anything else (spaces, letters, an "ext."
    /// suffix a dispatcher typed into the phone field) is dropped rather than
    /// percent-escaped, because a dialler cannot do anything with it and an
    /// unescaped space makes `URL(string:)` return nil outright.
    private static let dialableCharacters = CharacterSet(charactersIn: "0123456789+*#-().")

    /// `CharacterSet.urlQueryAllowed` leaves `&`, `=`, `+`, `?` and `#`
    /// unescaped — fine for a whole query string, wrong for a single value. An
    /// address containing `&` would split into two parameters, one containing
    /// `#` would push the rest into the fragment, and `+` is read as a space by
    /// most query parsers. Subtracting them keeps the address intact.
    private static let addressQueryAllowed = CharacterSet.urlQueryAllowed
        .subtracting(CharacterSet(charactersIn: "&=+?#"))

    static func callURL(phone: String?) -> URL? {
        dialable(phone).flatMap { URL(string: "tel:\($0)") }
    }

    /// `sms:` — the iOS scheme. Android's `CustomerCard.kt` uses `smsto:`,
    /// which is an Android-only `ACTION_SENDTO` convention and does nothing here.
    static func smsURL(phone: String?) -> URL? {
        dialable(phone).flatMap { URL(string: "sms:\($0)") }
    }

    /// Apple Maps via its universal link, mirroring the Android `geo:` intent:
    /// prefer the order's own coordinates so Maps does not re-geocode (and does
    /// not mis-resolve an address with a typo), and label the pin with the
    /// human-readable address. Orders that pre-date the geocoding backfill have
    /// no coordinates and fall back to a free-text query.
    static func mapsURL(coordinate: Coordinate?, address: String?) -> URL? {
        let label = address?.trimmingCharacters(in: .whitespacesAndNewlines)
        let encoded = (label?.isEmpty == false)
            ? label?.addingPercentEncoding(withAllowedCharacters: addressQueryAllowed)
            : nil

        if let coordinate {
            // Swift's `Double` description is locale-independent, so the decimal
            // point never turns into a comma and splits the `ll` pair.
            var query = "ll=\(coordinate.latitude),\(coordinate.longitude)"
            if let encoded { query += "&q=\(encoded)" }
            return URL(string: "https://maps.apple.com/?\(query)")
        }

        guard let encoded else { return nil }
        return URL(string: "https://maps.apple.com/?q=\(encoded)")
    }

    static func googleMapsURL(coordinate: Coordinate?, address: String?) -> URL? {
        let destination = coordinate.map { "\($0.latitude),\($0.longitude)" }
            ?? address?.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let destination, !destination.isEmpty else { return nil }
        var url = URLComponents(string: "https://www.google.com/maps/dir/")
        url?.queryItems = [URLQueryItem(name: "api", value: "1"), URLQueryItem(name: "destination", value: destination)]
        return url?.url
    }

    private static func dialable(_ phone: String?) -> String? {
        guard let phone else { return nil }
        let cleaned = String(String.UnicodeScalarView(phone.unicodeScalars.filter { dialableCharacters.contains($0) }))
        // A number with no digit at all (an empty field, "n/a") is not dialable;
        // returning nil hides the chip instead of offering a dead tap target.
        return cleaned.contains(where: \.isNumber) ? cleaned : nil
    }
}
