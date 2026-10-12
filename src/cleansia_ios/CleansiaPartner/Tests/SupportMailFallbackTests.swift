import Foundation
import XCTest
@testable import CleansiaPartner

/// On a phone where no app takes a `mailto:` link, the dashboard's Help shortcut and the registration lock's
/// support row did nothing. Both now copy the support address and say so, as the customer Help does, and copy
/// it up front where Mail has no account to send from.
final class SupportMailFallbackTests: XCTestCase {
    private static let languages = ["en", "cs", "sk", "uk", "ru"]

    func testTheHelpShortcutCopiesTheAddressWhenNoAppTakesTheLink() throws {
        try assertCopiesTheAddressOnRefusal(in: "Sources/Features/Shell/PartnerShellView.swift")
    }

    func testTheRegistrationLocksSupportRowCopiesTheAddressWhenNoAppTakesTheLink() throws {
        try assertCopiesTheAddressOnRefusal(in: "Sources/Features/RegistrationLock/RegistrationLockView.swift")
    }

    func testTheNoticeIsInEveryLanguage() throws {
        let url = partnerRoot().appendingPathComponent("Resources/Localizable.xcstrings")
        let catalog = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any])
        let strings = try XCTUnwrap(catalog["strings"] as? [String: Any])
        let entry = try XCTUnwrap(strings["help_email_unavailable"] as? [String: Any], "no help_email_unavailable")
        let localizations = try XCTUnwrap(entry["localizations"] as? [String: Any])
        for language in Self.languages {
            let unit = (localizations[language] as? [String: Any])?["stringUnit"] as? [String: Any]
            let value = try XCTUnwrap(unit?["value"] as? String, "help_email_unavailable has no \(language)")
            XCTAssertFalse(value.isEmpty, language)
        }
    }

    private func assertCopiesTheAddressOnRefusal(in path: String, line: UInt = #line) throws {
        let source = try String(contentsOf: partnerRoot().appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()

        XCTAssertTrue(source.contains("openURL(url){acceptedin"), "\(path): the refusal is never heard", line: line)
        XCTAssertTrue(
            source.contains("UIPasteboard.general.string=CleansiaWeb.contactEmail"),
            "\(path): the support address is never copied",
            line: line
        )
        XCTAssertTrue(source.contains("showInfo(L10n.Help.emailUnavailable)"), "\(path): nothing says so", line: line)
        XCTAssertTrue(
            source.contains("MFMailComposeViewController.canSendMail()"),
            "\(path): Mail with no account takes the link and sends nothing",
            line: line
        )
    }

    private func partnerRoot() -> URL {
        URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
    }
}
