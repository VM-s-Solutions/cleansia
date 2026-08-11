import XCTest
@testable import CleansiaCore

final class WireContractTests: XCTestCase {
    private struct StatusError: Error {
        let status: Int
    }

    func testRequireReturnsThePresentValue() throws {
        let total: Double? = 4800
        XCTAssertEqual(try total.require("totalPay"), 4800)
    }

    func testRequireRefusesNilAndNamesTheField() {
        let total: Double? = nil
        XCTAssertThrowsError(try total.require("totalPay")) { error in
            XCTAssertEqual(error as? WireContractViolation, WireContractViolation(field: "totalPay"))
        }
    }

    func testRequireNonBlankRefusesTheServersEmptyString() {
        for blank in ["", "   ", "\n"] {
            let id: String? = blank
            XCTAssertThrowsError(
                try id.requireNonBlank("id"),
                "\(blank.debugDescription) is an absent field"
            ) { error in
                XCTAssertEqual(error as? WireContractViolation, WireContractViolation(field: "id"))
            }
        }
    }

    func testRequireNonBlankPassesRealText() throws {
        let id: String? = "employee-1"
        XCTAssertEqual(try id.requireNonBlank("id"), "employee-1")
    }

    func testAViolationCannotDegradeToSuccess() async {
        let result: ApiResult<Double> = await apiResult { try Double?.none.require("totalPay") }
        XCTAssertNil(try? result.get())
    }

    func testAViolationCarriesTheFieldNameAndTheEndpoint() async {
        let result: ApiResult<Double> = await apiResult(endpoint: "getPeriodPays(employeeId:payPeriodId:)") {
            try Double?.none.require("totalPay")
        }
        let message = result.apiErrorOrNil?.message
        XCTAssertEqual(message?.contains("totalPay"), true)
        XCTAssertEqual(message?.contains("getPeriodPays(employeeId:payPeriodId:)"), true)
    }

    func testTheEndpointDefaultsToTheEnclosingClientMethod() async {
        let result: ApiResult<Double> = await apiResult { try Double?.none.require("totalPay") }
        XCTAssertEqual(
            result.apiErrorOrNil?.message?.contains("testTheEndpointDefaultsToTheEnclosingClientMethod()"),
            true
        )
    }

    /// A 200 whose body breaks the contract is a server fault: `network.*` would send the cleaner to
    /// check their connection and an investigator to the subsystem that did not fail.
    func testAViolationIsAttributedToTheServerNotTheNetwork() async {
        let result: ApiResult<Double> = await apiResult { try Double?.none.require("totalPay") }
        XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode)
        XCTAssertEqual(result.apiErrorOrNil?.httpStatus, 200)
    }

    func testAViolationSurvivesAGeneratedErrorMapper() async {
        let result: ApiResult<Double> = await apiResult(
            mapError: { _ in ApiError(code: "network.unknown") },
            { try Double?.none.require("totalPay") }
        )
        XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.wireContractCode)
    }

    func testAViolationRendersAsALocalizedLineAndNeverAsTheRawKey() {
        let error = ApiError(WireContractViolation(field: "totalPay"), endpoint: "getPeriodPays()")
        let rendered = ApiErrorLocalizer().message(for: error)
        XCTAssertFalse(rendered.contains(ApiError.wireContractCode))
        XCTAssertFalse(rendered.contains("totalPay"))
        XCTAssertFalse(rendered.isEmpty)
    }

    func testAThrownApiErrorReachesTheCallerIntact() async {
        let refusal = ApiError(code: "device.revoke_failed", httpStatus: 409)
        let result: ApiResult<Void> = await apiResult(mapError: ApiError.from) { throw refusal }
        XCTAssertEqual(result.apiErrorOrNil, refusal)
    }

    func testCancellationStillWinsOverEverythingElse() async {
        let result: ApiResult<Int> = await apiResult { throw CancellationError() }
        XCTAssertEqual(result.apiErrorOrNil?.code, ApiError.cancelledCode)
    }

    func testUnrelatedErrorsStillGoThroughTheMapper() async {
        let result: ApiResult<Int> = await apiResult(
            mapError: { error in ApiError(httpStatus: (error as? StatusError)?.status) },
            { throw StatusError(status: 503) }
        )
        XCTAssertEqual(result.apiErrorOrNil?.httpStatus, 503)
    }
}
