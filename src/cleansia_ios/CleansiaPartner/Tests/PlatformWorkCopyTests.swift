import Foundation
import XCTest

/// What a cleaner is told about the controls over their work: why they were taken off a job, why their
/// week is capped, where the rules of the board are written down, and the contract documents they accept.
final class PlatformWorkCopyTests: XCTestCase {
    private static let locales = ["en", "cs", "sk", "uk", "ru"]

    /// Each key with the format slots its sentence must carry, in every locale.
    private static let slots: [String: [String]] = [
        "order_removal_title": [],
        "order_removal_message": ["%1$@"],
        "profile_weekly_limit": ["%1$d"],
        "profile_weekly_limit_reason": ["%1$@"],
        "profile_how_jobs_are_offered": [],
        "profile_how_jobs_are_offered_summary": [],
        "profile_legal_documents_summary": [],
        "registration_lock_category_legal_documents": [],
        "legal_documents_title": [],
        "legal_documents_intro": [],
        "legal_documents_empty": [],
        "legal_documents_version": ["%1$@"],
        "legal_documents_accepted_on": ["%1$@", "%2$@"],
        "legal_documents_new_version": ["%1$@"],
        "legal_documents_awaiting": [],
        "legal_documents_accept": [],
        "legal_documents_accepted_toast": [],
        "legal_documents_text_updated": [],
        "legal_documents_review": []
    ]

    private func catalog() throws -> [String: Any] {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Resources/Localizable.xcstrings")
        let root = try JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: Any]
        return try XCTUnwrap(root?["strings"] as? [String: Any], "the partner catalog carries no strings table")
    }

    private func value(of key: String, in strings: [String: Any], locale: String) -> String? {
        let entry = strings[key] as? [String: Any]
        let localizations = entry?["localizations"] as? [String: Any]
        let unit = (localizations?[locale] as? [String: Any])?["stringUnit"] as? [String: Any]
        return unit?["value"] as? String
    }

    private func formatSlots(_ value: String) throws -> [String] {
        let pattern = try NSRegularExpression(pattern: "%(\\d+\\$)?[@a-zA-Z]")
        return pattern.matches(in: value, range: NSRange(value.startIndex..., in: value)).compactMap {
            Range($0.range, in: value).map { String(value[$0]) }
        }
    }

    func testEveryLocaleCarriesTheCopyWithTheSlotsItsSentenceNeeds() throws {
        let strings = try catalog()
        for (key, expected) in Self.slots {
            for locale in Self.locales {
                let resolved = try XCTUnwrap(value(of: key, in: strings, locale: locale), "\(key) [\(locale)] missing")
                XCTAssertFalse(
                    resolved.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                    "\(key) [\(locale)] is blank"
                )
                XCTAssertEqual(try formatSlots(resolved), expected, "\(key) [\(locale)]")
            }
        }
    }

    func testNoTranslationIsTheEnglishSentenceCopiedOver() throws {
        let strings = try catalog()
        for key in Self.slots.keys {
            let english = value(of: key, in: strings, locale: "en")
            for locale in Self.locales where locale != "en" {
                XCTAssertNotEqual(value(of: key, in: strings, locale: locale), english, "\(key) [\(locale)] is English")
            }
        }
    }
}
