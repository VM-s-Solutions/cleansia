import Foundation
import XCTest
@testable import CleansiaCustomer

/// The assignment step of the post-booking timeline. It stated an absolute time
/// ("Within 1 hour") that no dispatch mechanism has ever been able to keep — an
/// order can reach its cleaning time unclaimed. The step may describe what the
/// platform does; it may not state when the platform will be finished.
///
/// Read off the COMPILED string tables in the built app bundle rather than the
/// `.xcstrings` source, so a value that never reaches a device fails here too.
final class BookingSuccessTimelineStringsTests: XCTestCase {
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let assignmentStepKeys = [
        "booking_success_t2_title",
        "booking_success_t2_desc"
    ]

    /// Stems of a duration or calendar unit in each of the five shipped locales,
    /// matched against whole words. A deadline needs one of these or a digit, so
    /// the pair covers "1 hour", "60 minutes" and "do jedné hodiny" alike.
    private let timeUnitStems = [
        "hour", "minute", "day", "week",
        "hodin", "hodín", "minut", "minút", "den", "dní", "dnech", "týden", "týžd",
        "годин", "хвилин", "день", "дні", "тижд",
        "час", "минут", "дня", "дней", "недел"
    ]

    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testTheAssignmentStepIsTranslatedInAllFiveLocales() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in assignmentStepKeys {
                let value = try XCTUnwrap(table[key], "\(language).lproj is missing \(key)")
                XCTAssertFalse(
                    value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                    "\(language).lproj has a blank \(key)"
                )
            }
        }
    }

    func testTheFourTranslationsAreNotTheEnglishStringCopiedOver() throws {
        let english = try localizableTable(for: "en")
        for language in languages.dropFirst() {
            let table = try localizableTable(for: language)
            for key in assignmentStepKeys {
                XCTAssertNotEqual(table[key], english[key], "\(language).lproj left \(key) in English")
            }
        }
    }

    func testNoLocaleOfTheAssignmentStepStatesAnAbsoluteTime() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in assignmentStepKeys {
                let value = try XCTUnwrap(table[key], "\(key) in \(language).lproj")

                XCTAssertFalse(
                    value.contains(where: \.isNumber),
                    "\(language).lproj quotes a number in \(key): \(value)"
                )

                for word in words(value) {
                    XCTAssertNil(
                        timeUnitStems.first(where: word.hasPrefix),
                        "\(language).lproj names a time unit (\(word)) in \(key): \(value)"
                    )
                }
            }
        }
    }

    func testTheTimelineStillRendersTheAssignmentStep() throws {
        let table = try localizableTable(for: "en")
        L10n.bundle = try localeBundle("en")

        let steps = BookingSuccessTimeline.entries(status: nil, cleanerAssigned: false).map(\.step)
        XCTAssertTrue(steps.contains(.assigning), "the post-booking timeline no longer renders the assignment step")
        XCTAssertEqual(BookingSuccessTimelineStep.assigning.title, table["booking_success_t2_title"])
        XCTAssertEqual(BookingSuccessTimelineStep.assigning.subtitle, table["booking_success_t2_desc"])
    }

    private func words(_ value: String) -> [String] {
        value.lowercased()
            .components(separatedBy: CharacterSet.letters.inverted)
            .filter { !$0.isEmpty }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let strings = try localeBundle(language).bundleURL.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(
            NSDictionary(contentsOf: strings) as? [String: String],
            "Localizable.strings unreadable for \(language)"
        )
    }

    private func localeBundle(_ language: String) throws -> Bundle {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        return try XCTUnwrap(Bundle(url: lproj), "\(language).lproj at \(lproj.path) is not a bundle")
    }
}
