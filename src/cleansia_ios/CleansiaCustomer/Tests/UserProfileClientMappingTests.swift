import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

final class UserProfileClientMappingTests: XCTestCase {
    func testUpdateCommandCarriesIdAndBirthDate() {
        let birthDate = Date(timeIntervalSince1970: 641_520_000)
        let update = ProfileUpdate(
            id: "user-42",
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: "+420999",
            birthDate: birthDate,
            languageCode: "cs"
        )

        let command = UpdateCurrentUserCommand(update)

        XCTAssertEqual(command.id, "user-42")
        XCTAssertEqual(command.birthDate, OpenAPIDateWithoutTime(wrappedDate: birthDate))
        XCTAssertEqual(command.firstName, "Grace")
        XCTAssertEqual(command.lastName, "Hopper")
        XCTAssertEqual(command.phoneNumber, "+420999")
        XCTAssertEqual(command.languageCode, "cs")
    }

    func testUpdateCommandBlanksPhoneToNil() {
        let update = ProfileUpdate(
            id: "user-1",
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: "  ",
            birthDate: nil,
            languageCode: nil
        )

        let command = UpdateCurrentUserCommand(update)

        XCTAssertNil(command.phoneNumber)
        XCTAssertNil(command.birthDate)
    }
}
