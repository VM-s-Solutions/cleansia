import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

/// The deployed backend binds `format: date` fields as strict `DateOnly` — a full
/// ISO date-time on the wire 400s the whole save. These tests pin the JSON the
/// generated client actually produces/accepts for `birthDate` ("yyyy-MM-dd",
/// Android wire parity).
final class BirthDateWireFormatTests: XCTestCase {
    private func makeBirthDate() throws -> Date {
        try XCTUnwrap(Calendar.current.date(from: DateComponents(year: 1990, month: 5, day: 1, hour: 12)))
    }

    private func makeCommand(birthDate: Date?) -> UpdateCurrentUserCommand {
        UpdateCurrentUserCommand(
            ProfileUpdate(
                id: "user-42",
                firstName: "Grace",
                lastName: "Hopper",
                phoneNumber: "+420999",
                birthDate: birthDate,
                languageCode: "cs"
            )
        )
    }

    func testUpdateCommandEncodesBirthDateAsDateOnlyString() throws {
        let command = try makeCommand(birthDate: makeBirthDate())

        let body = try CodableHelper.jsonEncoder.encode(command)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])

        XCTAssertEqual(object["birthDate"] as? String, "1990-05-01")
    }

    func testMyProfileDtoDecodesDateOnlyBirthDate() throws {
        let json = Data(#"{"email":"jane@example.com","birthDate":"1990-05-01"}"#.utf8)

        let dto = try CodableHelper.jsonDecoder.decode(MyProfileDto.self, from: json)

        let day = try XCTUnwrap(dto.birthDate?.wrappedDate)
        var utc = Calendar(identifier: .iso8601)
        utc.timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let components = utc.dateComponents([.year, .month, .day], from: day)
        XCTAssertEqual(components.year, 1990)
        XCTAssertEqual(components.month, 5)
        XCTAssertEqual(components.day, 1)
    }

    func testBirthDateRoundTripsThroughEncodeAndDecode() throws {
        let command = try makeCommand(birthDate: makeBirthDate())

        let body = try CodableHelper.jsonEncoder.encode(command)
        let decoded = try CodableHelper.jsonDecoder.decode(UpdateCurrentUserCommand.self, from: body)

        XCTAssertEqual(decoded.birthDate, command.birthDate)
    }
}
