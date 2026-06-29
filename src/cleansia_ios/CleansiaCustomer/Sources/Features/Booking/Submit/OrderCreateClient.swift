import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct CreatedOrder: Equatable {
    let id: String
    let confirmationCode: String
}

protocol OrderCreateClient {
    func create(_ command: CreateOrderCommand) async -> ApiResult<CreatedOrder>
}

struct LiveOrderCreateClient: OrderCreateClient {
    func create(_ command: CreateOrderCommand) async -> ApiResult<CreatedOrder> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderCreateOrder(createOrderCommand: command)
        }
        return result.map { response in
            CreatedOrder(
                id: response.id ?? "",
                confirmationCode: response.confirmationCode ?? ""
            )
        }
    }
}
