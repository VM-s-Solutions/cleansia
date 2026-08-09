import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Asserts the bytes the production client puts on the wire, not a re-encoding of the command object.
/// The generated `UpdateJobRadiusCommand` encodes with `encodeIfPresent`, so clearing the radius OMITS
/// `radiusKm` rather than sending an explicit null. `UpdateJobRadius.Command.RadiusKm` is `int?` and
/// nothing marks it required, so the model binder resolves an absent member to null and the handler
/// calls `SetJobRadius(null)` either way — the two bodies clear the radius identically. What the
/// endpoint refuses is `0`: `JobProximity.MinRadiusKm` is 1, so "no limit" must never travel as a
/// number.
@MainActor
final class JobRadiusWireTests: XCTestCase {
    private var bodies: WireBodies!

    override func setUp() {
        super.setUp()
        bodies = WireBodies()
        GeneratedWireSpine.install(recording: bodies) { _ in
            (200, Data(#"{"employeeId":"emp-1","radiusKm":null}"#.utf8))
        }
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        bodies = nil
        super.tearDown()
    }

    @discardableResult
    private func save(_ radiusKm: Int?) async -> ApiResult<Int?> {
        await LivePartnerProfileClient().updateJobRadius(
            UpdateJobRadiusCommand(employeeId: "emp-1", radiusKm: radiusKm)
        )
    }

    func testASetRadiusTravelsAsANumberForTheSessionEmployee() async throws {
        await save(42)

        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertEqual(body["radiusKm"] as? Int, 42)
        XCTAssertEqual(body["employeeId"] as? String, "emp-1")
    }

    /// The one thing that would be a defect rather than a difference: a cleared radius arriving as a
    /// number. `0` is out of range and would earn a 400 instead of the country-wide board.
    func testClearingTheRadiusSendsNoRadiusValueAtAllRatherThanZero() async throws {
        await save(nil)

        let wire = try XCTUnwrap(bodies.text(ofPath: Self.updatePath))
        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertNil(body["radiusKm"], wire)
        XCTAssertEqual(body["employeeId"] as? String, "emp-1", wire)
    }

    func testTheUpdateIsAPutToTheMobilePartnerEndpoint() async {
        await save(42)

        XCTAssertEqual(bodies.paths, [Self.updatePath])
        XCTAssertEqual(bodies.method(ofPath: Self.updatePath), "PUT")
    }

    /// The response half of the same choice: the server answers a cleared radius with an explicit null,
    /// and the client has to read it as "no limit" rather than as a failed decode.
    func testAClearedRadiusComesBackAsTheAnywhereChoice() async throws {
        let result = try await save(nil).get()

        XCTAssertNil(result)
        XCTAssertEqual(JobRadiusSelection(radiusKm: result), .anywhere)
    }

    func testTheGeneratedEmployeeModelCarriesTheRadius() throws {
        let json = Data(#"{"id":"emp-1","firstName":"Jana","jobRadiusKm":30}"#.utf8)

        let employee = try JSONDecoder().decode(EmployeeItem.self, from: json)

        XCTAssertEqual(employee.id, "emp-1")
        XCTAssertEqual(employee.firstName, "Jana")
        XCTAssertEqual(employee.jobRadiusKm, 30)
    }

    func testAnAbsentRadiusDecodesAsTheAnywhereChoice() throws {
        let json = Data(#"{"id":"emp-1"}"#.utf8)

        let employee = try JSONDecoder().decode(EmployeeItem.self, from: json)

        XCTAssertNil(employee.jobRadiusKm)
        XCTAssertEqual(JobRadiusSelection(radiusKm: employee.jobRadiusKm), .anywhere)
    }

    private static let updatePath = "/api/Employee/UpdateJobRadius"
}
