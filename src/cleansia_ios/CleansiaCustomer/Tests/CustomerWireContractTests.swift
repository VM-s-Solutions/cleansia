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

    func quotePayload() -> QuoteOrderResponse {
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
            requiredEmployees: 2,
            expressSurchargeWaivedByMembership: false,
            dirtinessSurchargeAmount: 300,
            dirtinessLevel: ._1
        )
    }

    /// The two inputs to the confirm step's credit preview ride the quote; an absent one previews no
    /// credit rather than refusing the quote, since the order's own figures say what the card paid.
    func testTheQuoteCarriesTheCreditInputs() throws {
        var payload = quotePayload()
        payload.creditBalance = 250
        payload.creditMaxShareOfOrder = 0.7
        let quote = try BookingQuote(from: payload)
        XCTAssertEqual(quote.creditBalance, 250)
        XCTAssertEqual(quote.creditMaxShareOfOrder, 0.7)

        payload.creditBalance = nil
        payload.creditMaxShareOfOrder = nil
        let bare = try BookingQuote(from: payload)
        XCTAssertEqual(bare.creditBalance, 0)
        XCTAssertEqual(bare.creditMaxShareOfOrder, 0)
    }

    // MARK: credit — money owed, so refused rather than zeroed

    func creditPayload() -> GetMyCreditResponse {
        GetMyCreditResponse(
            balance: 250,
            currencyCode: "CZK",
            maxShareOfOrder: 0.7,
            appliesAutomatically: true,
            expiresOn: Date(timeIntervalSince1970: 1_800_000_000),
            balances: [GetMyCreditCurrencyBalance(
                balance: 250,
                currencyCode: "CZK",
                expiresOn: Date(timeIntervalSince1970: 1_800_000_000)
            )]
        )
    }

    func testAFullyPopulatedCreditMaps() throws {
        let credit = try creditPayload().toDomain()
        XCTAssertEqual(credit.primary.amount, 250)
        XCTAssertEqual(credit.primary.currencyCode, "CZK")
        XCTAssertEqual(credit.maxShareOfOrder, 0.7)
        XCTAssertEqual(credit.balances.count, 1)
        XCTAssertEqual(credit.heldBalances.count, 1)
    }

    /// The never-credited customer: no rows, and one zero in the platform default — shown, not hidden.
    func testANeverCreditedCustomerMapsToAZero() throws {
        var payload = creditPayload()
        payload.balance = 0
        payload.expiresOn = nil
        payload.balances = []
        let credit = try payload.toDomain()
        XCTAssertEqual(credit.primary.amount, 0)
        XCTAssertTrue(credit.heldBalances.isEmpty)
    }

    func testABrokenCreditIsRefusedRatherThanReadAsNothingOwed() {
        for (field, corrupt) in [
            ("balance", { (dto: inout GetMyCreditResponse) in dto.balance = nil }),
            ("currencyCode", { dto in dto.currencyCode = "" }),
            ("maxShareOfOrder", { dto in dto.maxShareOfOrder = nil }),
            ("balance", { dto in dto.balances = [GetMyCreditCurrencyBalance(currencyCode: "EUR")] }),
            ("currencyCode", { dto in dto.balances = [GetMyCreditCurrencyBalance(balance: 10)] })
        ] {
            var payload = creditPayload()
            corrupt(&payload)
            assertRefused(field) { try payload.toDomain() }
        }
    }

    func testAFullyPopulatedQuoteMaps() throws {
        let quote = try BookingQuote(from: quotePayload())
        XCTAssertEqual(quote.totalPrice, 2400)
        XCTAssertEqual(quote.preSurchargeSubtotal, 2000)
        XCTAssertEqual(quote.currencyId, "cur-1")
        XCTAssertEqual(quote.requiredEmployees, 2)
        XCTAssertEqual(quote.dirtiness, .increased)
        XCTAssertEqual(quote.dirtinessSurchargeAmount, 300)
    }

    func testABrokenQuoteIsRefusedRatherThanPricedAtZero() {
        for (field, corrupt) in [
            ("totalPrice", { (dto: inout QuoteOrderResponse) in dto.totalPrice = nil }),
            ("originalSubtotal", { dto in dto.originalSubtotal = nil }),
            ("servicesSubtotal", { dto in dto.servicesSubtotal = nil }),
            ("packagesSubtotal", { dto in dto.packagesSubtotal = nil }),
            ("extrasSubtotal", { dto in dto.extrasSubtotal = nil }),
            ("expressSurchargeAmount", { dto in dto.expressSurchargeAmount = nil }),
            ("expressSurchargeApplied", { dto in dto.expressSurchargeApplied = nil }),
            ("expressSurchargeWaivedByMembership", { dto in dto.expressSurchargeWaivedByMembership = nil }),
            ("currencyId", { dto in dto.currencyId = "" }),
            ("currencyCode", { dto in dto.currencyCode = nil }),
            ("requiredEmployees", { dto in dto.requiredEmployees = nil }),
            ("dirtinessSurchargeAmount", { dto in dto.dirtinessSurchargeAmount = nil }),
            ("dirtinessLevel", { dto in dto.dirtinessLevel = nil })
        ] {
            var payload = quotePayload()
            corrupt(&payload)
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

    /// The figure is the refund the server actually issued, never the policy `refundAmount` it
    /// quotes for every cancel — including a cash order that refunds nothing.
    func testACancellationReportsTheRefundItWasGivenAndNotThePolicyFigure() throws {
        let cancellation = try OrderCancellation(
            CancelOrderResponse(refundAmount: 1200, refundInitiated: true, actualRefundAmount: 1150)
        )
        XCTAssertEqual(cancellation.refundAmount, 1200)
        XCTAssertEqual(cancellation.actualRefundAmount, 1150)
        XCTAssertEqual(cancellation.refunded, 1150)
    }

    /// Nullable by design: no confirmed refund is "no refund", not the policy amount borrowed back.
    func testANullActualRefundStaysUnknownInsteadOfBorrowingThePolicyAmount() throws {
        let cancellation = try OrderCancellation(
            CancelOrderResponse(refundAmount: 1200, refundInitiated: true, actualRefundAmount: nil)
        )
        XCTAssertNil(cancellation.actualRefundAmount)
        XCTAssertNil(cancellation.refunded)
        XCTAssertEqual(cancellation.refundAmount, 1200)
    }

    func testABrokenCancellationDoesNotReportNoRefund() {
        for (field, corrupt) in [
            ("refundAmount", { (dto: inout CancelOrderResponse) in dto.refundAmount = nil }),
            ("refundInitiated", { dto in dto.refundInitiated = nil })
        ] {
            var payload = CancelOrderResponse(refundAmount: 1200, refundInitiated: true, actualRefundAmount: 1200)
            corrupt(&payload)
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

    // MARK: the order detail — the price the customer paid, and the scope it was quoted against

    func testAFullyPopulatedOrderDetailMaps() throws {
        var payload = OrderItem.wireComplete()
        payload.currency = CurrencyDetailDto(code: "CZK")
        payload.selectedServices = [ServiceDetails(name: "Deep clean", estimatedTime: 120)]
        payload.selectedPackages = [PackageDetails(name: "Move-out", price: 800, estimatedTime: 60)]
        payload.review = OrderReviewDto(rating: 4, comment: "Spotless.")
        payload.dirtinessLevel = ._2
        payload.dirtinessSurchargeAmount = 540

        let detail = try CustomerOrderDetail(payload)

        XCTAssertEqual(detail.total, 1590)
        XCTAssertEqual(detail.originalSubtotal, 2100)
        XCTAssertEqual(detail.rooms, 3)
        XCTAssertEqual(detail.estimatedMinutes, 180)
        XCTAssertEqual(detail.packages.first?.price, 800)
        XCTAssertEqual(detail.review?.rating, 4)
        XCTAssertEqual(detail.currencyCode, "CZK")
        XCTAssertEqual(detail.dirtiness, .heavy)
        XCTAssertEqual(detail.dirtinessSurchargeAmount, 540)
        XCTAssertEqual(detail.creditAppliedAmount, 0)
        XCTAssertEqual(detail.amountDueOnCard, 1590)
    }

    func testABrokenOrderDetailIsRefusedRatherThanPricedAtZero() {
        for (field, corrupt) in [
            ("totalPrice", { (dto: inout OrderItem) in dto.totalPrice = nil }),
            ("originalSubtotal", { dto in dto.originalSubtotal = nil }),
            ("rooms", { dto in dto.rooms = nil }),
            ("bathrooms", { dto in dto.bathrooms = nil }),
            ("estimatedTime", { dto in dto.estimatedTime = nil }),
            ("dirtinessLevel", { dto in dto.dirtinessLevel = nil }),
            ("dirtinessSurchargeAmount", { dto in dto.dirtinessSurchargeAmount = nil }),
            ("creditAppliedAmount", { dto in dto.creditAppliedAmount = nil }),
            ("amountDueOnCard", { dto in dto.amountDueOnCard = nil })
        ] {
            var payload = OrderItem.wireComplete()
            corrupt(&payload)
            assertRefused(field) { try CustomerOrderDetail(payload) }
        }
    }

    /// A line refuses with the order rather than dropping out of it: the lines and the total are read
    /// side by side, so a silently shorter or cheaper breakdown is a total that stops adding up.
    func testABrokenCatalogLineRefusesWithTheOrder() {
        for (field, corrupt) in [
            ("price", { (dto: inout OrderItem) in dto.selectedPackages = [PackageDetails(estimatedTime: 60)] }),
            ("estimatedTime", { dto in dto.selectedPackages = [PackageDetails(price: 800)] }),
            ("estimatedTime", { dto in dto.selectedServices = [ServiceDetails(name: "Deep clean")] })
        ] {
            var payload = OrderItem.wireComplete()
            corrupt(&payload)
            assertRefused(field) { try CustomerOrderDetail(payload) }
        }
    }

    /// A review always carries a rating. Coerced, the card draws five empty stars over the customer's
    /// own comment — a verdict they never gave rather than a blank.
    func testAReviewWithNoRatingRefusesRatherThanDrawingZeroStars() {
        var payload = OrderItem.wireComplete()
        payload.review = OrderReviewDto(rating: nil, comment: "Spotless.")
        assertRefused("rating") { try CustomerOrderDetail(payload) }
    }

    /// The market an order was booked in rides both the row and the detail as its `countryId`; the
    /// label resolves it against the directory, so nothing here is refused — an absent country reads as
    /// unavailable rather than blanking the order.
    func testTheRowAndTheDetailCarryTheMarketTheyWereBookedIn() throws {
        var row = OrderListItem.wireComplete()
        row.countryId = "svk"
        XCTAssertEqual(try CustomerOrderSummary(row)?.countryId, "svk")
        row.countryId = nil
        XCTAssertNil(try CustomerOrderSummary(row)?.countryId)

        var detail = OrderItem.wireComplete()
        detail.countryId = "svk"
        XCTAssertEqual(try CustomerOrderDetail(detail).countryId, "svk")
        detail.countryId = nil
        XCTAssertNil(try CustomerOrderDetail(detail).countryId)
    }

    /// Whether an occurrence still waits for the customer is the server's answer: a confirmed cash
    /// occurrence stays payment-pending until the cleaner takes the cash.
    func testTheDetailCarriesTheServersConfirmationVerdict() throws {
        var payload = OrderItem.wireComplete()
        payload.recurringTemplateId = "tpl-1"
        payload.paymentStatus = Code(type: "PaymentStatus", name: nil, value: 1)

        payload.needsConfirmation = true
        XCTAssertTrue(try CustomerOrderDetail(payload).needsConfirmation)
        payload.needsConfirmation = false
        XCTAssertFalse(try CustomerOrderDetail(payload).needsConfirmation)
        payload.needsConfirmation = nil
        XCTAssertFalse(try CustomerOrderDetail(payload).needsConfirmation)
    }

    /// The one identifier this surface does NOT refuse: the screen is routed with the order id and
    /// keeps it, so a null here has an equally authoritative replacement and refusing would blank a
    /// screen that navigates perfectly.
    func testTheDetailKeepsMappingWithoutAnIdBecauseTheRouteCarriesOne() throws {
        var payload = OrderItem.wireComplete()
        payload.id = nil
        XCTAssertNil(try CustomerOrderDetail(payload).id)
    }

    // MARK: the orders page — the row's own money, and the row that cannot be opened

    /// Because the row is an element of the page, refusing it refuses the page: the client maps the
    /// rows with a `rethrows` `compactMap`, so an order is priced as the server priced it or the list
    /// says it could not be loaded.
    func testAnOrderRowRefusesItsOwnMoneyRatherThanShowingItAtZero() {
        for (field, corrupt) in [
            ("totalPrice", { (dto: inout OrderListItem) in dto.totalPrice = nil }),
            ("estimatedTime", { dto in dto.estimatedTime = nil })
        ] {
            var payload = OrderListItem.wireComplete()
            corrupt(&payload)
            assertRefused(field) { try CustomerOrderSummary(payload) }
        }
    }

    /// The other half of the same ruling, and it goes the other way: an id-less row is already dead
    /// because every card navigates by id, and nothing on the list or Home sums or counts these rows
    /// against a figure — the paged `total` is the server's own count — so dropping one falsifies
    /// nothing while refusing the page would hide every order the server answered correctly.
    func testARowWithNoUsableIdIsDroppedRatherThanRefusingThePage() throws {
        for missing in [nil, "", "   "] {
            var payload = OrderListItem.wireComplete()
            payload.id = missing
            XCTAssertNil(try CustomerOrderSummary(payload), "\(missing.debugDescription) is not a navigable row")
        }
        XCTAssertEqual(try CustomerOrderSummary(.wireComplete())?.total, 1590)
    }

    // MARK: the cancellation quote — the only numbers the customer reads and THEN decides

    /// Each of these pushes the customer toward the outcome that costs them: a defaulted fee reads
    /// "free" over a charge, a defaulted refund reads "nothing comes back" over money that does, and a
    /// defaulted waiver flag promises a free express booking they are about to spend.
    /// `GetCancellationFeePreview.Response` is a positional record of non-nullable members with one
    /// success path, so there is no state this sheet can be in where any of them is legitimately absent.
    func testEveryFigureOnTheCancelSheetIsRefusedRatherThanDefaulted() {
        for (field, corrupt) in [
            ("tier", { (dto: inout GetCancellationFeePreviewResponse) in dto.tier = nil }),
            ("feeAmount", { dto in dto.feeAmount = nil }),
            ("refundAmount", { dto in dto.refundAmount = nil }),
            ("expressWaiverForfeitedOnCancel", { dto in dto.expressWaiverForfeitedOnCancel = nil }),
            ("oopsWindowMinutes", { dto in dto.oopsWindowMinutes = nil })
        ] {
            var payload = cancellationPayload()
            corrupt(&payload)
            assertRefused(field) { try CancellationQuote(payload) }
        }
    }

    func testAFullyPopulatedCancellationQuoteMaps() throws {
        let quote = try CancellationQuote(cancellationPayload())
        XCTAssertEqual(quote.tier, .partial)
        XCTAssertEqual(quote.feeAmount, 250)
        XCTAssertEqual(quote.refundAmount, 750)
        XCTAssertTrue(quote.forfeitsExpressWaiver)
        XCTAssertEqual(quote.oopsWindowMinutes, 60)
    }

    func cancellationPayload() -> GetCancellationFeePreviewResponse {
        GetCancellationFeePreviewResponse(
            orderId: "o1",
            tier: ._3,
            feeRate: 0.25,
            feeAmount: 250,
            refundAmount: 750,
            totalPrice: 1000,
            currencyCode: "CZK",
            expressWaiverForfeitedOnCancel: true,
            oopsWindowMinutes: 60
        )
    }

    // MARK: the recurring list — a schedule the server now skips

    func templatePayload() -> RecurringBookingTemplateDto {
        RecurringBookingTemplateDto(
            id: "tpl-1",
            frequency: 1,
            dayOfWeek: 4,
            timeOfDay: "10:00",
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "addr-1",
            paymentType: 1,
            startsOn: Date(timeIntervalSince1970: 1_780_000_000),
            isActive: true,
            requiresPaymentMethodChange: true,
            dirtinessLevel: ._2
        )
    }

    /// A cash schedule that now needs more than one cleaner is skipped rather than switched to card, so
    /// the list is the only place the customer learns it books nothing. Defaulted to `false`, the card
    /// reads as a live schedule that will never produce a cleaning.
    func testATemplateCarriesWhetherItNeedsAPaymentChangeAndRefusesWithoutIt() throws {
        XCTAssertTrue(try templatePayload().toDomain().requiresPaymentMethodChange)
        var payload = templatePayload()
        payload.requiresPaymentMethodChange = nil
        assertRefused("requiresPaymentMethodChange") { try payload.toDomain() }
    }

    /// Every occurrence is priced at the schedule's level and an edit sends the form back whole, so a
    /// level coerced to Normal would quietly reprice the schedule on the next save.
    func testATemplateCarriesItsLevelAndRefusesWithoutIt() throws {
        XCTAssertEqual(try templatePayload().toDomain().dirtiness, .heavy)
        var payload = templatePayload()
        payload.dirtinessLevel = nil
        assertRefused("dirtinessLevel") { try payload.toDomain() }
    }

    // MARK: the photo counts — a figure the server computes beside the rail, not from it

    /// `GetOrderPhotos` runs a dedicated count query per type rather than tallying the list it ships,
    /// so the count is a second statement about the order. Coerced, the pill reads "0 before photos"
    /// above a rail that is showing them.
    func testThePhotoCountsAreRefusedRatherThanReportedAsNone() {
        for (field, corrupt) in [
            ("beforePhotoCount", { (dto: inout GetOrderPhotosResponse) in dto.beforePhotoCount = nil }),
            ("afterPhotoCount", { dto in dto.afterPhotoCount = nil })
        ] {
            var payload = GetOrderPhotosResponse(photos: [], beforePhotoCount: 3, afterPhotoCount: 4)
            corrupt(&payload)
            assertRefused(field) { try OrderPhotos(payload) }
        }
    }

    /// A genuine zero is not a refusal, and an absent list is the same fact as an empty gallery.
    func testAnOrderWithNoPhotosIsNotARefusal() throws {
        let gallery = try OrderPhotos(GetOrderPhotosResponse(beforePhotoCount: 0, afterPhotoCount: 0))
        XCTAssertEqual(gallery.photos, [])
        XCTAssertEqual(gallery.beforeCount, 0)
    }
}
