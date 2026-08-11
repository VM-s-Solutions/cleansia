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

    /// Every sentence in this block is about a booking that is still looking for a cleaner, and the
    /// closed one makes a forward-looking claim about the platform — "now open to our whole team". A
    /// cancelled booking is open to nobody, so the block says nothing rather than something false.
    func testACancelledBookingDisclosesNothing() {
        for state in [PreferredOfferState._1, ._2, ._3] {
            XCTAssertNil(
                PreferredOfferPresentation.disclosure(
                    for: order(status: ._6, offer: details(state: state, name: "Jana", respondBy: deadline))
                ),
                "state \(state) still speaks on a cancelled booking"
            )
        }
    }

    /// Same reason on a finished clean: the assignment is history, and "now open to our whole team"
    /// would be false on every past order the customer ever named a cleaner for.
    func testACompletedBookingDisclosesNothing() {
        for state in [PreferredOfferState._1, ._2, ._3] {
            XCTAssertNil(
                PreferredOfferPresentation.disclosure(
                    for: order(status: ._5, offer: details(state: state, name: "Jana", respondBy: deadline))
                ),
                "state \(state) still speaks on a completed booking"
            )
        }
    }

    func testAnOrderWithNoStatusAtAllDisclosesNothing() {
        let item = OrderItem(orderStatus: nil, preferredOffer: details(state: ._3))
        XCTAssertNil(PreferredOfferPresentation.disclosure(for: item))
    }

    private func details(
        state: PreferredOfferState,
        name: String? = nil,
        respondBy: Date? = nil
    ) -> PreferredOfferDetails {
        PreferredOfferDetails(state: state, cleanerName: name, respondByUtc: respondBy, canChooseAnother: false)
    }

    private func order(status: OrderStatus = ._2, offer: PreferredOfferDetails?) -> OrderItem {
        OrderItem(orderStatus: Code(value: status.rawValue), preferredOffer: offer)
    }
}
