import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeSavedCardClient: SavedCardClient, @unchecked Sendable {
    /// Answered in order; the last one keeps answering.
    var reads: [ApiResult<[SavedCard]>]
    var captureResult: ApiResult<SavedCardSetup>
    var removeResult: ApiResult<Void>
    private(set) var readCount = 0
    private(set) var captureConsents: [Bool] = []
    private(set) var captureCountryIds: [String?] = []
    private(set) var removedIds: [String] = []

    init(
        reads: [ApiResult<[SavedCard]>] = [.success([])],
        captureResult: ApiResult<SavedCardSetup> = .success(PaymentsFixtures.setup),
        removeResult: ApiResult<Void> = .success(())
    ) {
        self.reads = reads
        self.captureResult = captureResult
        self.removeResult = removeResult
    }

    /// A customer whose cash bookings in CZK are already guaranteed.
    static func holdingCzkCard() -> FakeSavedCardClient {
        FakeSavedCardClient(reads: [.success([PaymentsFixtures.czkCard])])
    }

    func myCards() async -> ApiResult<[SavedCard]> {
        let answer = reads[min(readCount, reads.count - 1)]
        readCount += 1
        return answer
    }

    func startCapture(consentAccepted: Bool, countryId: String?) async -> ApiResult<SavedCardSetup> {
        captureConsents.append(consentAccepted)
        captureCountryIds.append(countryId)
        return captureResult
    }

    func remove(savedCardId: String) async -> ApiResult<Void> {
        removedIds.append(savedCardId)
        return removeResult
    }
}

final class FakeReceivableClient: ReceivableClient, @unchecked Sendable {
    var receivablesResult: ApiResult<[Receivable]>
    var payLinkResult: ApiResult<URL>
    private(set) var payLinkIds: [String] = []

    init(
        receivablesResult: ApiResult<[Receivable]> = .success([]),
        payLinkResult: ApiResult<URL> = .success(PaymentsFixtures.checkoutUrl)
    ) {
        self.receivablesResult = receivablesResult
        self.payLinkResult = payLinkResult
    }

    func myReceivables() async -> ApiResult<[Receivable]> {
        receivablesResult
    }

    func payLink(receivableId: String) async -> ApiResult<URL> {
        payLinkIds.append(receivableId)
        return payLinkResult
    }
}

enum PaymentsFixtures {
    static let czkCard = card(id: "card-czk", currencyCode: "CZK")

    static func card(id: String, currencyCode: String, expMonth: Int = 12, expYear: Int = 2099) -> SavedCard {
        SavedCard(
            id: id,
            brand: "visa",
            last4: "4242",
            expMonth: expMonth,
            expYear: expYear,
            currencyCode: currencyCode
        )
    }

    static let setup = SavedCardSetup(
        savedCardId: "card-new",
        setupIntentClientSecret: "seti_secret_123",
        stripeCustomerId: "cus_789",
        ephemeralKey: "ek_secret_456"
    )

    static let checkoutUrl = URL(string: "https://checkout.stripe.com/c/pay/cs_test_1") ?? URL(fileURLWithPath: "/")

    static func receivable(id: String = "rcv-1", kind: Receivable.Kind = .cancellationFee) -> Receivable {
        Receivable(
            id: id,
            orderId: "ord-1",
            displayOrderNumber: "CL-1042",
            kind: kind,
            amount: 450,
            currencyCode: "CZK",
            createdOn: Date(timeIntervalSince1970: 1_790_000_000)
        )
    }
}
