import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class PayoutAccountSummaryTests: XCTestCase {
    func testLocalPartsRenderTheWayTheBankStatementDoes() {
        let summary = PayoutAccountSummary.text(
            for: MyPayoutDetails(accountNumber: "5885638003", bankCode: "5500")
        )

        XCTAssertEqual(summary, "5885638003/5500")
    }

    func testStoredZeroPaddingIsStrippedFromBothThePrefixAndTheNumber() {
        let summary = PayoutAccountSummary.text(
            for: MyPayoutDetails(accountPrefix: "000019", accountNumber: "0002000145", bankCode: "0800")
        )

        XCTAssertEqual(summary, "19-2000145/0800")
    }

    func testAnAllZeroPrefixIsNotAPrefix() {
        let summary = PayoutAccountSummary.text(
            for: MyPayoutDetails(accountPrefix: "000000", accountNumber: "5885638003", bankCode: "5500")
        )

        XCTAssertEqual(summary, "5885638003/5500")
    }

    func testAnAccountWithNoLocalPartsFallsBackToTheIban() {
        let summary = PayoutAccountSummary.text(for: MyPayoutDetails(iban: "DE89370400440532013000"))

        XCTAssertEqual(summary, "DE89370400440532013000")
    }

    func testLocalPartsWinOverTheDerivedIban() {
        let summary = PayoutAccountSummary.text(
            for: MyPayoutDetails(
                accountNumber: "5885638003",
                bankCode: "5500",
                iban: "CZ3155000000005885638003"
            )
        )

        XCTAssertEqual(summary, "5885638003/5500")
    }

    func testNoDetailsAtAllIsNoSummary() {
        XCTAssertNil(PayoutAccountSummary.text(for: nil))
        XCTAssertNil(PayoutAccountSummary.text(for: MyPayoutDetails()))
    }

    func testAnAccountNumberWithNoBankCodeStillRenders() {
        let summary = PayoutAccountSummary.text(for: MyPayoutDetails(accountNumber: "0005885638003"))

        XCTAssertEqual(summary, "5885638003")
    }
}
