import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The sheet-edge puck's status → art rule, against Android's `FloatingMascot.mascotForStatus`.
final class OrderDetailMascotArtTests: XCTestCase {
    func testNewAndPendingLean() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._0), .still(.leaning))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._1), .still(.leaning))
    }

    func testConfirmedWaves() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._2), .still(.waving))
    }

    func testOnTheWayCarriesTheKit() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._3), .still(.sprayAndCloth))
    }

    func testCompletedGivesAThumbsUp() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._5), .still(.thumbsUp))
    }

    func testAnUnknownStatusFallsBackToTheThumbsUp() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: nil), .still(.thumbsUp))
    }

    /// The rule the whole affordance rests on, and the one a silent static fallback would hide:
    /// InProgress is the ONLY status that animates.
    func testOnlyInProgressAnimates() {
        XCTAssertEqual(
            OrderDetailMascotArt.art(for: ._4),
            .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        )
        for status in OrderStatus.allCases where status != ._4 {
            guard let art = OrderDetailMascotArt.art(for: status) else { continue }
            switch art {
            case .animated: XCTFail("\(status) must not animate")
            case .still: break
            }
        }
    }

    func testCancelledShowsNoMascot() {
        XCTAssertNil(OrderDetailMascotArt.art(for: ._6))
    }

    func testEveryOtherStatusShowsOne() {
        for status in OrderStatus.allCases where status != ._6 {
            XCTAssertNotNil(OrderDetailMascotArt.art(for: status), "\(status) has no mascot")
        }
    }
}
