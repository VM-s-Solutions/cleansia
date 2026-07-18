import Foundation
import XCTest
@testable import CleansiaCustomer

final class NotificationFeedTemplatesTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    func testOrderRowRendersTheApnsTemplateWithTheOrderNumber() throws {
        let rendered = try XCTUnwrap(NotificationFeedTemplates.render(
            eventKey: "order.completed",
            args: ["orderNumber": "A-1042"]
        ))
        XCTAssertEqual(rendered.title, L10n.localized("push.order.completed.title"))
        XCTAssertEqual(
            rendered.body,
            String(format: L10n.localized("push.order.completed.body"), "A-1042")
        )
        XCTAssertTrue(rendered.body.contains("A-1042"))
    }

    func testArglessEventsRenderTheTemplateVerbatim() throws {
        let rendered = try XCTUnwrap(NotificationFeedTemplates.render(eventKey: "dispute.reply", args: [:]))
        XCTAssertEqual(rendered.body, L10n.localized("push.dispute.reply.body"))
        XCTAssertFalse(rendered.body.contains("%"))
    }

    func testLoyaltyRowRendersTheLocalizedTierLabel() throws {
        let rendered = try XCTUnwrap(NotificationFeedTemplates.render(
            eventKey: "loyalty.tier_upgrade",
            args: ["tier": "SilverMopper"]
        ))
        XCTAssertTrue(rendered.body.contains(L10n.localized("loyalty_tier_silver_mopper")))
        XCTAssertFalse(rendered.body.contains("%"))
    }

    func testLoyaltyRowFallsBackToTheRawTierForAnUnknownEnumName() throws {
        let rendered = try XCTUnwrap(NotificationFeedTemplates.render(
            eventKey: "loyalty.tier_upgrade",
            args: ["tier": "DiamondDuster"]
        ))
        XCTAssertTrue(rendered.body.contains("DiamondDuster"))
    }

    func testUnknownEventKeyHidesTheRow() {
        XCTAssertNil(NotificationFeedTemplates.render(eventKey: "some.future_event", args: [:]))
        XCTAssertNil(NotificationFeedTemplates.render(eventKey: "promo.new_sitewide", args: [:]))
        XCTAssertNil(NotificationFeedTemplates.render(eventKey: "order.new_available", args: [:]))
    }

    func testEveryCustomerFeedKeyRendersANonRawTemplate() {
        for eventKey in CustomerFeedEventKeys.all {
            guard let rendered = NotificationFeedTemplates.render(eventKey: eventKey, args: [:]) else {
                XCTFail("\(eventKey) must render")
                continue
            }
            XCTAssertFalse(rendered.title.isEmpty)
            XCTAssertFalse(rendered.body.isEmpty)
            XCTAssertFalse(rendered.title.hasPrefix("push."), "\(eventKey) title resolved to the raw key")
            XCTAssertFalse(rendered.body.hasPrefix("push."), "\(eventKey) body resolved to the raw key")
        }
    }

    func testRowsFilterUnknownKeysAndCarryUnreadState() {
        let known = NotificationFixtures.item(id: "n-1", eventKey: "order.confirmed")
        let unknown = NotificationFixtures.item(id: "n-2", eventKey: "some.future_event")
        let read = NotificationFixtures.item(id: "n-3", eventKey: "dispute.reply", readOn: Date())

        let rows = NotificationFeedTemplates.rows(from: [known, unknown, read])

        XCTAssertEqual(rows.map(\.id), ["n-1", "n-3"])
        XCTAssertEqual(rows.map(\.isUnread), [true, false])
    }

    func testTimestampIsRelativeWithinAWeekAndAbsoluteBeyond() {
        let now = Date(timeIntervalSince1970: 1_750_000_000)
        let locale = Locale(identifier: "en")
        let recent = NotificationFeedFormat.timestamp(now.addingTimeInterval(-3600), relativeTo: now, locale: locale)
        XCTAssertFalse(recent.isEmpty)
        let old = NotificationFeedFormat.timestamp(
            now.addingTimeInterval(-30 * 24 * 3600),
            relativeTo: now,
            locale: locale
        )
        XCTAssertEqual(old, OrdersFormat.dateTime(now.addingTimeInterval(-30 * 24 * 3600), locale: locale))
    }

    func testNewInboxKeysShipInEveryLanguageTable() throws {
        let keys = [
            "notifications_inbox_error",
            "notifications_inbox_unread_a11y",
            "notifications_inbox_loyalty_tier_body"
        ]
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = table[key]
                XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                XCTAssertFalse(value?.isEmpty ?? true, "\(key) empty in \(language).lproj")
            }
            XCTAssertTrue(
                table["notifications_inbox_loyalty_tier_body"]?.contains("%1$@") ?? false,
                "loyalty tier body must carry the %1$@ slot in \(language)"
            )
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
