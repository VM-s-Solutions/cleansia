import CleansiaCore
import CleansiaCustomerApi
import Foundation
@testable import CleansiaCustomer

final class FakeOrderClient: OrderClient, @unchecked Sendable {
    var pages: [OrdersPage] = []
    var pageError: ApiError?
    private(set) var pageRequests: [(offset: Int, limit: Int)] = []

    var detailResults: [ApiResult<CustomerOrderDetail>] = []
    private(set) var detailCallCount = 0

    var cancelResult: ApiResult<OrderCancellation> = .success(
        OrderCancellation(refundAmount: 0, refundInitiated: false)
    )
    private(set) var cancelCallCount = 0
    private(set) var lastCancelReason: String??

    var reviewResult: ApiResult<OrderReviewDto> = .success(OrderReviewDto())
    private(set) var reviewCallCount = 0
    private(set) var lastReview: (rating: Int, comment: String?)?
    private(set) var lastReviewTags: [CustomerReviewTag] = []

    var receiptResult: ApiResult<URL> = .success(URL(fileURLWithPath: "/tmp/receipt.pdf"))
    private(set) var receiptCallCount = 0

    var photosResults: [ApiResult<OrderPhotos>] = []
    private(set) var photosCallCount = 0

    var confirmRecurringResult: ApiResult<RecurringConfirmation> =
        .success(RecurringConfirmation(clientSecret: nil, stripeCustomerId: nil, ephemeralKey: nil))
    private(set) var confirmRecurringCallCount = 0

    var cancellationQuoteResults: [ApiResult<CancellationQuote>] = []
    private(set) var cancellationQuoteCallCount = 0

    func getMyOrders(offset: Int, limit: Int) async -> ApiResult<OrdersPage> {
        pageRequests.append((offset, limit))
        if let pageError { return .failure(pageError) }
        let index = min(pageRequests.count - 1, pages.count - 1)
        guard index >= 0 else { return .success(OrdersPage(items: [], total: 0)) }
        return .success(pages[index])
    }

    func getById(orderId _: String) async -> ApiResult<CustomerOrderDetail> {
        defer { detailCallCount += 1 }
        let index = min(detailCallCount, detailResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return detailResults[index]
    }

    func cancel(orderId _: String, reason: String?) async -> ApiResult<OrderCancellation> {
        cancelCallCount += 1
        lastCancelReason = .some(reason)
        return cancelResult
    }

    func submitReview(
        orderId _: String,
        rating: Int,
        comment: String?,
        tags: [CustomerReviewTag]
    ) async -> ApiResult<OrderReviewDto> {
        reviewCallCount += 1
        lastReview = (rating, comment)
        lastReviewTags = tags
        return reviewResult
    }

    func downloadReceipt(orderId _: String) async -> ApiResult<URL> {
        receiptCallCount += 1
        return receiptResult
    }

    func getPhotos(orderId _: String) async -> ApiResult<OrderPhotos> {
        defer { photosCallCount += 1 }
        let index = min(photosCallCount, photosResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return photosResults[index]
    }

    func confirmRecurring(orderId _: String) async -> ApiResult<RecurringConfirmation> {
        confirmRecurringCallCount += 1
        return confirmRecurringResult
    }

    func cancellationQuote(orderId _: String) async -> ApiResult<CancellationQuote> {
        defer { cancellationQuoteCallCount += 1 }
        let index = min(cancellationQuoteCallCount, cancellationQuoteResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return cancellationQuoteResults[index]
    }
}

/// Domain fixtures, built through the memberwise initializers rather than through the wire mappers.
/// Every default here is a value the wire can legitimately carry, so nothing assembled below is a
/// payload the app would have refused — and the refusals themselves are driven against the real
/// mappers in `CustomerWireContractTests`, which is where they belong.
enum OrderFixtures {
    static func summary(id: String, statusValue: Int) -> CustomerOrderSummary {
        summary(id: id, statusCode: Code(type: "OrderStatus", name: nil, value: statusValue))
    }

    static func summary(
        id: String = "o1",
        statusCode: Code? = nil,
        displayOrderNumber: String? = nil,
        cleaningDateTime: Date? = nil,
        estimatedMinutes: Int = 0,
        address: String? = nil,
        total: Double = 0,
        currencyCode: String? = nil,
        services: [CustomerOrderLineName] = [],
        packages: [CustomerOrderLineName] = [],
        hasReview: Bool = false
    ) -> CustomerOrderSummary {
        CustomerOrderSummary(
            id: id,
            displayOrderNumber: displayOrderNumber,
            statusCode: statusCode,
            cleaningDateTime: cleaningDateTime,
            estimatedMinutes: estimatedMinutes,
            address: address,
            total: total,
            currencyCode: currencyCode,
            services: services,
            packages: packages,
            hasReview: hasReview
        )
    }

    static func detail(id: String = "o1", statusValue: Int) -> CustomerOrderDetail {
        detail(id: id, statusCode: Code(type: "OrderStatus", name: nil, value: statusValue))
    }

    static func detail(
        id: String? = "o1",
        statusCode: Code? = nil,
        displayOrderNumber: String? = nil,
        cleaningDateTime: Date? = nil,
        completedAt: Date? = nil,
        confirmationCode: String? = nil,
        receiptNumber: String? = nil,
        recurringTemplateId: String? = nil,
        address: OrderAddress? = nil,
        rooms: Int = 0,
        bathrooms: Int = 0,
        estimatedMinutes: Int = 0,
        extras: [String: Bool] = [:],
        services: [CustomerOrderService] = [],
        packages: [CustomerOrderPackage] = [],
        notes: String? = nil,
        specialInstructions: String? = nil,
        accessInstructions: String? = nil,
        total: Double = 0,
        originalSubtotal: Double = 0,
        tierDiscountAmount: Double? = nil,
        membershipDiscountAmount: Double? = nil,
        promoDiscountAmount: Double? = nil,
        appliedDiscountSource: AppliedDiscountSource? = nil,
        paymentType: Code? = nil,
        paymentStatus: Code? = nil,
        currencyCode: String? = nil,
        assignedEmployees: [AssignedEmployeeDto] = [],
        statusHistory: [OrderStatusTrackDto] = [],
        review: CustomerOrderReview? = nil,
        preferredOffer: PreferredOfferDetails? = nil,
        systemCancellationReason: String? = nil
    ) -> CustomerOrderDetail {
        CustomerOrderDetail(
            id: id,
            displayOrderNumber: displayOrderNumber,
            statusCode: statusCode,
            cleaningDateTime: cleaningDateTime,
            completedAt: completedAt,
            confirmationCode: confirmationCode,
            receiptNumber: receiptNumber,
            recurringTemplateId: recurringTemplateId,
            address: address,
            rooms: rooms,
            bathrooms: bathrooms,
            estimatedMinutes: estimatedMinutes,
            extras: extras,
            services: services,
            packages: packages,
            notes: notes,
            specialInstructions: specialInstructions,
            accessInstructions: accessInstructions,
            systemCancellationReason: systemCancellationReason,
            total: total,
            originalSubtotal: originalSubtotal,
            tierDiscountAmount: tierDiscountAmount,
            membershipDiscountAmount: membershipDiscountAmount,
            promoDiscountAmount: promoDiscountAmount,
            appliedDiscountSource: appliedDiscountSource,
            paymentType: paymentType,
            paymentStatus: paymentStatus,
            currencyCode: currencyCode,
            assignedEmployees: assignedEmployees,
            statusHistory: statusHistory,
            review: review,
            preferredOffer: preferredOffer
        )
    }

    static func service(
        id: String? = nil,
        name: String? = nil,
        description: String? = nil,
        estimatedMinutes: Int = 0,
        translations: [String: Translation]? = nil
    ) -> CustomerOrderService {
        CustomerOrderService(
            id: id,
            name: name,
            description: description,
            estimatedMinutes: estimatedMinutes,
            translations: translations
        )
    }

    static func package(
        id: String? = nil,
        name: String? = nil,
        description: String? = nil,
        price: Double = 0,
        estimatedMinutes: Int = 0,
        currencyCode: String? = nil,
        includedServices: [String] = [],
        translations: [String: Translation]? = nil
    ) -> CustomerOrderPackage {
        CustomerOrderPackage(
            id: id,
            name: name,
            description: description,
            price: price,
            estimatedMinutes: estimatedMinutes,
            currencyCode: currencyCode,
            includedServices: includedServices,
            translations: translations
        )
    }

    static func quote(
        tier: CancellationTier,
        fee: Double = 0,
        refund: Double = 1000,
        forfeitsExpressWaiver: Bool = false
    ) -> CancellationQuote {
        CancellationQuote(
            tier: tier,
            feeAmount: fee,
            refundAmount: refund,
            currencyCode: "CZK",
            forfeitsExpressWaiver: forfeitsExpressWaiver
        )
    }

    static func photos(
        _ photos: [GetOrderPhotosOrderPhotoDto] = [],
        before: Int = 0,
        after: Int = 0
    ) -> OrderPhotos {
        OrderPhotos(photos: photos, beforeCount: before, afterCount: after)
    }

    static func track(statusValue: Int, createdOn: Date) -> OrderStatusTrackDto {
        OrderStatusTrackDto(
            status: Code(type: "OrderStatus", name: nil, value: statusValue),
            createdOn: createdOn
        )
    }
}
