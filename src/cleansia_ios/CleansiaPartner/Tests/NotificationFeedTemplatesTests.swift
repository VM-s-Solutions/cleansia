import Foundation
import XCTest
@testable import CleansiaPartner

final class NotificationFeedTemplatesTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    func testNewAvailableRowRendersTheApnsTemplateWithTheCount() throws {
        let rendered = try XCTUnwrap(NotificationFeedTemplates.render(
            eventKey: "order.new_available",
            args: ["count": "3"]
        ))
        XCTAssertEqual(rendered.title, L10n.localized("push.order.new_available.title"))
        XCTAssertEqual(
            rendered.body,
            String(format: L10n.localized("push.order.new_available.body"), "3")
        )
        XCTAssertTrue(rendered.body.contains("3"))
    }

    func testUnknownEventKeyHidesTheRow() {
        XCTAssertNil(NotificationFeedTemplates.render(eventKey: "order.confirmed", args: [:]))
        XCTAssertNil(NotificationFeedTemplates.render(eventKey: "promo.new_sitewide", args: [:]))
        XCTAssertNil(NotificationFeedTemplates.render(eventKey: "some.future_event", args: [:]))
    }

    func testEveryPartnerFeedKeyRendersANonRawTemplate() {
        for eventKey in PartnerFeedEventKeys.all {
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
        let known = NotificationFixtures.item(id: "n-1", eventKey: "order.new_available")
        let customerKey = NotificationFixtures.item(id: "n-2", eventKey: "order.completed")
        let read = NotificationFixtures.item(id: "n-3", eventKey: "order.new_available", readOn: Date())

        let rows = NotificationFeedTemplates.rows(from: [known, customerKey, read])

        XCTAssertEqual(rows.map(\.id), ["n-1", "n-3"])
        XCTAssertEqual(rows.map(\.isUnread), [true, false])
    }

    func testTimestampIsRelativeWithinAWeekAndAbsoluteBeyond() {
        let now = Date(timeIntervalSince1970: 1_750_000_000)
        let locale = Locale(identifier: "en")
        let recent = NotificationFeedFormat.timestamp(now.addingTimeInterval(-3600), relativeTo: now, locale: locale)
        XCTAssertFalse(recent.isEmpty)
        let oldDate = now.addingTimeInterval(-30 * 24 * 3600)
        let old = NotificationFeedFormat.timestamp(oldDate, relativeTo: now, locale: locale)
        XCTAssertEqual(old, OrdersFormat.relativeDateTime(oldDate, locale: locale))
    }

    func testNewInboxKeysShipInEveryLanguageTable() throws {
        let keys = [
            "notifications_inbox_title",
            "notifications_inbox_empty_title",
            "notifications_inbox_empty_subtitle",
            "notifications_inbox_error",
            "notifications_inbox_unread_a11y",
            "notifications_inbox_close"
        ]
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = table[key]
                XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                XCTAssertFalse(value?.isEmpty ?? true, "\(key) empty in \(language).lproj")
            }
            XCTAssertTrue(
                table["notifications_inbox_unread_a11y"]?.contains("%1$@") ?? false,
                "unread a11y must carry the %1$@ slot in \(language)"
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
