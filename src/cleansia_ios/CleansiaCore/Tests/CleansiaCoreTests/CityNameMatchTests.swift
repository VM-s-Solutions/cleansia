import XCTest
@testable import CleansiaCore

/// The table the server's `CityNameMatchTests.cs`, Android's `CityNameMatchTest.kt` and the customer web's
/// `city-name-match.spec.ts` pin: the same cases, in the same order, with the same answers. A port fails by drifting,
/// and a port that drifts STRICTER than the server tells a customer we do not serve a city we do. Change a case here
/// and change it in all three twins.
final class CityNameMatchTests: XCTestCase {
    private enum Question: CustomStringConvertible {
        case serviced(rows: [ServicedCity], city: String?, zipCode: String?)
        case name(row: String?, city: String?)

        var description: String {
            switch self {
            case let .serviced(_, city, zipCode): "serviced \(city ?? "nil") | \(zipCode ?? "nil")"
            case let .name(row, city): "name \(row ?? "nil") | \(city ?? "nil")"
            }
        }

        func ask() -> Bool {
            switch self {
            case let .serviced(rows, city, zipCode): CityNameMatch.isServiced(rows, city: city, zipCode: zipCode)
            case let .name(row, city): CityNameMatch.matches(row, city)
            }
        }
    }

    private struct Case {
        let question: Question
        let answer: Bool
    }

    /// Every Prague spelling carries the ZIP prefix `1`, every other city none.
    private static let fixtureRows: [ServicedCity] = {
        let prague = ["Praha", "Prague"] + (1 ... 22).map { "Praha \($0)" } + (1 ... 22).map { "Prague \($0)" }
        let others = [
            "Brno", "Ostrava", "Plzeň", "Liberec", "Olomouc", "České Budějovice", "Hradec Králové", "Ústí nad Labem",
            "Pardubice"
        ]
        return prague.map { row($0, zipPrefix: "1") } + others.map { row($0, zipPrefix: nil) }
    }()

    private static let table: [Case] = [
        serviced("Praha", "110 00", true),
        serviced("Прага", "110 00", true),
        serviced("Prague", "15000", true),
        serviced("Praha 4 - Chodov", "149 00", true),
        serviced("PRAHA", "110 00", true),
        serviced("Praha 8", "180\u{00A0}00", true),
        serviced("", "120 00", true),
        serviced("Praha", "252 42", false),
        serviced("Praha", "602 00", false),
        serviced("Praha", nil, true),
        serviced("Praha", "1100", true),
        serviced("Praha-západ", "252 00", false),
        serviced("Praha-západ", nil, false),
        serviced("Nová Praha", nil, false),
        serviced("Jesenice", "252 42", false),
        serviced("Brno-střed", "602 00", true),
        serviced("Brno - Královo Pole", "612 00", true),
        serviced("Brno-venkov", "664 00", false),
        serviced("Ostrava-Poruba", "708 00", true),
        serviced("Pardubice II", "530 02", true),
        serviced("Liberec XIV-Ruprechtice", "460 14", true),
        serviced("Plzeň-jih", nil, false),
        serviced("Plzen", "301 00", true),
        serviced("Pilsen", "301 00", false),
        serviced("Ústí nad Labem-Střekov", "400 03", true),
        serviced("Kladno", "272 01", false),
        serviced("Brno", "110 00", true),
        serviced("Praha 8 - Libeň", "180 00", true),
        serviced("Прага", "110\u{00A0}00", true),
        serviced("Praha", "602\u{00A0}00", false),
        serviced("Прага", "1100", false),
        serviced("Praha", "6020", true),
        serviced("Прага", "1abcd", false),
        serviced("Praha", "252\u{00A0}42", false),
        serviced("Praha", "602000", true),
        serviced("Praha", "602 000", true),
        serviced("Praha", "110 000", true),
        serviced("Прага", "110 000", false),
        serviced("Praha", "CZ-110 00", true),
        serviced("Прага", "CZ-110 00", false),
        serviced("Praha", "110-00", true),
        serviced("Прага", "110-00", false),
        serviced("Praha", "", true),
        serviced("Praha", "   ", true),
        serviced("Прага", "", false),
        serviced("Прага", "   ", false),
        serviced("Praha-západ", "", false),
        serviced("Nová Praha", "", false),
        serviced("Plzeň-jih", "", false),
        serviced("Praha", "110 00", false, rows: []),
        serviced("Kladno", "110 00", true, rows: [row("Praha", zipPrefix: " 1 ")]),
        serviced("Praha", "110 00", false, rows: [row("Praha", zipPrefix: "1 1")]),
        serviced("Praha", "252 42", true, rows: [row("Praha", zipPrefix: " ")]),
        serviced("Прага", "110 00", false, rows: [row("Praha", zipPrefix: "")]),
        serviced("Plzen", "301 00", true, rows: [row("Plzeň", zipPrefix: nil)]),
        name("Plzeň", "Plzen", true),
        name("Plzen", "Plzeň", true),
        name("České Budějovice", "Ceske Budejovice", true),
        name("Ústí nad Labem", "Usti nad Labem", true),
        name("Hradec Králové", "Hradec Kralove", true),
        name("Praha", "Praha 8", true),
        name("Praha", "Praha 22", true),
        name("Prague", "Prague 8", true),
        name("Praha", "Praha 4 - Chodov", true),
        name("Praha", "Praha 5 \u{2013} Smíchov", true),
        name("Praha", "Praha 4-Chodov", true),
        name("Praha", "Praha 4\u{2011}Chodov", true),
        name("Praha", "Praha\u{00A0}8", true),
        name("Praha", "Praha-Kunratice", true),
        name("Praha", "Praha - Smíchov", true),
        name("Brno", "Brno-střed", true),
        name("Brno", "Brno-sever", true),
        name("Brno", "Brno - Královo Pole", true),
        name("Ostrava", "Ostrava-Poruba", true),
        name("Ostrava", "Ostrava-Jih", true),
        name("Ostrava", "Ostrava 3 - Ostrava-Jih", true),
        name("Ústí nad Labem", "Ústí nad Labem-Střekov", true),
        name("Pardubice", "Pardubice II", true),
        name("Liberec", "Liberec XIV-Ruprechtice", true),
        name("Plzeň", "Plzeň 2-Slovany", true),
        name("Praha 8", "Praha 8 - Libeň", true),
        name("Praha", "Praha-", false),
        name("Brno", "Brno -", false),
        name("Praha", "  PRAHA  ", true),
        name("Hradec Králové", "Hradec  Kralove", true),
        name("Ústí nad Labem", "Ústí\u{00A0}nad\u{00A0}Labem", true),
        name("Praha", "Praha-západ", false),
        name("Praha", "Praha-východ", false),
        name("Brno", "Brno-venkov", false),
        name("Brno", "Brno - venkov", false),
        name("Plzeň", "Plzeň-sever", false),
        name("Plzeň", "Plzeň-jih", false),
        name("Praha", "Nová Praha", false),
        name("Ústí nad Labem", "Ústí nad Orlicí", false),
        name("Praha", "Kladno", false),
        name("Praha", "Prague 8", false),
        name("Praha", "Prague", false),
        name("Plzeň", "Pilsen", false),
        name("Praha", "Прага", false),
        name("Praha 8", "Praha 22", false),
        name("Praha 8", "Praha", false),
        name("Praha", "8", false),
        name("Praha", "", false),
        name("", "Praha", false),
        name("Praha", " ", false),
        name(nil, "Praha", false),
        name("Praha", nil, false),
        name("Praha", "Praha", true),
        name("Praha", "PRAHA", true),
        name("Praha", "  Praha ", true),
        name("Brno", "Brno", true),
        name("Brno", "BRNO", true),
        name("Brno", "  Brno ", true),
        name("Plzeň", "Plzeň", true),
        name("Plzeň", "PLZEŇ", true),
        name("Plzeň", "  Plzeň ", true),
        name("České Budějovice", "České Budějovice", true),
        name("České Budějovice", "ČESKÉ BUDĚJOVICE", true),
        name("České Budějovice", "  České Budějovice ", true)
    ]

    private static func row(_ name: String, zipPrefix: String?) -> ServicedCity {
        ServicedCity(id: name, countryId: "cz", name: name, zipPrefix: zipPrefix)
    }

    private static func serviced(
        _ city: String?,
        _ zipCode: String?,
        _ answer: Bool,
        rows: [ServicedCity] = fixtureRows
    ) -> Case {
        Case(question: .serviced(rows: rows, city: city, zipCode: zipCode), answer: answer)
    }

    private static func name(_ row: String?, _ city: String?, _ answer: Bool) -> Case {
        Case(question: .name(row: row, city: city), answer: answer)
    }

    func testTheSharedCaseTable() {
        XCTAssertEqual(Self.table.count, 119)
        for (index, item) in Self.table.enumerated() {
            XCTAssertEqual(item.question.ask(), item.answer, "case \(index + 1): \(item.question)")
        }
    }
}
