import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class PayoutDetailsReadTests: XCTestCase {
    /// The body the mobile API actually returns for a cleaner with no payout record.
    private let notFoundBody = Data(
        """
        {"type":"PayoutDetailsNotFound","detail":"payout.not_found",\
        "errors":{"PayoutDetailsNotFound":"payout.not_found"}}
        """.utf8
    )

    func testNoPayoutRecordedYetIsSuccessWithNil() {
        let error = ApiError.fromProblemDetails(httpStatus: 400, body: notFoundBody)

        let result = PayoutDetailsRead.normalize(.failure(error))

        guard case let .success(details) = result else {
            return XCTFail("a cleaner who never saved payout details is not an error")
        }
        XCTAssertNil(details)
    }

    func testAGenuineFailureStaysAnError() {
        let error = ApiError.fromProblemDetails(httpStatus: 500, body: Data("boom".utf8))

        guard case .failure = PayoutDetailsRead.normalize(.failure(error)) else {
            return XCTFail("a failed read must not masquerade as an empty destination")
        }
    }

    func testAnotherBadRequestStaysAnError() {
        let body = Data(#"{"type":"Forbidden","errors":{"Forbidden":"auth.forbidden"}}"#.utf8)
        let error = ApiError.fromProblemDetails(httpStatus: 400, body: body)

        guard case .failure = PayoutDetailsRead.normalize(.failure(error)) else {
            return XCTFail("only payout.not_found normalizes to an empty destination")
        }
    }

    func testAStoredDestinationPassesThrough() {
        let stored = MyPayoutDetails(accountNumber: "5885638003", bankCode: "5500")

        let result = PayoutDetailsRead.normalize(.success(stored))

        XCTAssertEqual(result.apiValueOrNil??.accountNumber, "5885638003")
    }
}

private extension Result where Failure == ApiError {
    var apiValueOrNil: Success? {
        try? get()
    }
}
