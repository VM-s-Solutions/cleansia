import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct PaymentIntentDetails: Equatable {
    let clientSecret: String
    let ephemeralKey: String
    let stripeCustomerId: String
}

protocol PaymentIntentClient {
    func createPaymentIntent(orderId: String) async -> ApiResult<PaymentIntentDetails>
}

struct LivePaymentIntentClient: PaymentIntentClient {
    func createPaymentIntent(orderId: String) async -> ApiResult<PaymentIntentDetails> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerPaymentAPI.paymentCreatePaymentIntent(
                createPaymentIntentCommand: CreatePaymentIntentCommand(orderId: orderId)
            )
        }
        return result.map { response in
            PaymentIntentDetails(
                clientSecret: response.clientSecret ?? "",
                ephemeralKey: response.ephemeralKey ?? "",
                stripeCustomerId: response.stripeCustomerId ?? ""
            )
        }
    }
}
