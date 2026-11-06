import XCTest
@testable import CleansiaCustomer

/// `BookingPolicy.AllowsCash`: cash only for a signed-in customer whose booking the server says one
/// cleaner does alone. More than one required cleaner is card only whoever is booking; an unknown crew
/// is not a refusal.
final class CashEligibilityTests: XCTestCase {
    func testASignedInCustomerWithOneRequiredCleanerMayPayCash() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: true, requiredEmployees: 1, hasUsableSavedCard: true),
            .available
        )
    }

    func testASignedInCustomerWithTwoRequiredCleanersPaysByCard() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: true, requiredEmployees: 2, hasUsableSavedCard: true),
            .needsCard(requiredCleaners: 2)
        )
    }

    func testAGuestWithOneRequiredCleanerNeedsAnAccount() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: false, requiredEmployees: 1, hasUsableSavedCard: true),
            .needsAccount
        )
    }

    func testAGuestWithTwoRequiredCleanersIsToldAboutTheCrewFirst() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: false, requiredEmployees: 2, hasUsableSavedCard: true),
            .needsCard(requiredCleaners: 2)
        )
    }

    func testNoQuoteForTheSelectionIsPendingNotARefusal() {
        let pending = CashEligibility.resolve(signedIn: true, requiredEmployees: nil, hasUsableSavedCard: true)
        XCTAssertEqual(pending, .pending)
        XCTAssertFalse(pending.refusesCash)
    }

    func testOneCleanerStillRequiresACurrentVerifiedCard() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: true, requiredEmployees: 1, hasUsableSavedCard: false),
            .needsSavedCard
        )
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: true, requiredEmployees: 1, hasUsableSavedCard: nil),
            .pending
        )
    }

    func testOnlyTheAccountAndTheCrewRefuseCash() {
        XCTAssertTrue(CashEligibility.needsAccount.refusesCash)
        XCTAssertTrue(CashEligibility.needsCard(requiredCleaners: 3).refusesCash)
        XCTAssertFalse(CashEligibility.available.refusesCash)
        XCTAssertFalse(CashEligibility.pending.refusesCash)
    }
}
