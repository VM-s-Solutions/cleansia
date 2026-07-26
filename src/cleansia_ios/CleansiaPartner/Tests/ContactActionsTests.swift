import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaPartner

/// The customer card's Call / SMS / Navigate chips were inert `Label`s until the
/// chips were wrapped in `Link`. These tests pin the URL builders behind them.
///
/// The load-bearing case is `testMapsURLEscapesQuerySeparatorsInTheAddress`:
/// `CharacterSet.urlQueryAllowed` deliberately leaves `&`, `=`, `+`, `?` and `#`
/// unescaped, so encoding an address with the bare set silently truncates or
/// corrupts the maps query. Anyone who "simplifies" the allowed set back to
/// `.urlQueryAllowed` fails that test.
final class ContactActionsTests: XCTestCase {
    // MARK: - Call / SMS

    func testCallURLStripsVisualFormattingAndUsesTheTelScheme() {
        XCTAssertEqual(
            ContactActions.callURL(phone: "+420 777 123 456"),
            URL(string: "tel:+420777123456")
        )
    }

    func testSmsURLUsesTheSmsSchemeNotAndroidsSmsto() {
        XCTAssertEqual(
            ContactActions.smsURL(phone: "+420 777 123 456"),
            URL(string: "sms:+420777123456")
        )
    }

    func testDialableCharactersSurviveAndEverythingElseIsDropped() {
        // RFC 3966 visual separators stay; letters and stray punctuation go, so
        // the result is always a parseable `tel:` URL.
        XCTAssertEqual(
            ContactActions.callURL(phone: "(420) 777-123.456 ext"),
            URL(string: "tel:(420)777-123.456")
        )
    }

    func testCallAndSmsAreNilWhenThereIsNoDiallableDigit() {
        for phone in [nil, "", "   ", "n/a"] {
            XCTAssertNil(ContactActions.callURL(phone: phone), "call for \(phone ?? "nil")")
            XCTAssertNil(ContactActions.smsURL(phone: phone), "sms for \(phone ?? "nil")")
        }
    }

    // MARK: - Navigate

    func testMapsURLPrefersCoordinatesAndLabelsThemWithTheAddress() throws {
        let url = ContactActions.mapsURL(
            coordinate: Coordinate(latitude: 50.0755, longitude: 14.4378),
            address: "Karlova 1, Praha"
        )

        XCTAssertEqual(try XCTUnwrap(url).host, "maps.apple.com")
        XCTAssertEqual(queryValue(url, "ll"), "50.0755,14.4378")
        XCTAssertEqual(queryValue(url, "q"), "Karlova 1, Praha")
    }

    func testMapsURLFallsBackToAFreeTextQueryWithoutCoordinates() {
        let url = ContactActions.mapsURL(coordinate: nil, address: "Karlova 1, Praha")

        XCTAssertNil(queryValue(url, "ll"))
        XCTAssertEqual(queryValue(url, "q"), "Karlova 1, Praha")
    }

    func testMapsURLEscapesQuerySeparatorsInTheAddress() throws {
        let address = "King & Queen St 1=2 #3, Praha+Vinohrady?"
        let url = ContactActions.mapsURL(coordinate: nil, address: address)

        // Exactly one parameter survives — an unescaped `&` would split it into
        // several, and an unescaped `#` would amputate everything after it into
        // the fragment.
        let components = try URLComponents(url: XCTUnwrap(url), resolvingAgainstBaseURL: false)
        XCTAssertEqual(components?.queryItems?.count, 1)
        XCTAssertNil(components?.fragment)
        XCTAssertEqual(queryValue(url, "q"), address)
    }

    func testMapsURLIsNilWithoutCoordinatesOrAnAddress() {
        XCTAssertNil(ContactActions.mapsURL(coordinate: nil, address: nil))
        XCTAssertNil(ContactActions.mapsURL(coordinate: nil, address: "   "))
    }

    // MARK: - Helpers

    private func queryValue(_ url: URL?, _ name: String) -> String? {
        guard let url, let components = URLComponents(url: url, resolvingAgainstBaseURL: false) else {
            return nil
        }
        return components.queryItems?.first { $0.name == name }?.value
    }
}
