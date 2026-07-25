import XCTest
@testable import CleansiaCore

/// The Live Activity is rendered by a widget extension we cannot exercise from here, so this pins the part
/// that IS testable: every accessor the widget reads resolves out of the Core catalog, in all five app
/// languages, off the English source.
final class LiveActivityL10nTests: XCTestCase {
    private static let regions = ["en", "cs", "sk", "uk", "ru"]

    private static let accessors: [(name: String, read: () -> String)] = [
        ("status.on_the_way.title", { LiveActivityL10n.Status.onTheWayTitle }),
        ("status.on_the_way.detail", { LiveActivityL10n.Status.onTheWayDetail }),
        ("status.in_progress.title", { LiveActivityL10n.Status.inProgressTitle }),
        ("status.in_progress.detail", { LiveActivityL10n.Status.inProgressDetail }),
        ("status.completed.title", { LiveActivityL10n.Status.completedTitle }),
        ("status.completed.detail", { LiveActivityL10n.Status.completedDetail }),
        ("status.cancelled.title", { LiveActivityL10n.Status.cancelledTitle }),
        ("status.cancelled.detail", { LiveActivityL10n.Status.cancelledDetail }),
        ("status.generic.title", { LiveActivityL10n.Status.genericTitle }),
        ("eta.remaining", { LiveActivityL10n.Eta.remaining }),
        ("eta.left", { LiveActivityL10n.Eta.left }),
        ("eta.elapsed", { LiveActivityL10n.Eta.elapsed }),
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
