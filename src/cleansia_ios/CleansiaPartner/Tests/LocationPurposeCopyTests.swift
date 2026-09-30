import Foundation
import XCTest
@testable import CleansiaPartner

/// The location prompt names both reasons the app reads the device position: centering the address
/// picker's map, and the distance shown on the job list, which is worked out on the device and never
/// uploaded. Read through the BUILT `InfoPlist.strings` tables, so every shipped language answers for it.
final class LocationPurposeCopyTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private static let key = "NSLocationWhenInUseUsageDescription"

    private static let claims: [String: (distance: String, deviceOnly: String)] = [
        "en": ("how far", "on your device only"),
        "cs": ("vzdálenost", "pouze ve vašem zařízení"),
        "sk": ("vzdialenos", "iba vo vašom zariadení"),
        "uk": ("відстань", "лише на вашому пристрої"),
        "ru": ("расстояние", "только на вашем устройстве")
    ]

    func testThePurposeCoversTheJobListDistanceWorkedOutOnTheDeviceInEveryLocale() throws {
        for (language, claim) in Self.claims {
            let value = try purpose(in: language)
            XCTAssertNotNil(
                value.range(of: claim.distance, options: .caseInsensitive),
                "\(language) does not name the job-list distance: \(value)"
            )
            XCTAssertNotNil(
                value.range(of: claim.deviceOnly, options: .caseInsensitive),
                "\(language) does not say the distance stays on the device: \(value)"
            )
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
