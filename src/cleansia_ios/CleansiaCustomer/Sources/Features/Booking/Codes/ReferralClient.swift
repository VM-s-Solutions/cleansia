import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct ReferralValidation: Equatable {
    let isValid: Bool
    let referrerFirstName: String?
    let errorCode: String?
}

protocol ReferralClient {
    func validate(code: String) async -> ApiResult<ReferralValidation>
}

struct LiveReferralClient: ReferralClient {
    func validate(code: String) async -> ApiResult<ReferralValidation> {
        let query = ValidateReferralQuery(code: code)
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerReferralAPI.referralValidate(validateReferralQuery: query)
        }
        return result.map { response in
            ReferralValidation(
                isValid: response.isValid ?? false,
                referrerFirstName: response.referrerFirstName,
                errorCode: response.errorCode
            )
        }
    }
}
