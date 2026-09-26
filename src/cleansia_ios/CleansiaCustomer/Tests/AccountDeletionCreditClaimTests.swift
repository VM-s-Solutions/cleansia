import XCTest
@testable import CleansiaCustomer

/// A completed account deletion forfeits every unused credit balance; it is not paid out and cannot be
/// restored. Each locale's own credit word, its own "forfeited" and its own "paid out" — so a translation
/// that drops any of the three fails in the locale that dropped it. `account-deletion-credit-claim.spec.ts`
/// holds the same line for web, with the same words.
final class AccountDeletionCreditClaimTests: XCTestCase {
    private static let claims: [String: (credit: String, forfeited: String, paidOut: String)] = [
        "en": ("credit", "forfeit", "paid out"),
        "cs": ("kredit", "propadá", "vyplatit"),
        "sk": ("kredit", "prepadá", "vyplatiť"),
        "uk": ("бонус", "анулю", "виплатити"),
        "ru": ("бонус", "аннулир", "выплатить")
    ]

    /// The confirmation the customer accepts, and the text above the button that opens it.
    private static let rendered = [
        "delete_account_dialog_message": "L10n.DeleteAccount.dialogMessage",
        "delete_account_subtitle": "L10n.DeleteAccount.subtitle"
    ]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testTheWarningSaysTheCreditIsForfeitedAndCannotBePaidOutInEveryLocale() throws {
        for (language, claim) in Self.claims {
            L10n.bundle = try localeBundle(language)
            for key in Self.rendered.keys {
                let value = L10n.localized(key).lowercased()
                XCTAssertTrue(value.contains(claim.credit), "\(key) never names the credit in \(language)")
                XCTAssertTrue(value.contains(claim.forfeited), "\(key) never says it is forfeited in \(language)")
                XCTAssertTrue(value.contains(claim.paidOut), "\(key) never says it is not paid out in \(language)")
            }
        }
    }

    func testTheDeletionScreenRendersTheKeysThatCarryTheWarning() throws {
        let view = try read("CleansiaCustomer/Sources/Features/Profile/DeleteAccountView.swift")
        let accessors = try read("CleansiaCustomer/Sources/L10n+Profile.swift")
        for (key, accessor) in Self.rendered {
            XCTAssertTrue(view.contains(accessor), "the deletion screen no longer shows \(key)")
            XCTAssertTrue(accessors.contains("\"\(key)\""), "\(accessor) no longer reads \(key)")
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
