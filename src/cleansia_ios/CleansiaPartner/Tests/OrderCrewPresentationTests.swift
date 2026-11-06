import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// `TakeOrder`'s free-seat conjunct refuses a take the detail gave no warning about, and neither
/// the crew size nor the seat count is derivable client-side.
///
/// Every fixture here is arranged **multi-seat and partially crewed** and every expectation is a
/// literal: four of the five wire members are numeric and the fifth is a boolean, so an
/// unpopulated fixture yields exactly the `0`/`false` a lazy assertion is satisfied by.
final class OrderCrewPresentationTests: XCTestCase {
    /// 2 required, 2 max, 1 assigned ⇒ 1 available.
    private var partiallyCrewed: OrderItem {
        var item = OrderItem.wireComplete()
        item.requiredEmployees = 2
        item.maxEmployees = 2
        item.assignedEmployeesCount = 1
        item.availableSpots = 1
        item.hasAvailableSpots = true
        return item
    }

    private var fullyCrewed: OrderItem {
        var item = partiallyCrewed
        item.assignedEmployeesCount = 2
        item.availableSpots = 0
        item.hasAvailableSpots = false
        return item
    }

    func testPartiallyCrewedOrderNamesTheCrewAndTheOpenSeat() throws {
        let crew = try OrderCrew(partiallyCrewed)
        XCTAssertEqual(crew, .spotsOpen(crewSize: 2, openSpots: 1))
        XCTAssertEqual(crew?.crewSize, 2)
    }

    func testFullCrewWarnsThatNoSeatIsLeft() throws {
        let crew = try OrderCrew(fullyCrewed)
        XCTAssertEqual(crew, .full(crewSize: 2))
        XCTAssertEqual(crew?.crewSize, 2)
    }

    /// The server owns the seat arithmetic and sends both shapes; the client reads them and
    /// computes neither. When they disagree the flag — the same one behind the take's refusal —
    /// decides.
    func testServerFlagDecidesNotTheCount() throws {
        var flagCleared = partiallyCrewed
        flagCleared.hasAvailableSpots = false
        XCTAssertEqual(try OrderCrew(flagCleared), .full(crewSize: 2))

        var countCleared = partiallyCrewed
        countCleared.availableSpots = 0
        XCTAssertEqual(try OrderCrew(countCleared), .full(crewSize: 2))
    }

    func testSoloJobIsStillReported() throws {
        var item = OrderItem.wireComplete()
        item.requiredEmployees = 1
        item.maxEmployees = 1
        item.assignedEmployeesCount = 1
        item.availableSpots = 0
        item.hasAvailableSpots = false
        XCTAssertEqual(try OrderCrew(item), .full(crewSize: 1))
    }

    /// A build talking to a server that predates the seat block says nothing.
    func testAbsentCrewSizeRendersNoSeatFacts() throws {
        var item = OrderItem.wireComplete()
        item.availableSpots = 1
        item.hasAvailableSpots = true
        XCTAssertNil(try OrderCrew(item))

        item.requiredEmployees = 0
        XCTAssertNil(try OrderCrew(item))
    }

    // MARK: - What the screen reads

    func testDetailCarriesTheSeatFacts() throws {
        XCTAssertEqual(try OrderDetail(partiallyCrewed).crew, .spotsOpen(crewSize: 2, openSpots: 1))
        XCTAssertEqual(try OrderDetail(fullyCrewed).crew, .full(crewSize: 2))
        XCTAssertNil(try OrderDetail(.wireComplete()).crew)
    }
}
