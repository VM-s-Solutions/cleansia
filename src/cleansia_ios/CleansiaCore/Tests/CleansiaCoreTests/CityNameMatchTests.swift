import XCTest
@testable import CleansiaCore

/// The same table the server's `CityNameMatchTests.cs`, Android's `CityNameMatchTest.kt` and the customer web's
/// `city-name-match.spec.ts` pin, case for case. A port fails by drifting, and a port that drifts STRICTER than the
/// server tells a customer we do not serve a city we do. Change a case here and change it in all three twins.
final class CityNameMatchTests: XCTestCase {
    private struct Case {
        let city: String?
        let zipCode: String?
        let serviced: Bool

        init(_ city: String?, _ zipCode: String?, _ serviced: Bool) {
            self.city = city
            self.zipCode = zipCode
            self.serviced = serviced
        }
    }

    /// The serviced rows the table is read against: every Prague spelling carries the ZIP prefix `1`, every other
    /// city none.
    private static let fixtureRows: [ServicedCity] = {
        let prague = ["Praha", "Prague"] + (1 ... 22).flatMap { ["Praha \($0)", "Prague \($0)"] }
        let others = [
            "Brno", "Ostrava", "Plzeň", "Liberec", "Olomouc", "České Budějovice", "Hradec Králové", "Ústí nad Labem",
            "Pardubice"
        ]
        return prague.map { row($0, zipPrefix: "1") } + others.map { row($0, zipPrefix: nil) }
    }()

    private static let sharedCases = [
        Case("Praha", "110 00", true),
        Case("Прага", "110 00", true),
        Case("Prague", "15000", true),
        Case("Praha 4 - Chodov", "149 00", true),
        Case("PRAHA", "110 00", true),
        Case("Praha 8", "180\u{00A0}00", true),
        Case("", "120 00", true),
        Case("Praha", "252 42", false),
        Case("Praha", "602 00", false),
        Case("Praha", nil, true),
        Case("Praha", "1100", true),
        Case("Praha-západ", "252 00", false),
        Case("Praha-západ", nil, false),
        Case("Nová Praha", nil, false),
        Case("Jesenice", "252 42", false),
        Case("Brno-střed", "602 00", true),
        Case("Brno - Královo Pole", "612 00", true),
        Case("Brno-venkov", "664 00", false),
        Case("Ostrava-Poruba", "708 00", true),
        Case("Pardubice II", "530 02", true),
        Case("Liberec XIV-Ruprechtice", "460 14", true),
        Case("Plzeň-jih", nil, false),
        Case("Plzen", "301 00", true),
        Case("Pilsen", "301 00", false),
        Case("Ústí nad Labem-Střekov", "400 03", true),
        Case("Kladno", "272 01", false),
        Case("Brno", "110 00", true),
        Case("Praha 8 - Libeň", "180 00", true)
    ]

    private static func row(_ name: String, zipPrefix: String?) -> ServicedCity {
        ServicedCity(id: name, countryId: "cz", name: name, zipPrefix: zipPrefix)
    }

    func testTheSharedCaseTable() {
        XCTAssertEqual(Self.sharedCases.count, 28)
        for item in Self.sharedCases {
            XCTAssertEqual(
                CityNameMatch.isServiced(Self.fixtureRows, city: item.city, zipCode: item.zipCode),
                item.serviced,
                "\(item.city ?? "nil") | \(item.zipCode ?? "no postcode")"
            )
        }
    }

    /// A postcode that is not five digits is no postcode, so the Prague rows fall back to their names.
    func testAPostcodeThatIsNotFiveDigitsLeavesThePragueRowsToTheirNames() {
        for zipCode in ["", "   ", "1100", "110 000", "CZ-110 00", "110-00"] {
            XCTAssertTrue(CityNameMatch.isServiced(Self.fixtureRows, city: "Praha", zipCode: zipCode), zipCode)
            XCTAssertFalse(CityNameMatch.isServiced(Self.fixtureRows, city: "Прага", zipCode: zipCode), zipCode)
        }
    }

    func testABlankZipPrefixIsARowMatchedByName() {
        XCTAssertTrue(CityNameMatch.isServiced([Self.row("Praha", zipPrefix: " ")], city: "Praha", zipCode: "252 42"))
        XCTAssertFalse(CityNameMatch.isServiced([Self.row("Praha", zipPrefix: "")], city: "Прага", zipCode: "110 00"))
    }

    /// An empty list is "we know of nowhere", which must not read as "everywhere".
    func testNoRowsServeNothing() {
        XCTAssertFalse(CityNameMatch.isServiced([], city: "Praha", zipCode: "110 00"))
    }

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
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha\u{00A0}8"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha 4\u{2011}Chodov"))
        XCTAssertTrue(CityNameMatch.matches("Praha 8", "Praha 8 - Libeň"))
    }

    /// A city part written after a dash, or as a Roman numeral, is its city.
    func testACityPartIsServedByItsCity() {
        XCTAssertTrue(CityNameMatch.matches("Brno", "Brno-střed"))
        XCTAssertTrue(CityNameMatch.matches("Brno", "Brno-sever"))
        XCTAssertTrue(CityNameMatch.matches("Brno", "Brno - Královo Pole"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha-Kunratice"))
        XCTAssertTrue(CityNameMatch.matches("Praha", "Praha - Smíchov"))
        XCTAssertTrue(CityNameMatch.matches("Ostrava", "Ostrava-Poruba"))
        XCTAssertTrue(CityNameMatch.matches("Ostrava", "Ostrava-Jih"))
        XCTAssertTrue(CityNameMatch.matches("Ostrava", "Ostrava 3 - Ostrava-Jih"))
        XCTAssertTrue(CityNameMatch.matches("Ústí nad Labem", "Ústí nad Labem-Střekov"))
        XCTAssertTrue(CityNameMatch.matches("Pardubice", "Pardubice II"))
        XCTAssertTrue(CityNameMatch.matches("Liberec", "Liberec XIV-Ruprechtice"))
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
        XCTAssertFalse(CityNameMatch.matches("Brno", "Brno - venkov"))
        XCTAssertFalse(CityNameMatch.matches("Plzeň", "Plzeň-sever"))
        XCTAssertFalse(CityNameMatch.matches("Plzeň", "Plzeň-jih"))
    }

    func testADifferentCityIsRefused() {
        XCTAssertFalse(CityNameMatch.matches("Praha", "Nová Praha"))
        XCTAssertFalse(CityNameMatch.matches("Ústí nad Labem", "Ústí nad Orlicí"))
        XCTAssertFalse(CityNameMatch.matches("Praha", "Kladno"))
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
}
