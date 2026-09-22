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
    /// The service address's country — the market the server prices and charges the booking in.
    /// Nil until the address step yields one, which is the platform default.
    let countryId: String?
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
            cleaningDate: request.cleaningDate,
            countryId: request.countryId
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await BookingQuote(from: CustomerOrderAPI.orderQuote(quoteOrderCommand: command))
        }
    }
}
