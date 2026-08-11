import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The stored avatar. `fileName` is the content-addressed blob name — the backend mints a fresh one
/// on every upload — so it is the image's identity and its cache key. `blobURL` is a SAS link that is
/// re-signed on every fetch and expires within the hour: it is a credential, never persisted and
/// never used to key anything.
///
/// The URL is optional because it is only how the image is fetched, never whether it exists: a fetch
/// that returns the name without a signature still describes a photo the account holds and can delete.
struct ProfilePhoto: Equatable {
    let fileName: String
    let blobURL: URL?
}

struct ProfilePhotoUpload: Equatable {
    let base64: String
    let contentType: String
    let fileName: String
}

struct CurrentUserProfile: Equatable {
    let id: String
    let email: String
    let firstName: String
    let lastName: String
    let phoneNumber: String?
    let birthDate: Date?
    let preferredLanguageCode: String?
    let isEmailConfirmed: Bool
    var profilePhoto: ProfilePhoto?
    // Profile hero stats. memberSince is the account creation date;
    // totalSavings is the realized money saved, in savingsCurrencyCode.
    var memberSince: Date?
    var totalBookings: Int = 0
    var totalSavings: Double = 0
    var savingsCurrencyCode: String?

    var isComplete: Bool {
        !firstName.isBlank && !lastName.isBlank && !email.isBlank && !(phoneNumber ?? "").isBlank
    }

    var fullName: String {
        [firstName, lastName].filter { !$0.isBlank }.joined(separator: " ")
    }
}

struct ProfileUpdate: Equatable {
    let id: String
    let firstName: String
    let lastName: String
    let phoneNumber: String?
    let birthDate: Date?
    let languageCode: String?
    var photo: ProfilePhotoUpload?
    var removePhoto = false
}

protocol UserProfileClient: AnyObject {
    func currentUser() async -> ApiResult<CurrentUserProfile>
    func updateCurrentUser(_ update: ProfileUpdate) async -> ApiResult<Void>
}

final class LiveUserProfileClient: UserProfileClient {
    private let tokenStore: TokenStore

    init(tokenStore: TokenStore) {
        self.tokenStore = tokenStore
    }

    func currentUser() async -> ApiResult<CurrentUserProfile> {
        // The profile response carries no user id — it lives in the JWT sub
        // claim (Android UserRepository parity), pulled once per fetch.
        guard let accessToken = tokenStore.current()?.accessToken,
              let userId = JwtDecoder.userId(of: accessToken)
        else {
            return .failure(ApiError())
        }
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerUserAPI.userGetCurrentUser().toDomain(id: userId)
        }
    }

    func updateCurrentUser(_ update: ProfileUpdate) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerUserAPI.userUpdateCurrentUser(
                updateCurrentUserCommand: UpdateCurrentUserCommand(update)
            )
        }
    }
}

extension UpdateCurrentUserCommand {
    init(_ update: ProfileUpdate) {
        self.init(
            id: update.id,
            firstName: update.firstName,
            lastName: update.lastName,
            phoneNumber: update.phoneNumber?.nilIfBlank,
            birthDate: OpenAPIDateWithoutTime(day: update.birthDate),
            photo: update.photo.map(BlobFileDto.init),
            languageCode: update.languageCode,
            removePhoto: update.removePhoto
        )
    }
}

extension BlobFileDto {
    init(_ upload: ProfilePhotoUpload) {
        self.init(
            fileName: upload.fileName,
            base64Content: upload.base64,
            contentType: upload.contentType
        )
    }
}

/// **Refuse.** The identity fields feed the edit form, which posts them back, so a coerced `""`
/// does not render as an empty label — it is what overwrites the name on the account the next time
/// the customer saves. `totalSavings` is money the hero states outright, and `isEmailConfirmed` is a
/// verification claim that `false` makes for the server.
///
/// `phoneNumber`, `birthDate`, `savingsCurrencyCode` and the photo are nullable by design and stay
/// so — every one of them has a rendered absence.
extension MyProfileDto {
    func toDomain(id: String) throws -> CurrentUserProfile {
        try CurrentUserProfile(
            id: id,
            email: email.requireNonBlank("email"),
            firstName: firstName.requireNonBlank("firstName"),
            lastName: lastName.requireNonBlank("lastName"),
            phoneNumber: phoneNumber,
            birthDate: birthDate?.wrappedDate,
            preferredLanguageCode: preferredLanguageCode,
            isEmailConfirmed: isEmailConfirmed.require("isEmailConfirmed"),
            profilePhoto: profilePhoto?.toDomain(),
            memberSince: memberSince,
            totalBookings: totalBookings.require("totalBookings"),
            totalSavings: totalSavings.require("totalSavings"),
            savingsCurrencyCode: savingsCurrencyCode
        )
    }
}

extension BlobFileDto {
    func toDomain() -> ProfilePhoto? {
        guard let fileName = fileName?.nilIfBlank else { return nil }
        return ProfilePhoto(fileName: fileName, blobURL: blobUrl?.nilIfBlank.flatMap(URL.init(string:)))
    }
}

private extension String {
    var nilIfBlank: String? {
        isBlank ? nil : self
    }
}
