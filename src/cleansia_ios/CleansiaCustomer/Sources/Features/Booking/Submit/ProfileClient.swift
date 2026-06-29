import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct BookingProfile: Equatable {
    let firstName: String
    let lastName: String
    let email: String
    let phoneNumber: String

    var fullName: String {
        [firstName, lastName].filter { !$0.isBlank }.joined(separator: " ")
    }

    var isComplete: Bool {
        !firstName.isBlank && !lastName.isBlank && !email.isBlank && !phoneNumber.isBlank
    }
}

protocol ProfileClient {
    func currentProfile() async -> ApiResult<BookingProfile>
}

struct LiveProfileClient: ProfileClient {
    func currentProfile() async -> ApiResult<BookingProfile> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerUserAPI.userGetCurrentUser()
        }
        return result.map { dto in
            BookingProfile(
                firstName: dto.firstName ?? "",
                lastName: dto.lastName ?? "",
                email: dto.email ?? "",
                phoneNumber: dto.phoneNumber ?? ""
            )
        }
    }
}
