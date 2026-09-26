import Foundation
import XCTest
@testable import CleansiaPartner

/// Cleaner pay has no distance component (owner ruling 2026-09-24), so no partner copy may promise pay for
/// the distance travelled — the address card used to give it as a reason for asking for a home address.
/// Android's `AddressWhyCopyTest` holds the same line.
///
/// Read through the BUILT `.lproj` tables, so a promise under any key and in any shipped language fails.
final class AddressWhyCopyTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    /// Pay phrasing only: a bare "km" is the job radius and the distance to a job.
    private static let travelPayClaim = [
        #"travel pay"#, #"per kilomet"#, #"per km\b"#,
        #"cestovn\S* odm[eě]n"#, #"cestovné"#, #"za kilomet"#,
        #"оплат\S* за дорог"#, #"за кілометр"#, #"за километр"#
    ].joined(separator: "|")

    func testNoPartnerStringPromisesPayForTheDistanceTravelled() throws {
        let claims = try languages.flatMap { language in
            try localizableTable(for: language)
                .filter { Self.promisesTravelPay($0.value) }
                .map { "\(language)/\($0.key): \($0.value)" }
        }
        XCTAssertEqual(claims, [])
    }

    func testTheRemovedReasonWouldHaveBeenCaught() {
        for removed in [
            "To calculate your travel pay", "Pro výpočet cestovní odměny", "Na výpočet cestovnej odmeny",
            "Для розрахунку оплати за дорогу", "Для расчёта оплаты за дорогу"
        ] {
            XCTAssertTrue(Self.promisesTravelPay(removed), "the scan cannot see \(removed)")
        }
    }

    private static func promisesTravelPay(_ text: String) -> Bool {
        text.range(of: travelPayClaim, options: [.regularExpression, .caseInsensitive]) != nil
    }

    func testTheAddressCardKeepsTheReasonsThatAreStillTrue() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in ["address_why_reason_jobs", "address_why_reason_invoice"] {
                XCTAssertFalse(table[key]?.isEmpty ?? true, "\(language)/\(key) is missing")
            }
            XCTAssertNil(table["address_why_reason_distance_pay"], "\(language) still ships the travel-pay reason")
        }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        let strings = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(
            NSDictionary(contentsOf: strings) as? [String: String],
            "Localizable.strings unreadable for \(language)"
        )
    }
}
