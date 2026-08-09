import XCTest
@testable import CleansiaPartner

final class JobRadiusPromptTests: XCTestCase {
    func testACleanerWhoHasNeverBeenAskedIsAsked() {
        XCTAssertTrue(JobRadiusPrompt.shouldPresent(radiusKm: nil, hasBeenAsked: false))
    }

    func testACleanerWhoAlreadyChoseCountryWideIsNotAskedAgain() {
        XCTAssertFalse(JobRadiusPrompt.shouldPresent(radiusKm: nil, hasBeenAsked: true))
    }

    func testACleanerWithARadiusIsNeverAsked() {
        XCTAssertFalse(JobRadiusPrompt.shouldPresent(radiusKm: 25, hasBeenAsked: false))
        XCTAssertFalse(JobRadiusPrompt.shouldPresent(radiusKm: 25, hasBeenAsked: true))
    }
}
