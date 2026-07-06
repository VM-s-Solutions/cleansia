import XCTest
@testable import CleansiaCore

final class IsoCountryCodesTests: XCTestCase {
    /// Every alpha-3 code seeded into the backend Countries table
    /// (sql-scripts/insert_seed_data.sql). The generated constant in
    /// `IsoCountryCodes` must keep covering all of them — this pin is what
    /// stops the table drifting away from what the backend can actually send.
    private static let seedAlpha3ToAlpha2: [String: String] = [
        "ARG": "ar", "AUS": "au", "AUT": "at", "BEL": "be", "BGR": "bg",
        "BRA": "br", "CAN": "ca", "CHE": "ch", "CHN": "cn", "CZE": "cz",
        "DEU": "de", "DNK": "dk", "EGY": "eg", "ESP": "es", "EST": "ee",
        "FIN": "fi", "FRA": "fr", "GBR": "gb", "GRC": "gr", "HRV": "hr",
        "HUN": "hu", "IND": "in", "IRL": "ie", "ITA": "it", "JPN": "jp",
        "KOR": "kr", "LTU": "lt", "LVA": "lv", "MEX": "mx", "NLD": "nl",
        "NOR": "no", "NZL": "nz", "POL": "pl", "PRT": "pt", "ROU": "ro",
        "RUS": "ru", "SVK": "sk", "SVN": "si", "SWE": "se", "USA": "us",
        "ZAF": "za"
    ]

    func testMapsEveryBackendSeedAlpha3Code() {
        for (alpha3, alpha2) in Self.seedAlpha3ToAlpha2 {
            XCTAssertEqual(
                IsoCountryCodes.toAlpha2(alpha3),
                alpha2,
                "seeded country \(alpha3) must normalise to \(alpha2)"
            )
        }
    }

    func testAlpha2InputPassesThroughLowercased() {
        XCTAssertEqual(IsoCountryCodes.toAlpha2("cz"), "cz")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("CZ"), "cz")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("sk"), "sk")
    }

    func testNonPrefixPairsResolveWithoutHeuristics() {
        XCTAssertEqual(IsoCountryCodes.toAlpha2("SVK"), "sk")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("POL"), "pl")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("SVN"), "si")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("IRL"), "ie")
    }

    func testTrimsAndLowercasesInput() {
        XCTAssertEqual(IsoCountryCodes.toAlpha2("  CZE  "), "cz")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("cze"), "cz")
    }

    func testUnknownCodePassesThroughLowercased() {
        XCTAssertEqual(IsoCountryCodes.toAlpha2("XXX"), "xxx")
    }

    func testNilAndEmptyBecomeEmpty() {
        XCTAssertEqual(IsoCountryCodes.toAlpha2(nil), "")
        XCTAssertEqual(IsoCountryCodes.toAlpha2(""), "")
        XCTAssertEqual(IsoCountryCodes.toAlpha2("   "), "")
    }
}
