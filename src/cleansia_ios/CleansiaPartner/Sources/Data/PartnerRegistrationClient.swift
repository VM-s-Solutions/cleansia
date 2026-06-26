import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerRegistrationClient {
    func checkRegistrationStatus() async -> ApiResult<RegistrationCompletionStatus>
}

struct LivePartnerRegistrationClient: PartnerRegistrationClient {
    func checkRegistrationStatus() async -> ApiResult<RegistrationCompletionStatus> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeCheckCurrentEmployee()
        }
    }
}
