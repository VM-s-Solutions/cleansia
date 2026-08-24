import XCTest
@testable import CleansiaCore

/// The same table the server's `CityNameMatchTests.cs` and Android's `CityNameMatchTest.kt` pin, case
/// for case.
///
/// **The duplication is the point.** This is a port of a server rule, and a port fails by drifting —
/// specifically by becoming STRICTER than the server, which tells a customer we do not serve a city we
/// do serve. Three suites over one table means a divergence reddens something.
///
/// Change a case here and change it in both twins.
final class CityNameMatchTests: XCTestCase {
    func testASpellingWithoutDiacriticsIsTheSameCity() {
        XCTAssertTrue(CityNameMatch.matches("Plzeň", "Plzen"))
        XCTAssertTrue(CityNameMatch.matches("Plzen", "Plzeň"))
        XCTAssertTrue(CityNameMatch.matches("České Budějovice", "Ceske Budejovice"))
        XCTAssertTrue(CityNameMatch.matches("Ústí nad Labem", "Usti nad Labem"))
        XCTAssertTrue(CityNameMatch.matches("Hradec Králové", "Hradec Kralove"))
    }

    func testADistrictIsServedByItsCity() {
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha 8"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha 22"))
        XCTAssertTrue(CityNameMatch.matches("Prague", "Prague 8"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha 4 - Chodov"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha 5 – Smíchov"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha 4-Chodov"))
    }

    func testCaseAndSpacingAreNotADifference() {
        XCTAssertTrue(CityNameMatch.matches("Praha", "  PRAHA  "))
        XCTAssertTrue(CityNameMatch.matches("Hradec Králové", "Hradec  Kralove"))
    }

    /// An okres is the rural ring AROUND a city, not part of it — same syntax, opposite answer.
    func testTheOkresAroundACityIsNotTheCity() {
        XCTAssertFalse(CityNameMatch.matches("Praha", "Praha-západ"))
        XCTAssertFalse(CityNameMatch.matches("Praha", "Praha-východ"))
        XCTAssertFalse(CityNameMatch.matches("Brno", "Brno-venkov"))
    }

    func testADifferentCityIsRefused() {
        XCTAssertFalse(CityNameMatch.matches("Praha", "Nová Praha"))
        XCTAssertFalse(CityNameMatch.matches("Ústí nad Labem", "Ústí nad Orlicí"))
        XCTAssertFalse(CityNameMatch.matches("Praha", "Kladno"))
        XCTAssertFalse(CityNameMatch.matches("Brno", "Brno-střed"))
    }

    /// Exonyms are DATA — a row, never an algorithm.
    func testAnExonymMatchesNothingWithoutItsOwnRow() {
        XCTAssertFalse(CityNameMatch.matches("Praha", "Prague 8"))
        XCTAssertFalse(CityNameMatch.matches("Praha", "Prague"))
        XCTAssertFalse(CityNameMatch.matches("Plzeň", "Pilsen"))
        XCTAssertFalse(CityNameMatch.matches("Praha", "Прага"))
    }

    /// The district strip runs on the CUSTOMER's string only.
    func testARowNamingOneDistrictDoesNotClaimTheCity() {
        XCTAssertFalse(CityNameMatch.matches("Praha 8", "Praha 22"))
        XCTAssertFalse(CityNameMatch.matches("Praha 8", "Praha"))
    }

    func testNothingMatchesNothing() {
        XCTAssertFalse(CityNameMatch.matches("Praha", "8"))
        XCTAssertFalse(CityNameMatch.matches("Praha", ""))
        XCTAssertFalse(CityNameMatch.matches("", "Praha"))
        XCTAssertFalse(CityNameMatch.matches("Praha", " "))
        XCTAssertFalse(CityNameMatch.matches(nil, "Praha"))
        XCTAssertFalse(CityNameMatch.matches("Praha", nil))
    }

    func testIsServicedAnswersOverTheWholeList() {
        let serviced = ["Praha", "Brno", "Plzeň"]
        XCTAssertTrue(CityNameMatch.isServiced(serviced, "Praha 4 - Chodov"))
        XCTAssertTrue(CityNameMatch.isServiced(serviced, "Plzen"))
        XCTAssertFalse(CityNameMatch.isServiced(serviced, "Kladno"))

        // An empty list is "we know of nowhere", which must not read as "everywhere".
        XCTAssertFalse(CityNameMatch.isServiced([], "Praha"))
    }
}
