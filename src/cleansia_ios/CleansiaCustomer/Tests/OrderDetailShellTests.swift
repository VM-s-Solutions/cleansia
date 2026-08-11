import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The three decisions the map+sheet shell makes before any view is built: which
/// mascot rides the sheet edge, whether there is a map behind it at all, and which
/// blocks of the instructions card are secret.
final class OrderDetailShellTests: XCTestCase {
    // MARK: - Mascot puck

    func testInProgressPlaysTheAnimatedCleaningLoop() {
        XCTAssertEqual(
            OrderDetailMascotArt.art(for: ._4),
            .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        )
    }

    func testConfirmedAndOnTheWayPlayTheWelcomeOnce() {
        for status in [OrderStatus._2, ._3] {
            XCTAssertEqual(
                OrderDetailMascotArt.art(for: status),
                .animated(.welcoming, loop: false, fallback: .waving),
                "status \(status)"
            )
        }
    }

    func testNotYetAcceptedOrdersGetAStillMascot() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._0), .still(.leaning))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._1), .still(.leaning))
    }

    func testCompletedGetsAStillMascot() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._5), .still(.ready))
    }

    func testCancelledAndUnknownCarryNoMascot() {
        XCTAssertNil(OrderDetailMascotArt.art(for: ._6))
        XCTAssertNil(OrderDetailMascotArt.art(for: nil))
    }

    func testOnlyTheInProgressLoopRepeats() {
        // A looping welcome would wave forever behind the sheet edge.
        let looping = OrderStatus.allCases.filter {
            if case .animated(_, true, _) = OrderDetailMascotArt.art(for: $0) { return true }
            return false
        }
        XCTAssertEqual(looping, [._4])
    }

    // MARK: - Map backdrop

    func testMapShowsForAnOrderWithCoordinates() {
        let order = orderAt(latitude: 50.0755, longitude: 14.4378, status: ._2)
        XCTAssertEqual(OrderDetailMap.coordinate(for: order)?.latitude, 50.0755)
        XCTAssertEqual(OrderDetailMap.coordinate(for: order)?.longitude, 14.4378)
    }

    func testMapSurvivesCompletion() {
        // The cleaning happened there; only a visit that never happened loses it.
        XCTAssertNotNil(OrderDetailMap.coordinate(for: orderAt(latitude: 50, longitude: 14, status: ._5)))
    }

    func testCancelledOrderHasNoMap() {
        XCTAssertNil(OrderDetailMap.coordinate(for: orderAt(latitude: 50, longitude: 14, status: ._6)))
    }

    func testMissingEitherCoordinateHasNoMap() {
        XCTAssertNil(OrderDetailMap.coordinate(for: orderAt(latitude: nil, longitude: 14, status: ._2)))
        XCTAssertNil(OrderDetailMap.coordinate(for: orderAt(latitude: 50, longitude: nil, status: ._2)))
    }

    func testMissingAddressHasNoMap() {
        XCTAssertNil(OrderDetailMap.coordinate(for: OrderFixtures.detail(statusCode: Code(value: 2))))
    }

    // MARK: - Instructions split

    func testAccessInstructionsAreNeverAPlainBlock() {
        let order = OrderFixtures.detail(
            notes: "Cat is friendly.",
            specialInstructions: "Use the eco products under the sink.",
            accessInstructions: "Key box by the gate, code 4417."
        )

        let plain = OrderInstructions.plainBlocks(order).map(\.text)
        XCTAssertEqual(plain, ["Use the eco products under the sink.", "Cat is friendly."])
        XCTAssertEqual(OrderInstructions.secret(order), "Key box by the gate, code 4417.")
    }

    func testBlankAccessInstructionsProduceNoSecret() {
        let order = OrderFixtures.detail(accessInstructions: "   \n ")
        XCTAssertNil(OrderInstructions.secret(order))
    }

    func testACardWithNothingButAccessInstructionsStillRenders() {
        let order = OrderFixtures.detail(accessInstructions: "Alarm code 9911.")
        XCTAssertTrue(OrderInstructions.plainBlocks(order).isEmpty)
        XCTAssertTrue(OrderInstructions.hasAnything(order))
    }

    func testAnEmptyOrderHasNoInstructionsCard() {
        XCTAssertFalse(OrderInstructions.hasAnything(OrderFixtures.detail()))
    }

    // MARK: - Fixtures

    private func orderAt(latitude: Double?, longitude: Double?, status: Int) -> CustomerOrderDetail {
        OrderFixtures.detail(
            statusCode: Code(value: status),
            address: OrderAddress(street: "Vinohradská 12", latitude: latitude, longitude: longitude)
        )
    }

    private func orderAt(latitude: Double?, longitude: Double?, status: OrderStatus) -> CustomerOrderDetail {
        orderAt(latitude: latitude, longitude: longitude, status: status.rawValue)
    }
}
