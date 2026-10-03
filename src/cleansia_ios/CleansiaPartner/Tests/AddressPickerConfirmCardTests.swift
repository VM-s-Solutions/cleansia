import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// The picker insets its map by the measured height of the stack over its bottom edge, and a new inset
/// re-centres the map on the picked point. Every drag starts a reverse lookup, so if the address card
/// is one line while "Looking up…" and two once the address lands, the map, the pin and the card jump
/// on every drag. This pins that the card keeps one height in every state, in every language, at the
/// default text size and at an accessibility size.
@MainActor
final class AddressPickerConfirmCardTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]
    private static let textSizes: [DynamicTypeSize] = [.large, .accessibility3]

    /// iPhone SE (3rd gen) — no iOS-16-capable device is narrower, so the hints are at their tightest.
    private static let narrowestSupportedWidth: CGFloat = 375

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testTheCardKeepsOneHeightThroughALookup() throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            for size in Self.textSizes {
                let resolvedHeight = height(resolved: Self.address, lookingUp: false, size: size)
                for (state, (resolved, lookingUp)) in Self.otherStates {
                    XCTAssertEqual(
                        height(resolved: resolved, lookingUp: lookingUp, size: size),
                        resolvedHeight,
                        accuracy: 0.5,
                        "the card's height in \"\(state)\" differs from the resolved address's in \(language) at "
                            + "\(size) — the map is inset by it, so every lookup would move the map"
                    )
                }
            }
        }
    }

    // MARK: - Fixtures

    private static let address = GeocodedAddress(
        latitude: 50.0755,
        longitude: 14.4378,
        street: "Vinohradská 12",
        city: "Praha",
        zipCode: "120 00",
        country: "Czechia",
        countryIsoCode: "cz",
        formatted: "Vinohradská 12, Praha"
    )

    /// A place with no city line (a park, water): the title alone, and still two lines tall.
    private static let streetOnly = GeocodedAddress(
        latitude: 50.0755,
        longitude: 14.4378,
        street: "",
        city: "",
        zipCode: "",
        country: "",
        countryIsoCode: "cz",
        formatted: "Riegrovy sady"
    )

    private static let longAddress = GeocodedAddress(
        latitude: 50.0755,
        longitude: 14.4378,
        street: "Nábřeží Kapitána Jaroše 1000/7, vchod ze dvora",
        city: "Praha 7 – Holešovice",
        zipCode: "170 00",
        country: "Czechia",
        countryIsoCode: "cz",
        formatted: "Nábřeží Kapitána Jaroše 1000/7, Praha 7"
    )

    /// Each state by name: the address the card shows, and whether a lookup is running.
    private static let otherStates: KeyValuePairs<String, (GeocodedAddress?, Bool)> = [
        "drag to pick": (nil, false),
        "first lookup": (nil, true),
        "lookup after an address": (address, true),
        "no city line": (streetOnly, false),
        "long address": (longAddress, false)
    ]

    // MARK: - Rendering

    private func height(resolved: GeocodedAddress?, lookingUp: Bool, size: DynamicTypeSize) -> CGFloat {
        let card = ConfirmCard(resolved: resolved, lookingUp: lookingUp, enabled: !lookingUp, onConfirm: {})
            .environment(\.dynamicTypeSize, size)
        let host = UIHostingController(rootView: card)
        return host.sizeThatFits(
            in: CGSize(width: Self.narrowestSupportedWidth, height: .greatestFiniteMagnitude)
        ).height
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
