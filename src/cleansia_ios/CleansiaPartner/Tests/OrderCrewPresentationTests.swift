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

    // MARK: - The line the scope card renders

    func testCrewLineNamesTheCrewThenTheOpenSeats() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        L10n.bundle = try localeBundle("en")

        XCTAssertEqual(
            OrdersFormat.crewLine(.spotsOpen(crewSize: 2, openSpots: 1)),
            "2 cleaners needed · 1 spot open"
        )
        XCTAssertEqual(OrdersFormat.crewLine(.full(crewSize: 2)), "2 cleaners needed · No spots left")
    }

    /// Through the BUILT bundle, not the `.xcstrings` source: a key present in the catalog but
    /// absent from a shipped `.lproj` renders as its own name on screen.
    func testCrewLineResolvesInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in ["en", "cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            for crew in [OrderCrew.spotsOpen(crewSize: 2, openSpots: 1), .full(crewSize: 2)] {
                let rendered = OrdersFormat.crewLine(crew)
                XCTAssertTrue(rendered.contains(" · "), "\(language) dropped the separator: \(rendered)")
                XCTAssertTrue(rendered.contains("2"), "\(language) dropped the crew size: \(rendered)")
                for key in ["crew_size", "crew_spots_open", "crew_no_spots"] {
                    XCTAssertFalse(rendered.contains(key), "\(language) left \(key) unresolved: \(rendered)")
                }
            }
        }
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
