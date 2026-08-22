@testable import CleansiaCustomer
import XCTest

/// The rule that decides whether to interrupt a customer on app open, and the chip set it offers.
///
/// Pure functions on purpose — the same reasoning as `JobRadiusPrompt` on the partner side. A rule
/// that decides to interrupt someone should be assertable without standing up a view.
final class ReviewPromptTests: XCTestCase {

    private func order(
        _ id: String,
        statusValue: Int = 5,
        hasReview: Bool = false,
        cleaningDateTime: Date? = Date(timeIntervalSince1970: 1_755_000_000)
    ) -> CustomerOrderSummary {
        OrderFakes.summary(
            id: id,
            statusCode: Code(type: "OrderStatus", name: nil, value: statusValue),
            cleaningDateTime: cleaningDateTime,
            hasReview: hasReview
        )
    }

    func testACompletedUnreviewedOrderIsTheCandidate() {
        let candidate = ReviewPrompt.candidate(orders: [order("ord-1")], alreadyPrompted: [])

        XCTAssertEqual(candidate?.id, "ord-1")
    }

    func testAnOrderTheServerSaysIsReviewedIsNeverOffered() {
        XCTAssertNil(
            ReviewPrompt.candidate(orders: [order("ord-1", hasReview: true)], alreadyPrompted: []),
            "server truth must outrank the local prompted flag"
        )
    }

    func testAnOrderAlreadyPromptedForIsNotOfferedAgain() {
        XCTAssertNil(ReviewPrompt.candidate(orders: [order("ord-1")], alreadyPrompted: ["ord-1"]))
    }

    func testOnlyCompletedOrdersAreOffered() {
        // 0 New, 2 Confirmed, 3 OnTheWay, 4 InProgress, 6 Cancelled — none of them finished work.
        for status in [0, 2, 3, 4, 6] {
            XCTAssertNil(
                ReviewPrompt.candidate(orders: [order("ord-\(status)", statusValue: status)], alreadyPrompted: []),
                "status \(status) must not be offered for review"
            )
        }
    }

    func testTheNewestCompletedOrderWins() {
        let candidate = ReviewPrompt.candidate(
            orders: [
                order("old", cleaningDateTime: Date(timeIntervalSince1970: 1_750_000_000)),
                order("new", cleaningDateTime: Date(timeIntervalSince1970: 1_756_000_000)),
                order("middle", cleaningDateTime: Date(timeIntervalSince1970: 1_753_000_000)),
            ],
            alreadyPrompted: []
        )

        XCTAssertEqual(candidate?.id, "new")
    }

    func testAnEmptyListAsksNothing() {
        XCTAssertNil(ReviewPrompt.candidate(orders: [], alreadyPrompted: []))
    }

    /// Keyed per ORDER, not per user — a second completed clean must still ask.
    func testTheSettingsKeyIsPerOrder() {
        XCTAssertNotEqual(
            ReviewPrompt.settingsKey(orderId: "ord-1"),
            ReviewPrompt.settingsKey(orderId: "ord-2")
        )
    }
}

final class CustomerReviewTagTests: XCTestCase {

    func testLowRatingsOfferOnlyNegativeTags() {
        for rating in 1 ... 3 {
            let offered = CustomerReviewTag.forRating(rating)
            XCTAssertFalse(offered.isEmpty, "rating \(rating) offered nothing")
            XCTAssertTrue(offered.allSatisfy { !$0.isPositive }, "rating \(rating) offered a positive tag")
        }
    }

    func testHighRatingsOfferOnlyPositiveTags() {
        for rating in 4 ... 5 {
            let offered = CustomerReviewTag.forRating(rating)
            XCTAssertFalse(offered.isEmpty, "rating \(rating) offered nothing")
            XCTAssertTrue(offered.allSatisfy(\.isPositive), "rating \(rating) offered a negative tag")
        }
    }

    func testARatingOutsideOneToFiveOffersNothing() {
        for rating in [0, 6, -1] {
            XCTAssertTrue(CustomerReviewTag.forRating(rating).isEmpty)
        }
    }

    func testEveryTagSitsInExactlyOnePolarityBand() {
        let positive = CustomerReviewTag.forRating(5)
        let negative = CustomerReviewTag.forRating(1)

        XCTAssertEqual(CustomerReviewTag.allCases.count, positive.count + negative.count)
        XCTAssertTrue(Set(positive).isDisjoint(with: Set(negative)))
    }

    /// The integers ARE the wire contract — the backend enum, the OpenAPI spec, Android and this must
    /// agree, and a renumbering silently reinterprets every stored review.
    func testWireCodesAreFrozen() {
        XCTAssertEqual(
            CustomerReviewTag.allCases.map(\.rawValue),
            [1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18]
        )
    }

    func testAnUnknownCodeResolvesToNil() {
        XCTAssertNil(CustomerReviewTag(rawValue: 999))
    }
}
