import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// Help's contact rows were plain text: the support address could not be tapped and the support line
/// was never dialled. They now open the mail app on the one support address and the dialer on the line
/// the customer web footer prints, and when nothing on the device takes the link they copy the value
/// and say so. Android's `MarketCopyStringsTest` holds the same rows. Mail with no account set up still
/// takes a mailto: link, on its setup screen, so with no account the address is copied as the link goes out.
final class HelpSupportContactTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]
    private static let noticeKeys = ["help_email_unavailable", "help_call_unavailable"]

    func testTheRowsOpenTheSupportAddressAndLineAndCopyWhatNothingTakes() throws {
        let help = try String(
            contentsOf: Self.sourceRoot()
                .appendingPathComponent("cleansia_ios/CleansiaCustomer/Sources/Features/Profile/HelpSupportView.swift"),
            encoding: .utf8
        )
        XCTAssertTrue(help.contains("\"mailto:\\(CleansiaWeb.contactEmail)\""), "Email us opens no mail")
        XCTAssertTrue(help.contains("\"tel:\\(Self.supportPhone)\""), "Call support dials nothing")
        XCTAssertTrue(help.contains("openURL(url) { accepted in"), "a link nothing takes goes unnoticed")
        XCTAssertTrue(help.contains("UIPasteboard.general.string = value"), "a link nothing takes is not copied")
        XCTAssertTrue(
            help.contains("copyFirst: !MFMailComposeViewController.canSendMail()"),
            "with no mail account set up, Mail opens on its setup screen and the address is not copied"
        )
        XCTAssertTrue(help.contains("if copyFirst { copy() }"), "the address is not copied before the link goes out")
        XCTAssertTrue(help.contains("notice: L10n.Help.emailUnavailable"))
        XCTAssertTrue(help.contains("notice: L10n.Help.callUnavailable"))
    }

    /// One address and one line on both apps: the ones Android's Help opens.
    func testBothAppsOpenTheSameAddressAndLine() throws {
        let android = try String(
            contentsOf: Self.sourceRoot().appendingPathComponent(
                "cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/profile/HelpSupportScreen.kt"
            ),
            encoding: .utf8
        )
        XCTAssertEqual(CleansiaWeb.contactEmail, "support@cleansia.cz")
        XCTAssertTrue(android.contains("private const val SUPPORT_EMAIL = \"\(CleansiaWeb.contactEmail)\""))
        XCTAssertTrue(android.contains("private const val SUPPORT_PHONE = \"\(HelpSupportView.supportPhone)\""))
    }

    /// Through the BUILT bundle: a key in the catalog but missing from a shipped `.lproj` renders as its name.
    func testTheCopiedNoticesResolveInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        L10n.bundle = try localeBundle("en")
        let english = [L10n.Help.emailUnavailable, L10n.Help.callUnavailable]
        for language in Self.languages {
            L10n.bundle = try localeBundle(language)
            let notices = [L10n.Help.emailUnavailable, L10n.Help.callUnavailable]
            for (index, notice) in notices.enumerated() {
                let key = Self.noticeKeys[index]
                XCTAssertFalse(notice.isEmpty || notice == key, "\(language)/\(key) does not resolve")
                if language != "en" {
                    XCTAssertNotEqual(notice, english[index], "\(language)/\(key) fell through to English")
                }
            }
        }
    }

    private static func sourceRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
