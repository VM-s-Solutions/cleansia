import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct PromoValidation: Equatable {
    let isValid: Bool
    let discountAmount: Double?
    let errorCode: String?
}

protocol PromoCodeClient {
    func validate(code: String, orderSubtotal: Double, currencyId: String?) async -> ApiResult<PromoValidation>
}

struct LivePromoCodeClient: PromoCodeClient {
    func validate(code: String, orderSubtotal: Double, currencyId: String?) async -> ApiResult<PromoValidation> {
        let command = ValidatePromoCodeCommand(code: code, orderSubtotal: orderSubtotal, currencyId: currencyId)
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerPromoCodeAPI.promoCodeValidate(validatePromoCodeCommand: command)
        }
        return result.map { response in
            PromoValidation(
                isValid: response.isValid ?? false,
                discountAmount: response.discountAmount,
                errorCode: response.errorCode
            )
        }
    }
}
