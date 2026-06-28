import CleansiaCore
import CleansiaCustomerApi
import Foundation

struct QuoteRequest: Equatable {
    let serviceIds: [String]
    let packageIds: [String]
    let extraSlugs: [String]
    let rooms: Int
    let bathrooms: Int
    let cleaningDate: Date?
}

protocol QuoteClient {
    func quote(_ request: QuoteRequest) async -> ApiResult<BookingQuote>
}

struct LiveQuoteClient: QuoteClient {
    func quote(_ request: QuoteRequest) async -> ApiResult<BookingQuote> {
        let command = QuoteOrderCommand(
            selectedServiceIds: request.serviceIds,
            selectedPackageIds: request.packageIds,
            rooms: request.rooms,
            bathrooms: request.bathrooms,
            currencyId: nil,
            selectedExtraSlugs: request.extraSlugs,
            cleaningDate: request.cleaningDate
        )
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerOrderAPI.orderQuote(quoteOrderCommand: command)
        }
        return result.map(BookingQuote.init(from:))
    }
}
