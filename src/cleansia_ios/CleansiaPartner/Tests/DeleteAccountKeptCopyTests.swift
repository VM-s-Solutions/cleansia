import Foundation
import XCTest
@testable import CleansiaPartner

/// No self-billing agreement exists in the app yet (ADR-0041 was never built), so the offboarding screen
/// may not list one among the records kept, only the invoices and pay records that do exist. Android's
/// `DeleteAccountKeptCopyTest` holds the same line.
///
/// Read through the BUILT `.lproj` tables, so a promise in any shipped language fails.
final class DeleteAccountKeptCopyTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private static let agreementClaim = "self-billing|samofaktur|самовистав|самовыстав"

    func testNoDeletionStringPromisesASelfBillingAgreement() throws {
        let claims = try languages.flatMap { language in
            try localizableTable(for: language)
                .filter { $0.key.hasPrefix("delete_account_") }
                .filter { Self.promisesAgreement($0.value) }
                .map { "\(language)/\($0.key): \($0.value)" }
        }
        XCTAssertEqual(claims, [])
    }

    func testTheScanWouldHaveCaughtTheRemovedRow() {
        for removed in [
            "The self-billing agreement behind those invoices", "Dohodu o samofakturaci k těmto fakturám",
            "Dohodu o samofakturácii k týmto faktúram", "Угоду про самовиставлення рахунків до них",
            "Соглашение о самовыставлении счетов к ним"
        ] {
            XCTAssertTrue(Self.promisesAgreement(removed), "the scan cannot see \(removed)")
        }
    }

    private static func promisesAgreement(_ text: String) -> Bool {
        text.range(of: agreementClaim, options: [.regularExpression, .caseInsensitive]) != nil
    }

    func testTheKeptListNamesTheInvoicesAndPayRecordsInEveryLocale() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in ["delete_account_kept_invoices", "delete_account_kept_pay"] {
                XCTAssertFalse(table[key]?.isEmpty ?? true, "\(language)/\(key) is missing")
            }
        }
    }

    func testTheScreenRendersNoAgreementRow() throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let view = try String(
            contentsOf: root.appendingPathComponent("CleansiaPartner/Sources/Features/Profile/DeleteAccountView.swift"),
            encoding: .utf8
        )
        XCTAssertTrue(view.contains("L10n.DeleteAccount.keptInvoices"), "the invoices row is gone")
        XCTAssertTrue(view.contains("L10n.DeleteAccount.keptPay"), "the pay records row is gone")
        XCTAssertFalse(view.contains("keptAgreement"), "the screen still renders an agreement row")
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
