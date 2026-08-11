import XCTest
@testable import CleansiaCore

/// The Live Activity is rendered by a widget extension we cannot exercise from here, so this pins the part
/// that IS testable: every accessor the widget reads resolves out of the Core catalog, in all five app
/// languages, off the English source.
final class LiveActivityL10nTests: XCTestCase {
    private static let regions = ["en", "cs", "sk", "uk", "ru"]

    private static let accessors: [(name: String, read: () -> String)] = [
        ("step.confirmed", { LiveActivityL10n.Step.confirmed }),
        ("step.on_the_way", { LiveActivityL10n.Step.onTheWay }),
        ("step.cleaning", { LiveActivityL10n.Step.cleaning }),
        ("step.done", { LiveActivityL10n.Step.done }),
        ("step_of", { LiveActivityL10n.stepOf(3, 4) }),
        ("status.on_the_way.detail", { LiveActivityL10n.Status.onTheWayDetail }),
        ("status.in_progress.detail", { LiveActivityL10n.Status.inProgressDetail }),
        ("status.completed.title", { LiveActivityL10n.Status.completedTitle }),
        ("status.completed.detail", { LiveActivityL10n.Status.completedDetail }),
        ("status.cancelled.title", { LiveActivityL10n.Status.cancelledTitle }),
        ("status.cancelled.detail", { LiveActivityL10n.Status.cancelledDetail }),
        ("status.generic.title", { LiveActivityL10n.Status.genericTitle }),
        ("finish", { LiveActivityL10n.finish }),
        ("arrival", { LiveActivityL10n.arrival }),
        ("booking_fallback", { LiveActivityL10n.bookingFallback }),
        ("order_number", { LiveActivityL10n.orderNumber("AB12CD34") })
    ]

    override func tearDown() {
        CoreL10n.bundle = .module
        super.tearDown()
    }

    func testEveryAccessorResolvesInAllFiveLanguagesAndTranslatesOffEnglish() {
        CoreL10n.apply(languageTag: "en")
        let english = Self.accessors.map { $0.read() }

        for region in Self.regions {
            CoreL10n.apply(languageTag: region)
            for (index, accessor) in Self.accessors.enumerated() {
                let value = accessor.read()
                XCTAssertFalse(value.isEmpty, "\(accessor.name) is empty in \(region)")
                XCTAssertFalse(
                    value.hasPrefix("live_activity."),
                    "\(accessor.name) fell through to its key in \(region) — the catalog entry is missing"
                )
                if region != "en" {
                    XCTAssertNotEqual(
                        value, english[index],
                        "\(accessor.name) in \(region) equals the en baseline — it was never translated"
                    )
                }
            }
        }
    }

    /// The two captions are what tell an arrival apart from a finish. If a language renders them the same,
    /// the card is back to showing one instant under one word for both legs.
    func testTheTwoClockCaptionsNeverReadTheSameInAnyLanguage() {
        for region in Self.regions {
            CoreL10n.apply(languageTag: region)

            XCTAssertNotEqual(
                LiveActivityTimeCaption.arrival.label,
                LiveActivityTimeCaption.finish.label,
                "\(region) captions an arrival and a finish identically"
            )
        }
    }

    func testOrderNumberSubstitutesTheNumberInEveryLanguage() {
        for region in Self.regions {
            CoreL10n.apply(languageTag: region)

            XCTAssertTrue(
                LiveActivityL10n.orderNumber("AB12CD34").contains("AB12CD34"),
                "\(region) dropped the order number — its format specifier does not match the English one"
            )
        }
    }
}
