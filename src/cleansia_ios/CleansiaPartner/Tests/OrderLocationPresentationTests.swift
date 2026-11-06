import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The server redacts `Address` for a caller the order does not belong to and sends
/// `CustomerAddressApproximate` to everyone. The detail screen read only the first, so a
/// cleaner who saw "Praha · 120" on the board they tapped got no location at all on the
/// screen where they decide whether the travel is worth it — and the same null hid Navigate.
///
/// The predicate is the *arrival* of the street address, never a client-side re-derivation of
/// who is entitled to it: an employee who booked a cleaning for their own home reaches this
/// screen as the order's customer, with a full address and `isAssignedToCurrentUser` false.
final class OrderLocationPresentationTests: XCTestCase {
    private let precise = OrderAddress(
        street: "Korunní 810/104",
        city: "Praha",
        zipCode: "12000",
        latitude: 50.0755,
        longitude: 14.4378
    )

    private let preciseCoordinate = Coordinate(latitude: 50.0755, longitude: 14.4378)
    private let preciseLine = "Korunní 810/104, Praha, 12000"

    private func item(address: OrderAddress?, zone: String? = "Praha · 120") -> OrderItem {
        var item = OrderItem.wireComplete()
        item.address = address
        item.customerAddressApproximate = zone
        return item
    }

    private var entitled: OrderItem {
        item(address: precise)
    }

    private var browsing: OrderItem {
        item(address: nil)
    }

    func testBrowsingCleanerGetsTheZoneTheServerSent() {
        XCTAssertEqual(OrderLocation(browsing), .approximate("Praha · 120"))
        XCTAssertEqual(OrderLocation(browsing).line, "Praha · 120")
    }

    func testBrowsingCleanerGetsNoNavigate() {
        XCTAssertNil(OrderLocation(browsing).navigationTarget)
    }

    func testEntitledReaderKeepsTheStreetAddressAndNavigate() {
        let location = OrderLocation(entitled)
        XCTAssertEqual(location, .precise(.init(line: preciseLine, coordinate: preciseCoordinate)))
        XCTAssertEqual(location.navigationTarget?.line, preciseLine)
        XCTAssertEqual(location.navigationTarget?.coordinate, preciseCoordinate)
    }

    /// The mirror of the browsing case: the entitled reader's line and Navigate target are what they
    /// were in EVERY status the wire can carry. Status is not an input to either.
    func testEntitledReaderSeesTheSameTwoFactsInEveryStatus() {
        for status in OrderStatus.allCases {
            let location = OrderLocation(entitled)
            XCTAssertEqual(location.line, preciseLine, "\(status) dropped the street address")
            XCTAssertEqual(location.navigationTarget?.line, preciseLine, "\(status) dropped Navigate")
        }
    }

    /// Orders that predate the geocoding backfill navigate by free-text query.
    func testEntitledAddressWithoutCoordinatesStillNavigates() {
        var address = precise
        address.latitude = nil
        address.longitude = nil
        let location = OrderLocation(item(address: address))
        XCTAssertEqual(location.line, preciseLine)
        XCTAssertEqual(location.navigationTarget?.line, preciseLine)
        XCTAssertNil(location.navigationTarget?.coordinate)
    }

    /// `BuildApproximateAddress` returns an empty string, not null, for an order with no city —
    /// so an absent zone arrives as `""` and must render nothing rather than an empty location row.
    func testEmptyZoneIsNotALocation() {
        let location = OrderLocation(item(address: nil, zone: ""))
        XCTAssertEqual(location, .none)
        XCTAssertNil(location.line)
        XCTAssertNil(location.navigationTarget)
    }

    func testAddressRecordWithNoUsableTextFallsBackToTheZone() {
        let location = OrderLocation(item(address: OrderAddress(latitude: 50.0755, longitude: 14.4378)))
        XCTAssertEqual(location, .approximate("Praha · 120"))
        XCTAssertNil(location.navigationTarget)
    }

    func testNoAddressAndNoZoneRendersNothing() {
        let location = OrderLocation(OrderItem())
        XCTAssertEqual(location, .none)
        XCTAssertNil(location.line)
    }

    // MARK: - What the screen reads

    func testDetailHandsTheBrowsingCleanerTheZoneAndNoNavigate() throws {
        let detail = try OrderDetail(browsing)
        XCTAssertEqual(detail.location.line, "Praha · 120")
        XCTAssertNil(detail.location.navigationTarget)
    }

    func testDetailHandsTheEntitledReaderTheStreetAddressAndNavigate() throws {
        let detail = try OrderDetail(entitled)
        XCTAssertEqual(detail.location.line, preciseLine)
        XCTAssertEqual(detail.location.navigationTarget?.coordinate, preciseCoordinate)
    }
}
