import CoreLocation
import XCTest
@testable import CleansiaCore

final class LocationAuthorizationStatusMappingTests: XCTestCase {
    func testNotDetermined() {
        XCTAssertEqual(LocationAuthorizationStatus(CLAuthorizationStatus.notDetermined), .notDetermined)
    }

    func testDenied() {
        XCTAssertEqual(LocationAuthorizationStatus(CLAuthorizationStatus.denied), .denied)
    }

    func testRestricted() {
        XCTAssertEqual(LocationAuthorizationStatus(CLAuthorizationStatus.restricted), .restricted)
    }

    func testAuthorizedWhenInUse() {
        XCTAssertEqual(LocationAuthorizationStatus(CLAuthorizationStatus.authorizedWhenInUse), .authorized)
    }

    func testAuthorizedAlways() {
        XCTAssertEqual(LocationAuthorizationStatus(CLAuthorizationStatus.authorizedAlways), .authorized)
    }
}
