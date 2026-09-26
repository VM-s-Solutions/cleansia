import XCTest
@testable import CleansiaCustomer

/// `BookingPolicy.AllowsCash`: cash only for a signed-in customer whose booking the server says one
/// cleaner does alone. More than one required cleaner is card only whoever is booking; an unknown crew
/// is not a refusal.
final class CashEligibilityTests: XCTestCase {
    func testASignedInCustomerWithOneRequiredCleanerMayPayCash() {
        XCTAssertEqual(CashEligibility.resolve(signedIn: true, requiredEmployees: 1), .available)
    }

    func testASignedInCustomerWithTwoRequiredCleanersPaysByCard() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: true, requiredEmployees: 2),
            .needsCard(requiredCleaners: 2)
        )
    }

    func testAGuestWithOneRequiredCleanerNeedsAnAccount() {
        XCTAssertEqual(CashEligibility.resolve(signedIn: false, requiredEmployees: 1), .needsAccount)
    }

    func testAGuestWithTwoRequiredCleanersIsToldAboutTheCrewFirst() {
        XCTAssertEqual(
            CashEligibility.resolve(signedIn: false, requiredEmployees: 2),
            .needsCard(requiredCleaners: 2)
        )
    }

    func testNoQuoteForTheSelectionIsPendingNotARefusal() {
        let pending = CashEligibility.resolve(signedIn: true, requiredEmployees: nil)
        XCTAssertEqual(pending, .pending)
        XCTAssertFalse(pending.refusesCash)
    }

    func testOnlyTheAccountAndTheCrewRefuseCash() {
        XCTAssertTrue(CashEligibility.needsAccount.refusesCash)
        XCTAssertTrue(CashEligibility.needsCard(requiredCleaners: 3).refusesCash)
        XCTAssertFalse(CashEligibility.available.refusesCash)
        XCTAssertFalse(CashEligibility.pending.refusesCash)
    }
}
