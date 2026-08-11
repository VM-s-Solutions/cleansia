import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// Per-surface pins for the customer money and claim mappers. A fully populated payload maps, and
/// removing a member the C# record declares non-nullable fails the mapping naming that member.
///
/// Nullability is read from the C# records — `QuoteOrder.Response`, `GetMembershipPlans.Response`,
/// `GetMyMembership.Response`, `CancelOrder.Response`, `GetMyLoyalty.Response`,
/// `GetMyReferral.Response`, `SavedAddressDto`, `NotificationPreferencesDto`, `MyProfileDto`,
/// `RecurringBookingTemplateDto` — never from the spec.
final class CustomerWireContractTests: XCTestCase {
    // MARK: the quote — the number the customer commits to

    private func quotePayload() -> QuoteOrderResponse {
        QuoteOrderResponse(
            totalPrice: 2400,
            finalPriceAfterDiscount: 2400,
            originalSubtotal: 2600,
            tierDiscountAmount: 200,
            membershipDiscountAmount: nil,
            currencyId: "cur-1",
            currencyCode: "CZK",
            servicesSubtotal: 2000,
            packagesSubtotal: 600,
            extrasSubtotal: 0,
            expressSurchargeApplied: true,
            expressSurchargeAmount: 400,
            exchangeRate: 1,
            expressSurchargeWaivedByMembership: false
        )
    }

    func testAFullyPopulatedQuoteMaps() throws {
        let quote = try BookingQuote(from: quotePayload())
        XCTAssertEqual(quote.totalPrice, 2400)
        XCTAssertEqual(quote.preSurchargeSubtotal, 2000)
        XCTAssertEqual(quote.currencyId, "cur-1")
    }

    func testABrokenQuoteIsRefusedRatherThanPricedAtZero() {
        for (field, break_) in [
            ("totalPrice", { (dto: inout QuoteOrderResponse) in dto.totalPrice = nil }),
            ("originalSubtotal", { dto in dto.originalSubtotal = nil }),
            ("servicesSubtotal", { dto in dto.servicesSubtotal = nil }),
            ("packagesSubtotal", { dto in dto.packagesSubtotal = nil }),
            ("extrasSubtotal", { dto in dto.extrasSubtotal = nil }),
            ("expressSurchargeAmount", { dto in dto.expressSurchargeAmount = nil }),
            ("expressSurchargeApplied", { dto in dto.expressSurchargeApplied = nil }),
            ("expressSurchargeWaivedByMembership", { dto in dto.expressSurchargeWaivedByMembership = nil }),
            ("currencyId", { dto in dto.currencyId = "" }),
            ("currencyCode", { dto in dto.currencyCode = nil })
        ] {
            var payload = quotePayload()
            break_(&payload)
            assertRefused(field) { try BookingQuote(from: payload) }
        }
    }

    /// The server sends null for *no such discount applied*, which is the same fact as zero — the
    /// summary adds them and nothing is falsified.
    func testAQuoteWithNoDiscountsIsNotARefusal() throws {
        var payload = quotePayload()
        payload.tierDiscountAmount = nil
        payload.membershipDiscountAmount = nil
        let quote = try BookingQuote(from: payload)
        XCTAssertEqual(quote.tierDiscountAmount, 0)
        XCTAssertEqual(quote.membershipDiscountAmount, 0)
    }

    // MARK: the cancellation refund

    func testACancellationReportsTheRefundItWasGiven() throws {
        let cancellation = try OrderCancellation(
            CancelOrderResponse(refundAmount: 1200, refundInitiated: true)
        )
        XCTAssertEqual(cancellation.refunded, 1200)
    }

    func testABrokenCancellationDoesNotReportNoRefund() {
        for (field, break_) in [
            ("refundAmount", { (dto: inout CancelOrderResponse) in dto.refundAmount = nil }),
            ("refundInitiated", { dto in dto.refundInitiated = nil })
        ] {
            var payload = CancelOrderResponse(refundAmount: 1200, refundInitiated: true)
            break_(&payload)
            assertRefused(field) { try OrderCancellation(payload) }
        }
    }

    // MARK: the orders page — pagination's only input

    func testAPageWithNoCountIsRefusedSoOlderOrdersDoNotStopExisting() async {
        let refused: ApiResult<Int> = await apiResult {
            try PagedDataOfOrderListItem(pageNumber: 1, pageSize: 20, total: nil, data: [])
                .total.require("total")
        }
        XCTAssertEqual(refused.apiErrorOrNil?.code, ApiError.wireContractCode)
    }

    // MARK: the express-waiver quota — a claim, not a number

    /// The one case where the coerced value is the OPPOSITE of what the server's null means:
    /// *null = no membership*, and `?? 0` turned it into "you used your allowance up".
    func testAnUnreportedQuotaIsNotReportedAsUsedUp() {
        XCTAssertEqual(
            ExpressWaiverStatus.resolve(
                hasMembership: true,
                upgradesPerMonth: 2,
                upgradesRemaining: nil,
                trialEndsAtUtc: nil,
                now: Date()
            ),
            .none
        )
    }

    // MARK: notification preferences — read, then written back

    func testEveryPreferenceIsRefusedBecauseTheScreenWritesThemBack() {
        for (field, break_) in [
            ("orderUpdates", { (dto: inout NotificationPreferencesDto) in dto.orderUpdates = nil }),
            ("promo", { dto in dto.promo = nil }),
            ("disputeReply", { dto in dto.disputeReply = nil })
        ] {
            var payload = preferencesPayload()
            break_(&payload)
            assertRefused(field) { try payload.toDomain() }
        }
    }

    func testAFullyPopulatedPreferencePayloadMaps() throws {
        let preferences = try preferencesPayload().toDomain()
        XCTAssertTrue(preferences.orderUpdates)
        XCTAssertFalse(preferences.promo)
    }

    private func preferencesPayload() -> NotificationPreferencesDto {
        NotificationPreferencesDto(
            orderUpdates: true,
            cleanerOnTheWay: true,
            orderCompleted: true,
            orderCancelled: true,
            refundIssued: true,
            membershipExpiring: true,
            membershipCancelled: true,
            tierUpgrade: true,
            promo: false,
            disputeReply: true,
            recurringScheduled: true
        )
    }

    // MARK: the referral code — the one string a spec sweep reads clean

    func testAReferralAccountWithNoCodeRefusesRatherThanSharingNothing() {
        var payload = GetMyReferralResponse(
            code: "JANE-2026",
            timesUsed: 3,
            qualifiedCount: 2,
            acceptedCount: 3,
            pointsPerReferral: 250
        )
        XCTAssertEqual(try? payload.toDomain().code, "JANE-2026")
        payload.code = ""
        assertRefused("code") { try payload.toDomain() }
    }

    // MARK: the saved-address picker

    func testASavedAddressIsRefusedRatherThanLosingItsDefaultFlag() {
        var payload = SavedAddressDto(
            id: "addr-1",
            label: "Home",
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            isDefault: true
        )
        XCTAssertEqual(try? payload.toDomain().isDefault, true)
        payload.isDefault = nil
        assertRefused("isDefault") { try payload.toDomain() }
    }

    private func assertRefused(
        _ field: String,
        file: StaticString = #filePath,
        line: UInt = #line,
        _ map: () throws -> some Any
    ) {
        XCTAssertThrowsError(try map(), "\(field) was supplied a value instead of refusing", file: file, line: line) {
            XCTAssertEqual(
                $0 as? WireContractViolation,
                WireContractViolation(field: field),
                "the refusal must name \(field)",
                file: file,
                line: line
            )
        }
    }
}
