import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct ReferralValidation: Equatable {
    let isValid: Bool
    let referrerFirstName: String?
    let errorCode: String?
}

/// `Referral/Validate` is anonymous and market-scoped: the code is looked up in the operating
/// company that serves `countryId`, so the caller names the country the account or the booking
/// will land in. Nil is the default market.
protocol ReferralClient {
    func validate(code: String, countryId: String?) async -> ApiResult<ReferralValidation>
}

struct LiveReferralClient: ReferralClient {
    func validate(code: String, countryId: String?) async -> ApiResult<ReferralValidation> {
        let query = ValidateReferralQuery(code: code, countryId: countryId)
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
