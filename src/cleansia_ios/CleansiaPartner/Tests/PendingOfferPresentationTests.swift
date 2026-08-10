import CleansiaPartnerApi
import Foundation
import XCTest
@testable import CleansiaPartner

/// The deadline is rendered as an instant and never as a remaining time: the hold's real expiry is
/// server-side, so "expires in 12 minutes" on a screen that has been open for twenty is a lie the
/// client cannot detect. Everything here resolves to a wall-clock label plus the calendar day it falls
/// on, and the caller supplies `now` so the classification is assertable.
final class PendingOfferPresentationTests: XCTestCase {
    private let english = Locale(identifier: "en")

    private var utc: Calendar {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }

    private func at(_ iso: String) -> Date {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter.date(from: iso)!
    }

    func testADeadlineLaterTheSameLocalDayIsTodayAndCarriesItsWallClockTime() {
        let respondBy = PendingOfferPresentation.respondBy(
            at("2026-08-10T18:40:00Z"),
            now: at("2026-08-10T09:00:00Z"),
            calendar: utc,
            locale: english
        )

        XCTAssertEqual(respondBy?.day, .today)
        XCTAssertEqual(respondBy?.time, "18:40")
    }

    /// Three hours away, but on the next calendar day in the rendering zone. A delta-based
    /// classification says "today" here and the cleaner reads a deadline their own calendar has
    /// already passed.
    func testTheDayIsTheLocalCalendarDayNotATwentyFourHourDelta() {
        let respondBy = PendingOfferPresentation.respondBy(
            at("2026-08-11T01:00:00Z"),
            now: at("2026-08-10T22:00:00Z"),
            calendar: utc,
            locale: english
        )

        XCTAssertEqual(respondBy?.day, .tomorrow)
    }

    func testADeadlineFurtherOutIsLaterAndCarriesADateAsWellAsATime() {
        let respondBy = PendingOfferPresentation.respondBy(
            at("2026-08-14T07:05:00Z"),
            now: at("2026-08-10T09:00:00Z"),
            calendar: utc,
            locale: english
        )

        XCTAssertEqual(respondBy?.day, .later)
        XCTAssertEqual(respondBy?.time, "07:05")
        XCTAssertEqual(respondBy?.date.contains("14"), true)
    }

    /// The server's own predicate is `PreferredHoldUntilUtc > nowUtc`, so equality is over.
    func testADeadlineAlreadyReachedIsEndedNeverATimeTheCleanerCouldStillActOn() {
        XCTAssertEqual(
            PendingOfferPresentation.respondBy(
                at("2026-08-10T09:00:00Z"),
                now: at("2026-08-10T09:00:00Z"),
                calendar: utc,
                locale: english
            )?.day,
            .ended
        )
        XCTAssertEqual(
            PendingOfferPresentation.respondBy(
                at("2026-08-10T08:59:59Z"),
                now: at("2026-08-10T09:00:00Z"),
                calendar: utc,
                locale: english
            )?.day,
            .ended
        )
    }

    func testAnAbsentDeadlineResolvesToNothingRatherThanAGuess() {
        XCTAssertNil(
            PendingOfferPresentation.respondBy(
                nil,
                now: at("2026-08-10T09:00:00Z"),
                calendar: utc,
                locale: english
            )
        )
    }

    func testTheDeadlineIsRenderedInTheRequestedZoneNotInUtc() throws {
        var prague = Calendar(identifier: .gregorian)
        prague.timeZone = try XCTUnwrap(TimeZone(identifier: "Europe/Prague"))

        XCTAssertEqual(
            PendingOfferPresentation.respondBy(
                at("2026-08-10T21:40:00Z"),
                now: at("2026-08-10T09:00:00Z"),
                calendar: prague,
                locale: english
            )?.time,
            "23:40"
        )
    }

    func testTheSoonestOfferIsTheEarliestDeadlineNotTheFirstRowTheServerSent() {
        let offers = [
            PendingOfferItem.sample(id: "late", respondByUtc: at("2026-08-10T20:00:00Z")),
            PendingOfferItem.sample(id: "soon", respondByUtc: at("2026-08-10T10:00:00Z")),
            PendingOfferItem.sample(id: "middle", respondByUtc: at("2026-08-10T15:00:00Z"))
        ]

        XCTAssertEqual(PendingOfferPresentation.soonestOffer(offers)?.id, "soon")
    }

    func testAnOfferWithNoDeadlineNeverWinsTheSoonestSlot() {
        let offers = [
            PendingOfferItem.sample(id: "broken", respondByUtc: nil),
            PendingOfferItem.sample(id: "real", respondByUtc: at("2026-08-10T20:00:00Z"))
        ]

        XCTAssertEqual(PendingOfferPresentation.soonestOffer(offers)?.id, "real")
    }

    func testNoOffersMeansNoSoonest() {
        XCTAssertNil(PendingOfferPresentation.soonestOffer([]))
        XCTAssertNil(PendingOfferPresentation.soonestOffer([.sample(id: "broken", respondByUtc: nil)]))
    }
}
