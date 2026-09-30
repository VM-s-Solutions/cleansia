import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// The card a customer saves as the guarantee for cash bookings: captured through a SetupIntent, never
/// charged at capture.
struct SavedCard: Equatable, Identifiable {
    let id: String
    let brand: String
    let last4: String
    let expMonth: Int
    let expYear: Int
    let currencyCode: String

    /// The server's `SavedCard.IsUsableOn`: good through the last day of its expiry month, in UTC.
    func isUsable(on date: Date) -> Bool {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0) ?? calendar.timeZone
        let month = calendar.dateComponents([.year, .month], from: date)
        guard let year = month.year, let monthValue = month.month else { return false }
        return expYear > year || (expYear == year && expMonth >= monthValue)
    }

    static func usable(in cards: [SavedCard], currencyCode: String, on date: Date = Date()) -> SavedCard? {
        cards.first { card in
            card.currencyCode.caseInsensitiveCompare(currencyCode) == .orderedSame && card.isUsable(on: date)
        }
    }
}

/// What PaymentSheet needs in setup mode.
struct SavedCardSetup: Equatable {
    let savedCardId: String
    let setupIntentClientSecret: String
    let stripeCustomerId: String
    let ephemeralKey: String
}

protocol SavedCardClient {
    func myCards() async -> ApiResult<[SavedCard]>
    func startCapture(consentAccepted: Bool, countryId: String?) async -> ApiResult<SavedCardSetup>
    func remove(savedCardId: String) async -> ApiResult<Void>
}

struct LiveSavedCardClient: SavedCardClient {
    /// Refuses the page rather than dropping a row: a card missing from this list reads as "no card
    /// saved", and the booking would then capture a second card that retires the one the customer holds.
    func myCards() async -> ApiResult<[SavedCard]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerSavedCardAPI.savedCardGetMine().map { try SavedCard($0) }
        }
    }

    func startCapture(consentAccepted: Bool, countryId: String?) async -> ApiResult<SavedCardSetup> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let setup = try await CustomerSavedCardAPI.savedCardCreateSetupIntent(
                createSavedCardSetupIntentCommand: CreateSavedCardSetupIntentCommand(
                    consentAccepted: consentAccepted,
                    countryId: countryId
                )
            )
            return try SavedCardSetup(
                savedCardId: setup.savedCardId.requireNonBlank("savedCardId"),
                setupIntentClientSecret: setup.setupIntentClientSecret.requireNonBlank("setupIntentClientSecret"),
                stripeCustomerId: setup.stripeCustomerId.requireNonBlank("stripeCustomerId"),
                ephemeralKey: setup.ephemeralKey.requireNonBlank("ephemeralKey")
            )
        }
    }

    func remove(savedCardId: String) async -> ApiResult<Void> {
        await apiResult(mapError: ApiError.fromGenerated) {
            _ = try await CustomerSavedCardAPI.savedCardRemove(id: savedCardId)
        }
    }
}

extension SavedCard {
    init(_ dto: SavedCardDto) throws {
        try self.init(
            id: dto.id.requireNonBlank("id"),
            brand: dto.brand.requireNonBlank("brand"),
            last4: dto.last4.requireNonBlank("last4"),
            expMonth: dto.expMonth.require("expMonth"),
            expYear: dto.expYear.require("expYear"),
            currencyCode: dto.currencyCode.requireNonBlank("currencyCode")
        )
    }
}
