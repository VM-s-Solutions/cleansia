import XCTest
@testable import CleansiaCore

final class HaversineTests: XCTestCase {
    func testZeroDistanceForSamePoint() {
        let point = Coordinate(latitude: 50.0755, longitude: 14.4378)
        XCTAssertEqual(point.distanceKm(to: point), 0, accuracy: 0.0001)
    }

    func testKnownShortDistanceInPrague() {
        let here = Coordinate(latitude: 50.0755, longitude: 14.4378)
        let there = Coordinate(latitude: 50.0875, longitude: 14.4213)
        XCTAssertEqual(here.distanceKm(to: there), 1.7, accuracy: 0.5)
    }

    func testSymmetry() {
        let paris = Coordinate(latitude: 48.8566, longitude: 2.3522)
        let london = Coordinate(latitude: 51.5074, longitude: -0.1278)
        XCTAssertEqual(paris.distanceKm(to: london), london.distanceKm(to: paris), accuracy: 0.0001)
    }

    func testKnownLongDistanceParisToLondon() {
        let paris = Coordinate(latitude: 48.8566, longitude: 2.3522)
        let london = Coordinate(latitude: 51.5074, longitude: -0.1278)
        XCTAssertEqual(paris.distanceKm(to: london), 343, accuracy: 5)
    }
}
