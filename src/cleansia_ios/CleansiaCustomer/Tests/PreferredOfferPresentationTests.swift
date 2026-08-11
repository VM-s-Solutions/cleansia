import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The reservation as the customer sees it. Three states and nothing else, because the fourth thing the
/// server knows — WHY a reservation ended — is a fact about a person the platform has ruled it will not
/// report. A decline and a silence resolve to the same state here and to the same sentence on screen.
final class PreferredOfferPresentationTests: XCTestCase {
    private let deadline = Date(timeIntervalSince1970: 1_800_000_000)

    func testAnOrderThatNeverAskedForAnyoneDisclosesNothing() {
        XCTAssertNil(PreferredOfferPresentation.disclosure(for: order(offer: nil)))
    }

    /// `None` covers a non-member, a declined resolve and the whole notify-only lead-time band, where
    /// nothing was withheld from anybody and there is nothing to explain.
    func testAnOrderWithNoReservationDisclosesNothing() {
        XCTAssertNil(PreferredOfferPresentation.disclosure(for: order(offer: details(state: ._0))))
    }

    func testARunningReservationIsTheAskedState() {
        let disclosure = PreferredOfferPresentation.disclosure(
            for: order(offer: details(state: ._1, name: "Jana", respondBy: deadline))
        )

        XCTAssertEqual(disclosure, .asked(cleanerName: "Jana", respondBy: deadline))
    }

    func testAReservationTheCleanerTookIsTheAcceptedState() {
        let disclosure = PreferredOfferPresentation.disclosure(
            for: order(offer: details(state: ._2, name: "Jana"))
        )

        XCTAssertEqual(disclosure, .accepted(cleanerName: "Jana"))
    }

    /// The closed state carries no name and no reason. It is the same value whether the cleaner refused
    /// or never answered, so nothing downstream can tell the two apart even by accident.
    func testAnEndedReservationIsTheClosedStateAndCarriesNothingElse() {
        let declined = PreferredOfferPresentation.disclosure(
            for: order(offer: details(state: ._3, name: "Jana"))
        )
        let lapsed = PreferredOfferPresentation.disclosure(for: order(offer: details(state: ._3)))

        XCTAssertEqual(declined, .closed)
        XCTAssertEqual(lapsed, .closed)
    }

    /// "We've asked " is worse than saying nothing. Both halves of the sentence are required or the
    /// state is not disclosed at all.
    func testAnAskWithNothingToPutInTheSentenceDisclosesNothing() {
        XCTAssertNil(PreferredOfferPresentation.disclosure(
            for: order(offer: details(state: ._1, name: nil, respondBy: deadline))
        ))
        XCTAssertNil(PreferredOfferPresentation.disclosure(
            for: order(offer: details(state: ._1, name: "   ", respondBy: deadline))
        ))
        XCTAssertNil(PreferredOfferPresentation.disclosure(
            for: order(offer: details(state: ._1, name: "Jana", respondBy: nil))
        ))
    }

    func testAnAcceptedReservationWithNoNameDisclosesNothing() {
        XCTAssertNil(PreferredOfferPresentation.disclosure(for: order(offer: details(state: ._2, name: nil))))
        XCTAssertNil(PreferredOfferPresentation.disclosure(for: order(offer: details(state: ._2, name: " "))))
    }

    /// The "this booking is now open to our whole team" sentence really does stop being true on a
    /// concluded booking — but deciding that is the server's job now (ADR-0049 §D4), and it withholds the
    /// whole block. So a block that ARRIVES is one the server means, on every fulfilment state, and a
    /// client-side status veto would only silence sentences the server had already judged true.
    func testAnArrivedBlockIsDisclosedOnEveryFulfilmentState() {
        for status in OrderStatus.allCases {
            XCTAssertEqual(
                PreferredOfferPresentation.disclosure(
                    for: order(status: status, offer: details(state: ._3))
                ),
                .closed,
                "status \(status) vetoed a block the server chose to send"
            )
            XCTAssertEqual(
                PreferredOfferPresentation.disclosure(
                    for: order(status: status, offer: details(state: ._2, name: "Jana"))
                ),
                .accepted(cleanerName: "Jana"),
                "status \(status) vetoed a block the server chose to send"
            )
        }
    }

    /// The block's arrival is the whole gate, so it does not need a status to be one — and the wire
    /// carries the status inside an envelope that can arrive empty.
    func testTheOrderStatusIsNotReadAtAll() {
        XCTAssertEqual(
            PreferredOfferPresentation.disclosure(for: statelessOrder(offer: details(state: ._3))),
            .closed
        )
        XCTAssertNil(PreferredOfferPresentation.disclosure(for: statelessOrder(offer: nil)))
    }

    private func details(
        state: PreferredOfferState,
        name: String? = nil,
        respondBy: Date? = nil
    ) -> PreferredOfferDetails {
        PreferredOfferDetails(state: state, cleanerName: name, respondByUtc: respondBy, canChooseAnother: false)
    }

    private func order(status: OrderStatus = ._2, offer: PreferredOfferDetails?) -> CustomerOrderDetail {
        OrderFixtures.detail(statusCode: Code(value: status.rawValue), preferredOffer: offer)
    }

    private func statelessOrder(offer: PreferredOfferDetails?) -> CustomerOrderDetail {
        OrderFixtures.detail(statusCode: nil, preferredOffer: offer)
    }
}
