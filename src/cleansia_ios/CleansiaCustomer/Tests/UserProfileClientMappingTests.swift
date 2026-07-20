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

    func testMyProfileMapsStatsIntoTheDomainProfile() {
        let memberSince = Date(timeIntervalSince1970: 1_739_534_400)
        let dto = MyProfileDto(
            email: "jane@example.com",
            firstName: "Jane",
            lastName: "Doe",
            memberSince: memberSince,
            totalBookings: 7,
            totalSavings: 320,
            savingsCurrencyCode: "CZK"
        )

        let user = dto.toDomain(id: "user-1")

        XCTAssertEqual(user.memberSince, memberSince)
        XCTAssertEqual(user.totalBookings, 7)
        XCTAssertEqual(user.totalSavings, 320)
        XCTAssertEqual(user.savingsCurrencyCode, "CZK")
    }

    func testMyProfileDefaultsAbsentStatsToZeroAndNil() {
        let dto = MyProfileDto(email: "jane@example.com")

        let user = dto.toDomain(id: "user-1")

        XCTAssertNil(user.memberSince)
        XCTAssertEqual(user.totalBookings, 0)
        XCTAssertEqual(user.totalSavings, 0)
        XCTAssertNil(user.savingsCurrencyCode)
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
