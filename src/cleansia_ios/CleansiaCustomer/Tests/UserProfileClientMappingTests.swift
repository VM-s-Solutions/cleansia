import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

final class UserProfileClientMappingTests: XCTestCase {
    /// The birth date is asserted as the DAY that reaches the wire. It used to be compared against a
    /// second call to the same defaulted initializer, which cannot fail: `OpenAPIDateWithoutTime` stores
    /// `wrappedDate` untouched and its `==` reads only that, so the zone that decides the encoded day is
    /// invisible to equality and the assertion held for every possible zone, right and wrong alike.
    func testUpdateCommandCarriesIdAndTheBirthDayItself() {
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
        XCTAssertEqual(command.birthDate?.rawValue, "1990-05-01")
        XCTAssertEqual(command.firstName, "Grace")
        XCTAssertEqual(command.lastName, "Hopper")
        XCTAssertEqual(command.phoneNumber, "+420999")
        XCTAssertEqual(command.languageCode, "cs")
    }

    /// The customer twin of the partner characterisation: a day that arrived as midnight UTC must go
    /// back out as that day, not as the previous one, from a handset west of Greenwich. The instant is
    /// late in the UTC day because that is the case that separates reading the day in Greenwich from
    /// re-offsetting it — any positive device offset rolls a late instant into the next day.
    func testTheBirthDayIsReadInGreenwichSoItSurvivesEveryHandset() {
        let lateOnTheFourth = Date(timeIntervalSince1970: 400_030_200)
        let update = ProfileUpdate(
            id: "user-42",
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: nil,
            birthDate: lateOnTheFourth,
            languageCode: "cs"
        )

        let command = UpdateCurrentUserCommand(update)

        XCTAssertEqual(command.birthDate?.timezone.secondsFromGMT(), 0)
        XCTAssertEqual(command.birthDate?.rawValue, "1982-09-04")
    }

    func testMyProfileMapsStatsIntoTheDomainProfile() throws {
        let memberSince = Date(timeIntervalSince1970: 1_739_534_400)
        let dto = MyProfileDto.wireComplete(
            memberSince: memberSince,
            totalBookings: 7,
            totalSavings: 320,
            savingsCurrencyCode: "CZK"
        )

        let user = try dto.toDomain(id: "user-1")

        XCTAssertEqual(user.memberSince, memberSince)
        XCTAssertEqual(user.totalBookings, 7)
        XCTAssertEqual(user.totalSavings, 320)
        XCTAssertEqual(user.savingsCurrencyCode, "CZK")
    }

    /// A genuinely new account has no bookings and no savings, and the server says so with zeros.
    /// `memberSince` is `nullable: false` yet stays optional: the hero already renders its absence,
    /// so leaving it nil fabricates no join date.
    func testAnAccountWithNoHistoryYetStillMaps() throws {
        let dto = MyProfileDto.wireComplete()

        let user = try dto.toDomain(id: "user-1")

        XCTAssertNil(user.memberSince)
        XCTAssertEqual(user.totalBookings, 0)
        XCTAssertEqual(user.totalSavings, 0)
        XCTAssertNil(user.savingsCurrencyCode)
    }

    // MARK: - The avatar on the wire

    /// Asserted on the GENERATED command, not on `ProfileUpdate`: every generated property is optional
    /// with a nil default, so dropping a line from the mapper still compiles and a ViewModel test that
    /// stops at the app DTO stays green while the field never reaches the wire.
    func testUpdateCommandCarriesThePickedPhotoAndAsksForNoRemoval() {
        let update = ProfileUpdate(
            id: "user-1",
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: nil,
            birthDate: nil,
            languageCode: nil,
            photo: ProfilePhotoUpload(base64: "QUJD", contentType: "image/jpeg", fileName: "photo.jpg")
        )

        let command = UpdateCurrentUserCommand(update)

        XCTAssertEqual(command.photo?.base64Content, "QUJD")
        XCTAssertEqual(command.photo?.contentType, "image/jpeg")
        XCTAssertEqual(command.photo?.fileName, "photo.jpg")
        XCTAssertEqual(command.removePhoto, false)
    }

    func testUpdateCommandAsksForRemovalWithoutSendingAPhoto() {
        let update = ProfileUpdate(
            id: "user-1",
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: nil,
            birthDate: nil,
            languageCode: nil,
            removePhoto: true
        )

        let command = UpdateCurrentUserCommand(update)

        XCTAssertEqual(command.removePhoto, true)
        XCTAssertNil(command.photo)
    }

    /// The `fe0c985b` regression, at the wire: a save that never touched the avatar must carry no
    /// image AND an explicit `false`, so the stored photo survives an ordinary field edit.
    func testUpdateCommandLeavesTheStoredPhotoAloneWhenNothingWasTouched() {
        let update = ProfileUpdate(
            id: "user-1",
            firstName: "Grace",
            lastName: "Hopper",
            phoneNumber: nil,
            birthDate: nil,
            languageCode: nil
        )

        let command = UpdateCurrentUserCommand(update)

        XCTAssertNil(command.photo)
        XCTAssertEqual(command.removePhoto, false)
    }

    func testMyProfileMapsTheStoredPhotoNameAndSignedUrl() throws {
        let dto = MyProfileDto.wireComplete(
            profilePhoto: BlobFileDto(fileName: "blob-1", blobUrl: "https://blobs.example/blob-1?sig=abc")
        )

        let photo = try dto.toDomain(id: "user-1").profilePhoto

        XCTAssertEqual(photo?.fileName, "blob-1")
        XCTAssertEqual(photo?.blobURL?.absoluteString, "https://blobs.example/blob-1?sig=abc")
    }

    /// The name is the photo's identity and the URL is only how to fetch it, so a stored photo whose
    /// SAS came back blank is still a stored photo — it simply cannot be drawn this fetch. Dropping it
    /// here would tell the profile there is nothing to delete.
    func testMyProfileKeepsAStoredPhotoWhoseSignedUrlIsMissing() throws {
        let dto = MyProfileDto.wireComplete(profilePhoto: BlobFileDto(fileName: "blob-1"))

        let photo = try dto.toDomain(id: "user-1").profilePhoto

        XCTAssertEqual(photo?.fileName, "blob-1")
        XCTAssertNil(photo?.blobURL)
    }

    func testMyProfileHasNoPhotoWithoutAName() throws {
        let noName = MyProfileDto.wireComplete(profilePhoto: BlobFileDto(blobUrl: "https://blobs.example/x"))
        let blank = MyProfileDto.wireComplete(profilePhoto: BlobFileDto(fileName: " ", blobUrl: " "))

        XCTAssertNil(try noName.toDomain(id: "user-1").profilePhoto)
        XCTAssertNil(try blank.toDomain(id: "user-1").profilePhoto)
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

private extension MyProfileDto {
    /// A payload that satisfies the contract: every member the mapper refuses is populated, so a
    /// fixture cannot assert against something the app would refuse in production.
    static func wireComplete(
        email: String = "jane@example.com",
        profilePhoto: BlobFileDto? = nil,
        memberSince: Date? = nil,
        totalBookings: Int = 0,
        totalSavings: Double = 0,
        savingsCurrencyCode: String? = nil
    ) -> MyProfileDto {
        MyProfileDto(
            email: email,
            firstName: "Jane",
            lastName: "Doe",
            isEmailConfirmed: true,
            profilePhoto: profilePhoto,
            memberSince: memberSince,
            totalBookings: totalBookings,
            totalSavings: totalSavings,
            savingsCurrencyCode: savingsCurrencyCode
        )
    }
}
