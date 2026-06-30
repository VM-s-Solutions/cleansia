import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol ChangePasswordClient: AnyObject {
    func requestCode(email: String, language: String) async -> ApiResult<Void>
    func changePassword(email: String, code: String, newPassword: String) async -> ApiResult<Void>
}

final class LiveChangePasswordClient: ChangePasswordClient {
    func requestCode(email: String, language: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerUserAPI.userRequestPasswordChange(
                requestPasswordChangeCommand: RequestPasswordChangeCommand(email: email, language: language)
            )
        }
    }

    func changePassword(email: String, code: String, newPassword: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerUserAPI.userChangePassword(
                changePasswordCommand: ChangePasswordCommand(email: email, newPassword: newPassword, code: code)
            )
        }
    }
}
