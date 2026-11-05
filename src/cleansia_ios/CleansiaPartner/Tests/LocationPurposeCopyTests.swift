import Foundation
import XCTest
@testable import CleansiaPartner

/// The location prompt names the one reason the app reads the device position: centring the address picker's
/// map. The job list no longer shows a distance from the device (Orders shows a banded distance from the stored
/// home, worked out on the server), so the prompt must not claim one. Read through the BUILT `InfoPlist.strings`
/// tables, so every shipped language answers for it.
final class LocationPurposeCopyTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private static let key = "NSLocationWhenInUseUsageDescription"

    private static let mapClause: [String: String] = [
        "en": "center the map when you set your address",
        "cs": "vycentrování mapy při zadávání adresy",
        "sk": "vycentrovanie mapy pri zadávaní adresy",
        "uk": "відцентрувати карту під час введення адреси",
        "ru": "центрировать карту при вводе адреса"
    ]

    /// The words the job-list distance clause used, in every language: none may come back.
    private static let distanceClause: [String: [String]] = [
        "en": ["how far", "job list", "on your device only"],
        "cs": ["vzdálenost", "seznamu", "pouze ve vašem zařízení"],
        "sk": ["vzdialenos", "zozname", "iba vo vašom zariadení"],
        "uk": ["відстань", "списку", "лише на вашому пристрої"],
        "ru": ["расстояние", "списке", "только на вашем устройстве"]
    ]

    func testThePurposeNamesTheAddressMapInEveryLocale() throws {
        for (language, clause) in Self.mapClause {
            let value = try purpose(in: language)
            XCTAssertNotNil(
                value.range(of: clause, options: .caseInsensitive),
                "\(language) does not name centring the address map: \(value)"
            )
        }
    }

    func testThePurposeNoLongerClaimsAJobListDistance() throws {
        for (language, words) in Self.distanceClause {
            let value = try purpose(in: language)
            for word in words {
                XCTAssertNil(
                    value.range(of: word, options: .caseInsensitive),
                    "\(language) still claims the job-list distance (\(word)): \(value)"
                )
            }
        }
    }

    /// The Info.plist value is what a language without its own table falls back to.
    func testTheBaseInfoPlistReadsLikeTheEnglishTable() throws {
        let base = try XCTUnwrap(appBundle.infoDictionary?[Self.key] as? String, "no base purpose string")
        XCTAssertEqual(base, try purpose(in: "en"))
    }

    private func purpose(in language: String) throws -> String {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        let table = try XCTUnwrap(
            NSDictionary(contentsOf: lproj.appendingPathComponent("InfoPlist.strings")) as? [String: String],
            "InfoPlist.strings unreadable for \(language)"
        )
        return try XCTUnwrap(table[Self.key], "\(language) has no location purpose string")
    }
}
