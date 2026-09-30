import XCTest
@testable import CleansiaCustomer

/// Orders, receipts, the consent record, the action log and dispute text all outlive the account by design
/// (→ /flows/gdpr-and-audit), so "everything is deleted" is a promise the erasure does not keep. The web's
/// delete page and Android's screen state the same list.
final class AccountDeletionKeptRecordsTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    private static let everythingDeletedClaim = [
        "all associated data", "account and data", "and all data",
        "všechna související data", "účet a data",
        "všetky súvisiace údaje", "účet a údaje",
        #"всі пов.язані дані"#, "обліковий запис і дані",
        "все связанные данные", "аккаунт и данные"
    ].joined(separator: "|")

    private static let renderedKeys: [String] = [
        "delete_account_subtitle",
        "delete_account_dialog_message",
        "delete_account_what_happens",
        "delete_account_item_profile",
        "delete_account_item_addresses",
        "delete_account_item_history",
        "delete_account_item_devices"
    ] + Array(keptKeys.keys)

    private static let keptKeys = [
        "delete_account_what_is_kept": "L10n.DeleteAccount.whatIsKept",
        "delete_account_kept_bookings": "L10n.DeleteAccount.keptBookings",
        "delete_account_kept_receipts": "L10n.DeleteAccount.keptReceipts",
        "delete_account_kept_consents": "L10n.DeleteAccount.keptConsents",
        "delete_account_kept_audit": "L10n.DeleteAccount.keptAudit",
        "delete_account_kept_disputes": "L10n.DeleteAccount.keptDisputes"
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

    func testNoLocaleSaysEveryPieceOfDataIsDeleted() throws {
        try forEachLanguage { language in
            for key in Self.renderedKeys {
                let value = L10n.localized(key)
                XCTAssertFalse(Self.claimsEverything(value), "\(language)/\(key): \(value)")
            }
        }
    }

    func testTheScreenListsWhatIsKeptInEveryLocale() throws {
        try forEachLanguage { language in
            for key in Self.keptKeys.keys {
                let value = L10n.localized(key)
                XCTAssertNotEqual(value, key, "\(key) is unlocalized in \(language)")
                XCTAssertFalse(value.isBlank, "\(key) is empty in \(language)")
            }
        }
        let view = try read("CleansiaCustomer/Sources/Features/Profile/DeleteAccountView.swift")
        let accessors = try read("CleansiaCustomer/Sources/L10n+Profile.swift")
        for (key, accessor) in Self.keptKeys {
            XCTAssertTrue(view.contains(accessor), "the deletion screen does not render \(key)")
            XCTAssertTrue(accessors.contains("\"\(key)\""), "\(accessor) does not read \(key)")
        }
    }

    /// The record of every consent the customer gave is one of the things kept.
    func testTheDeletedListNoLongerClaimsTheConsentsGo() throws {
        try forEachLanguage { language in
            let key = "delete_account_item_consents"
            XCTAssertEqual(L10n.localized(key), key, "\(language) still ships \(key)")
        }
    }

    func testTheScanWouldHaveCaughtTheRemovedCopy() {
        for removed in [
            "This will permanently delete your account and all associated data.",
            "This permanently deletes your account and data.",
            "Це назавжди видалить ваш обліковий запис і всі пов'язані дані.",
            "Это навсегда удалит ваш аккаунт и данные."
        ] {
            XCTAssertTrue(Self.claimsEverything(removed), "the scan cannot see \(removed)")
        }
    }

    private static func claimsEverything(_ text: String) -> Bool {
        text.range(of: everythingDeletedClaim, options: [.regularExpression, .caseInsensitive]) != nil
    }

    private func forEachLanguage(_ body: (String) throws -> Void) throws {
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            try body(language)
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
