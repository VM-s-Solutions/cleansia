import XCTest
@testable import CleansiaCore

/// The waving figure leans on a ledge drawn at 0.797 of its art. The puck scales that art by 1.009 about its centre,
/// so in the puck's square frame the ledge runs at 0.5 + 0.297 × 1.009 of the height.
final class MascotPuckLedgeTests: XCTestCase {
    func testTheWavingFiguresLedgeIsWhereThePuckDrawsIt() throws {
        let ledge = try XCTUnwrap(MascotPuck.ledgeFraction(.still(.waving)))

        XCTAssertEqual(ledge, 0.5 + (0.797 - 0.5) * 1.009, accuracy: 0.0001)
    }

    func testEveryOtherArtHasNoLedge() {
        XCTAssertNil(MascotPuck.ledgeFraction(nil))
        for mascot in Mascot.allCases where mascot != .waving {
            XCTAssertNil(MascotPuck.ledgeFraction(.still(mascot)), "\(mascot)")
        }
        XCTAssertNil(MascotPuck.ledgeFraction(.animated(.welcoming, loop: true, fallback: .waving)))
    }
}
