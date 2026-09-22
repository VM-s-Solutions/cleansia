import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The acceptance carries no name and the crew entry carries no acceptance; the line the customer
/// reads is the pairing of the two by the seat id, and every way the pairing can fail must drop the
/// line rather than name the wrong cleaner or a cleaner for nothing.
final class WorkContractAcceptanceLineTests: XCTestCase {
    private let acceptedOn = Date(timeIntervalSince1970: 1_786_000_000)

    private func seat(_ id: String, name: String?) -> AssignedEmployeeDto {
        AssignedEmployeeDto(id: id, employeeId: "emp-\(id)", fullName: name, phoneNumber: nil)
    }

    private func acceptance(
        _ id: String?,
        seat seatId: String,
        acceptedOn: Date? = Date(timeIntervalSince1970: 1_786_000_000),
        version: String? = "2026-09-20"
    ) -> WorkContractAcceptanceDto {
        WorkContractAcceptanceDto(
            id: id,
            orderEmployeeId: seatId,
            employeeId: "emp-\(seatId)",
            acceptedOn: acceptedOn,
            documentVersion: version,
            language: "cs"
        )
    }

    private func order(
        crew: [AssignedEmployeeDto],
        acceptances: [WorkContractAcceptanceDto]?
    ) throws -> CustomerOrderDetail {
        var item = OrderItem.wireComplete()
        item.assignedEmployees = crew
        item.workContractAcceptances = acceptances
        return try CustomerOrderDetail(item)
    }

    func testOneAcceptanceOnOneSeatIsOneLineNamingThatSeatsCleaner() throws {
        let lines = try order(
            crew: [seat("seat-1", name: "Jana")],
            acceptances: [acceptance("acc-1", seat: "seat-1")]
        ).workContractAcceptanceLines()

        XCTAssertEqual(
            lines,
            [WorkContractAcceptanceLine(
                id: "acc-1",
                cleanerName: "Jana",
                acceptedOn: acceptedOn,
                documentVersion: "2026-09-20"
            )]
        )
    }

    func testTwoCrewMembersEachWithAnAcceptanceAreTwoLinesInTheServersOrder() throws {
        let lines = try order(
            crew: [seat("seat-1", name: "Jana"), seat("seat-2", name: "Petr")],
            acceptances: [acceptance("acc-2", seat: "seat-2"), acceptance("acc-1", seat: "seat-1")]
        ).workContractAcceptanceLines()

        XCTAssertEqual(lines.map(\.id), ["acc-2", "acc-1"])
        XCTAssertEqual(lines.map(\.cleanerName), ["Petr", "Jana"])
    }

    func testNoAcceptanceIsNoLineEvenWithACrewOnTheJob() throws {
        let crew = [seat("seat-1", name: "Jana")]

        XCTAssertTrue(try order(crew: crew, acceptances: nil).workContractAcceptanceLines().isEmpty)
        XCTAssertTrue(try order(crew: crew, acceptances: []).workContractAcceptanceLines().isEmpty)
    }

    func testAnAcceptanceNamingNoCurrentSeatIsDroppedRatherThanShownNameless() throws {
        let lines = try order(
            crew: [seat("seat-1", name: "Jana")],
            acceptances: [acceptance("acc-9", seat: "seat-gone"), acceptance("acc-1", seat: "seat-1")]
        ).workContractAcceptanceLines()

        XCTAssertEqual(lines.map(\.id), ["acc-1"])
    }

    func testAnAcceptanceWithNoIdOrNoInstantIsDroppedRatherThanRenderedUnreadable() throws {
        let lines = try order(
            crew: [seat("seat-1", name: "Jana"), seat("seat-2", name: "Petr")],
            acceptances: [
                acceptance(nil, seat: "seat-1"),
                acceptance("acc-2", seat: "seat-2", acceptedOn: nil)
            ]
        ).workContractAcceptanceLines()

        XCTAssertTrue(lines.isEmpty)
    }

    func testABlankCrewNameAndAMissingVersionReadAsAbsentNotAsText() throws {
        let lines = try order(
            crew: [seat("seat-1", name: "  ")],
            acceptances: [acceptance("acc-1", seat: "seat-1", version: nil)]
        ).workContractAcceptanceLines()

        XCTAssertEqual(lines.count, 1)
        XCTAssertNil(lines.first?.cleanerName)
        XCTAssertEqual(lines.first?.documentVersion, "")
    }
}
