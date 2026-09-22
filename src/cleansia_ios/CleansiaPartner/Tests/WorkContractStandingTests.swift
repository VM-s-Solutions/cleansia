import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The pairing the detail renders from: the caller's acceptance is the row whose seat id is their own
/// crew entry's id. A crew entry with no row is the pending state — the only way onto a crew without
/// accepting is an administrator's placement — and it is offered only while the job is not over.
final class WorkContractStandingTests: XCTestCase {
    private let acceptedOn = Date(timeIntervalSince1970: 1_786_000_000)

    private func order(
        status: Int = 2,
        seats: [(id: String, employeeId: String)] = [("seat-1", "emp-me")],
        acceptances: [(id: String, seatId: String)] = []
    ) throws -> OrderDetail {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.assignedEmployees = seats.map { AssignedEmployeeDto(id: $0.id, employeeId: $0.employeeId) }
        item.workContractAcceptances = acceptances.map {
            WorkContractAcceptanceDto(
                id: $0.id,
                orderEmployeeId: $0.seatId,
                employeeId: nil,
                acceptedOn: acceptedOn,
                documentVersion: "2026-09-20",
                language: "cs"
            )
        }
        return try OrderDetail(item)
    }

    func testARowOnMyOwnSeatIsAccepted() throws {
        let detail = try order(acceptances: [("acc-1", "seat-1")])

        XCTAssertEqual(
            detail.workContractStanding(myEmployeeId: "emp-me"),
            .accepted(acceptanceId: "acc-1", acceptedOn: acceptedOn, documentVersion: "2026-09-20")
        )
    }

    func testMySeatWithNoRowIsPendingWhileTheJobIsNotOver() throws {
        for status in [0, 2, 3, 4] {
            XCTAssertEqual(
                try order(status: status).workContractStanding(myEmployeeId: "emp-me"),
                .pending,
                "status \(status) admits the standalone acceptance"
            )
        }
    }

    func testMySeatWithNoRowOnAFinishedJobIsNothing() throws {
        for status in [5, 6] {
            XCTAssertEqual(
                try order(status: status).workContractStanding(myEmployeeId: "emp-me"),
                .none,
                "status \(status) is over; there is nothing left to accept"
            )
        }
    }

    /// The server sends the rows without names, so a crew mate's row must never read as mine.
    func testACrewMatesRowIsNotMine() throws {
        let detail = try order(
            seats: [("seat-1", "emp-me"), ("seat-2", "emp-other")],
            acceptances: [("acc-2", "seat-2")]
        )

        XCTAssertEqual(detail.workContractStanding(myEmployeeId: "emp-me"), .pending)
        XCTAssertEqual(
            detail.workContractStanding(myEmployeeId: "emp-other"),
            .accepted(acceptanceId: "acc-2", acceptedOn: acceptedOn, documentVersion: "2026-09-20")
        )
    }

    func testNotOnTheCrewIsNothingEvenWhenRowsExist() throws {
        let detail = try order(acceptances: [("acc-1", "seat-1")])

        XCTAssertEqual(detail.workContractStanding(myEmployeeId: "emp-stranger"), .none)
        XCTAssertEqual(detail.workContractStanding(myEmployeeId: nil), .none)
    }

    /// A re-take is a new seat; the old seat's row stays on the server but never reaches this DTO, and a
    /// row naming a seat that is not on the crew pairs with nobody.
    func testARowOnASeatNotOnTheCrewPairsWithNobody() throws {
        let detail = try order(acceptances: [("acc-old", "seat-gone")])

        XCTAssertEqual(detail.workContractStanding(myEmployeeId: "emp-me"), .pending)
    }

    // MARK: the mapper — drop the row, never claim

    func testASeatWithoutBothIdsIsDroppedNotRefused() throws {
        var item = OrderItem.wireComplete()
        item.assignedEmployees = [
            AssignedEmployeeDto(id: "seat-1", employeeId: "emp-1"),
            AssignedEmployeeDto(id: nil, employeeId: "emp-2"),
            AssignedEmployeeDto(id: "seat-3", employeeId: " ")
        ]

        XCTAssertEqual(try OrderDetail(item).seats, [OrderSeat(id: "seat-1", employeeId: "emp-1")])
    }

    /// The version is refused blank on the sheet's mapper; here the row is dropped for the same reason,
    /// so the line never reads "version " over nothing.
    func testAnAcceptanceWithoutItsIdSeatInstantOrVersionIsDropped() throws {
        var item = OrderItem.wireComplete()
        item.workContractAcceptances = [
            acceptance(id: "acc-1", seatId: "seat-1", acceptedOn: acceptedOn, version: "2026-09-20"),
            acceptance(id: nil, seatId: "seat-2", acceptedOn: acceptedOn, version: "2026-09-20"),
            acceptance(id: "acc-3", seatId: nil, acceptedOn: acceptedOn, version: "2026-09-20"),
            acceptance(id: "acc-4", seatId: "seat-4", acceptedOn: nil, version: "2026-09-20"),
            acceptance(id: "acc-5", seatId: "seat-5", acceptedOn: acceptedOn, version: nil),
            acceptance(id: "acc-6", seatId: "seat-6", acceptedOn: acceptedOn, version: " ")
        ]

        let rows = try OrderDetail(item).workContractAcceptances

        XCTAssertEqual(rows.map(\.id), ["acc-1"])
        XCTAssertEqual(rows.first?.documentVersion, "2026-09-20")
    }

    private func acceptance(
        id: String?,
        seatId: String?,
        acceptedOn: Date?,
        version: String?
    ) -> WorkContractAcceptanceDto {
        WorkContractAcceptanceDto(id: id, orderEmployeeId: seatId, acceptedOn: acceptedOn, documentVersion: version)
    }

    func testAbsentListsMapToEmptyOnes() throws {
        let detail = try OrderDetail(OrderItem.wireComplete())

        XCTAssertTrue(detail.seats.isEmpty)
        XCTAssertTrue(detail.workContractAcceptances.isEmpty)
    }
}
