import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// The cleaner's USER row — the login identity — as opposed to `PartnerProfileClient`, which owns the
/// employee record. `PreferredLanguageCode` lives here, and it is the only input to the language
/// `PayPeriodBackgroundService` renders the period-closed mail and the payout invoice PDF in.
///
/// So is the avatar: `EmployeeItem.profilePhoto` exists on the employee record but its `blobUrl` is
/// never signed on this host, so `MyProfileDto` is the only place a stored photo can be read from.
struct CurrentUser: Equatable {
    let email: String?
    let firstName: String?
    let lastName: String?
    let phoneNumber: String?
    let birthDate: Date?
    let preferredLanguageCode: String?
    let profilePhoto: ProfilePhoto?
}

/// A partial save: absent optional fields mean "nothing to say", never "delete it". First and last name
/// are the exception — the handler replaces them outright — so every caller replays them.
///
/// `phoneNumber` is deliberately non-optional. The command's `PhoneNumber` is a non-nullable reference
/// type on a host with nullable reference types enabled, so an omitted member is refused by the model
/// binder before the handler runs; a blank string is what "nothing to say" looks like there, and the
/// handler keeps the stored number when one arrives.
///
/// The avatar is the three-way choice `AvatarEdit` names, flattened onto the two members the command
/// carries. Both defaults say nothing, so a save about anything else leaves a stored photo alone by
/// writing nothing about it.
struct CurrentUserUpdate: Equatable {
    let firstName: String
    let lastName: String
    let phoneNumber: String
    let birthDate: Date?
    let languageCode: String?
    var photo: ProfilePhotoUpload?
    var removePhoto = false
}

protocol PartnerUserClient: AnyObject {
    func getCurrentUser() async -> ApiResult<CurrentUser>
    func updateCurrentUser(_ update: CurrentUserUpdate) async -> ApiResult<Void>
}

final class LivePartnerUserClient: PartnerUserClient {
    func getCurrentUser() async -> ApiResult<CurrentUser> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerUserAPI.userGetCurrentUser().toDomain()
        }
    }

    func updateCurrentUser(_ update: CurrentUserUpdate) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            // No id: the row written is always the JWT caller's and the command's id is inert.
            _ = try await PartnerUserAPI.userUpdateCurrentUser(updateCurrentUserCommand: UpdateCurrentUserCommand(
                firstName: update.firstName,
                lastName: update.lastName,
                phoneNumber: update.phoneNumber,
                birthDate: OpenAPIDateWithoutTime(day: update.birthDate),
                photo: update.photo.map(BlobFileDto.init),
                languageCode: update.languageCode,
                removePhoto: update.removePhoto
            ))
        }
    }
}

private extension BlobFileDto {
    init(_ upload: ProfilePhotoUpload) {
        self.init(
            fileName: upload.fileName,
            base64Content: upload.base64,
            contentType: upload.contentType
        )
    }

    func toDomain() -> ProfilePhoto? {
        guard let fileName = fileName?.trimmedOrNil else { return nil }
        return ProfilePhoto(fileName: fileName, blobURL: blobUrl?.trimmedOrNil.flatMap(URL.init(string:)))
    }
}

private extension MyProfileDto {
    func toDomain() -> CurrentUser {
        CurrentUser(
            email: email,
            firstName: firstName,
            lastName: lastName,
            phoneNumber: phoneNumber,
            birthDate: birthDate?.wrappedDate,
            preferredLanguageCode: preferredLanguageCode,
            profilePhoto: profilePhoto?.toDomain()
        )
    }
}
