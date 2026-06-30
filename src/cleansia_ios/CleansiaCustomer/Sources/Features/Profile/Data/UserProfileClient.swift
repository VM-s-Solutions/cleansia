import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct CurrentUserProfile: Equatable {
    let email: String
    let firstName: String
    let lastName: String
    let phoneNumber: String?
    let birthDate: Date?
    let preferredLanguageCode: String?
    let isEmailConfirmed: Bool

    var isComplete: Bool {
        !firstName.isBlank && !lastName.isBlank && !email.isBlank && !(phoneNumber ?? "").isBlank
    }

    var fullName: String {
        [firstName, lastName].filter { !$0.isBlank }.joined(separator: " ")
    }
}

struct ProfileUpdate: Equatable {
    let firstName: String
    let lastName: String
    let phoneNumber: String?
    let birthDate: Date?
    let languageCode: String?
}

protocol UserProfileClient: AnyObject {
    func currentUser() async -> ApiResult<CurrentUserProfile>
    func updateCurrentUser(_ update: ProfileUpdate) async -> ApiResult<Void>
}

final class LiveUserProfileClient: UserProfileClient {
    func currentUser() async -> ApiResult<CurrentUserProfile> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerUserAPI.userGetCurrentUser().toDomain()
        }
    }

    func updateCurrentUser(_ update: ProfileUpdate) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerUserAPI.userUpdateCurrentUser(
                updateCurrentUserCommand: UpdateCurrentUserCommand(
                    firstName: update.firstName,
                    lastName: update.lastName,
                    phoneNumber: update.phoneNumber?.nilIfBlank,
                    birthDate: update.birthDate,
                    languageCode: update.languageCode
                )
            )
        }
    }
}

private extension MyProfileDto {
    func toDomain() -> CurrentUserProfile {
        CurrentUserProfile(
            email: email ?? "",
            firstName: firstName ?? "",
            lastName: lastName ?? "",
            phoneNumber: phoneNumber,
            birthDate: birthDate,
            preferredLanguageCode: preferredLanguageCode,
            isEmailConfirmed: isEmailConfirmed ?? false
        )
    }
}

private extension String {
    var nilIfBlank: String? {
        isBlank ? nil : self
    }
}
