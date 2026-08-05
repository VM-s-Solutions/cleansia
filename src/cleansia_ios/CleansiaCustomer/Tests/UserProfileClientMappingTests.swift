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

    func testMyProfileMapsTheStoredPhotoNameAndSignedUrl() {
        let dto = MyProfileDto(
            email: "jane@example.com",
            profilePhoto: BlobFileDto(fileName: "blob-1", blobUrl: "https://blobs.example/blob-1?sig=abc")
        )

        let photo = dto.toDomain(id: "user-1").profilePhoto

        XCTAssertEqual(photo?.fileName, "blob-1")
        XCTAssertEqual(photo?.blobURL?.absoluteString, "https://blobs.example/blob-1?sig=abc")
    }

    /// The name is the photo's identity and the URL is only how to fetch it, so a stored photo whose
    /// SAS came back blank is still a stored photo — it simply cannot be drawn this fetch. Dropping it
    /// here would tell the profile there is nothing to delete.
    func testMyProfileKeepsAStoredPhotoWhoseSignedUrlIsMissing() {
        let dto = MyProfileDto(email: "a@b.c", profilePhoto: BlobFileDto(fileName: "blob-1"))

        let photo = dto.toDomain(id: "user-1").profilePhoto

        XCTAssertEqual(photo?.fileName, "blob-1")
        XCTAssertNil(photo?.blobURL)
    }

    func testMyProfileHasNoPhotoWithoutAName() {
        let noName = MyProfileDto(email: "a@b.c", profilePhoto: BlobFileDto(blobUrl: "https://blobs.example/x"))
        let blank = MyProfileDto(email: "a@b.c", profilePhoto: BlobFileDto(fileName: " ", blobUrl: " "))

        XCTAssertNil(noName.toDomain(id: "user-1").profilePhoto)
        XCTAssertNil(blank.toDomain(id: "user-1").profilePhoto)
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
