import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class JobRadiusWireTests: XCTestCase {
    private func encoded(_ command: UpdateJobRadiusCommand) throws -> [String: Any] {
        let data = try JSONEncoder().encode(command)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    }

    /// Omitting the field would also clear the radius, but only an explicit null makes "give me the
    /// country-wide board back" readable on the wire — and rules out the 0 the slider must never send.
    func testClearingTheRadiusPutsAnExplicitNullOnTheWire() throws {
        let body = try encoded(UpdateJobRadiusCommand(employeeId: "emp-1", radiusKm: nil))

        XCTAssertTrue(body.keys.contains("radiusKm"))
        XCTAssertTrue(body["radiusKm"] is NSNull)
    }

    func testASetRadiusTravelsAsANumber() throws {
        let body = try encoded(UpdateJobRadiusCommand(employeeId: "emp-1", radiusKm: 42))

        XCTAssertEqual(body["radiusKm"] as? Int, 42)
        XCTAssertEqual(body["employeeId"] as? String, "emp-1")
    }

    /// The generated `EmployeeItem` predates the field, so the profile read decodes it beside the
    /// generated model rather than through it.
    func testTheEmployeeReadModelCarriesTheRadiusAlongsideTheGeneratedFields() throws {
        let json = Data(#"{"id":"emp-1","firstName":"Jana","jobRadiusKm":30}"#.utf8)

        let profile = try JSONDecoder().decode(EmployeeProfile.self, from: json)

        XCTAssertEqual(profile.employee.id, "emp-1")
        XCTAssertEqual(profile.employee.firstName, "Jana")
        XCTAssertEqual(profile.jobRadiusKm, 30)
    }

    func testAnAbsentRadiusDecodesAsTheAnywhereChoice() throws {
        let json = Data(#"{"id":"emp-1"}"#.utf8)

        let profile = try JSONDecoder().decode(EmployeeProfile.self, from: json)

        XCTAssertNil(profile.jobRadiusKm)
        XCTAssertEqual(JobRadiusSelection(radiusKm: profile.jobRadiusKm), .anywhere)
    }

    /// The path is the one thing this adapter states that the generated client cannot check for it.
    func testTheUpdatePathIsTheMobilePartnerEndpoint() {
        XCTAssertEqual(PartnerEmployeeEndpoints.updateJobRadiusPath, "/api/Employee/UpdateJobRadius")
    }
}
