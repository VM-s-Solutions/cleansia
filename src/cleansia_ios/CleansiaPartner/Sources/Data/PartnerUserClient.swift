import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// The cleaner's USER row — the login identity — as opposed to `PartnerProfileClient`, which owns the
/// employee record. `PreferredLanguageCode` lives here, and it is the only input to the language
/// `PayPeriodBackgroundService` renders the period-closed mail and the payout invoice PDF in.
struct CurrentUser: Equatable {
    let email: String?
    let firstName: String?
    let lastName: String?
    let phoneNumber: String?
    let birthDate: Date?
    let preferredLanguageCode: String?
}

protocol PartnerUserClient: AnyObject {
    func getCurrentUser() async -> ApiResult<CurrentUser>

    /// A partial save: absent optional fields mean "nothing to say", never "delete it". First and last
    /// name are the exception — the handler replaces them outright — so every caller replays them.
    ///
    /// `phoneNumber` is deliberately non-optional. The command's `PhoneNumber` is a non-nullable
    /// reference type on a host with nullable reference types enabled, so an omitted member is refused
    /// by the model binder before the handler runs; a blank string is what "nothing to say" looks like
    /// there, and the handler keeps the stored number when one arrives.
    func updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String,
        birthDate: Date?,
        languageCode: String?
    ) async -> ApiResult<Void>
}

final class LivePartnerUserClient: PartnerUserClient {
    func getCurrentUser() async -> ApiResult<CurrentUser> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerUserAPI.userGetCurrentUser().toDomain()
        }
    }

    func updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String,
        birthDate: Date?,
        languageCode: String?
    ) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            // No id: the row written is always the JWT caller's and the command's id is inert.
            _ = try await PartnerUserAPI.userUpdateCurrentUser(updateCurrentUserCommand: UpdateCurrentUserCommand(
                firstName: firstName,
                lastName: lastName,
                phoneNumber: phoneNumber,
                birthDate: OpenAPIDateWithoutTime(day: birthDate),
                languageCode: languageCode,
                removePhoto: false
            ))
        }
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
            preferredLanguageCode: preferredLanguageCode
        )
    }
}
