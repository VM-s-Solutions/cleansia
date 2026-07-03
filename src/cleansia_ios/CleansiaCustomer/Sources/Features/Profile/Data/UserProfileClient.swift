import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct CurrentUserProfile: Equatable {
    let id: String
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
    let id: String
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
            birthDate: update.birthDate,
            languageCode: update.languageCode
        )
    }
}

private extension MyProfileDto {
    func toDomain(id: String) -> CurrentUserProfile {
        CurrentUserProfile(
            id: id,
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
