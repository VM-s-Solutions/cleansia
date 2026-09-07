import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// One catalog line as it was priced onto this order. The frozen snapshot name travels with whatever
/// translations rode with it so the card resolves the language at render, not at fetch.
struct CustomerOrderService: Equatable, Hashable {
    let id: String?
    let name: String?
    let description: String?
    let estimatedMinutes: Int
    let translations: [String: Translation]?
}

/// One service inside a package, addressable by id.
///
/// `includedServices` beside it is a list of NAMES — printable, but impossible to send back, so a
/// customer could read what a package contained and never point at one part of it. Both are kept:
/// the name list is what the detail card renders, this is what a dispute line or a review score is
/// built from.
struct CustomerOrderPackageService: Equatable, Hashable {
    let id: String?
    let name: String?
}

struct CustomerOrderPackage: Equatable, Hashable {
    let id: String?
    let name: String?
    let description: String?
    let price: Double
    let estimatedMinutes: Int
    let currencyCode: String?
    let includedServices: [String]
    let includedServiceItems: [CustomerOrderPackageService]
    let translations: [String: Translation]?
}

/// A review the customer left. `rating` is the whole card — a coerced `0` draws five empty stars over
/// their own comment, which reads as a one-star verdict they never gave.
struct CustomerOrderReview: Equatable {
    let rating: Int
    let comment: String?
    let tags: [CustomerReviewTag]
    /// The per-item scores this review carries. Empty on every review written before per-item
    /// scoring existed and on every review that only left an overall rating — most of them. Read on
    /// the EDIT path, so reopening the sheet shows what the customer said last time.
    let lines: [OrderItemLineScore]
}

/// The order detail as the screen renders it, with the mobile API contract re-asserted at the boundary
/// so no view is left guessing what a missing number meant.
///
/// **Refuse the money and the scope.** A coerced `0` renders "0 Kč" beside a struck-through total and
/// nothing goes red — the customer reads a price the server never quoted.
/// → /decisions/adr-0048
struct CustomerOrderDetail: Equatable {
    let id: String?
    let displayOrderNumber: String?
    let statusCode: Code?
    let cleaningDateTime: Date?
    let completedAt: Date?
    let confirmationCode: String?
    let receiptNumber: String?
    let recurringTemplateId: String?

    let address: OrderAddress?
    let rooms: Int
    let bathrooms: Int
    let estimatedMinutes: Int
    let extras: [String: Bool]

    let services: [CustomerOrderService]
    let packages: [CustomerOrderPackage]

    let notes: String?
    let specialInstructions: String?
    let accessInstructions: String?

    /// Why the PLATFORM cancelled this order, as a key the UI localises — null for every order the
    /// platform did not cancel itself.
    ///
    /// The backend populates it only when `CancelledBy` is `System`, because the same column also
    /// carries an admin's free-text note written for other staff. That gating is server-side, so
    /// nothing on this side has to decide whether the value is safe to render.
    let systemCancellationReason: String?

    let total: Double
    let originalSubtotal: Double
    let tierDiscountAmount: Double?
    let membershipDiscountAmount: Double?
    let promoDiscountAmount: Double?
    let appliedDiscountSource: AppliedDiscountSource?
    let paymentType: Code?
    let paymentStatus: Code?
    let currencyCode: String?

    let assignedEmployees: [AssignedEmployeeDto]
    let statusHistory: [OrderStatusTrackDto]
    let review: CustomerOrderReview?
    let preferredOffer: PreferredOfferDetails?

    var status: OrderStatus? {
        statusCode?.toOrderStatus()
    }

    /// The extras the customer actually chose, sorted — the dictionary carries every key the platform
    /// offers and marks the unchosen ones `false`.
    var activeExtras: [String] {
        extras.filter(\.value).keys.sorted()
    }
}

extension CustomerOrderDetail {
    init(_ item: OrderItem) throws {
        id = item.id
        displayOrderNumber = item.displayOrderNumber
        statusCode = item.orderStatus
        cleaningDateTime = item.cleaningDateTime
        completedAt = item.completedAt
        confirmationCode = item.confirmationCode
        receiptNumber = item.receiptNumber
        recurringTemplateId = item.recurringTemplateId

        address = item.address
        rooms = try item.rooms.require("rooms")
        bathrooms = try item.bathrooms.require("bathrooms")
        estimatedMinutes = try item.estimatedTime.require("estimatedTime")
        extras = item.extras ?? [:]

        services = try (item.selectedServices ?? []).map(CustomerOrderService.init)
        packages = try (item.selectedPackages ?? []).map(CustomerOrderPackage.init)

        notes = item.notes
        specialInstructions = item.specialInstructions
        accessInstructions = item.accessInstructions
        systemCancellationReason = item.systemCancellationReason

        total = try item.totalPrice.require("totalPrice")
        originalSubtotal = try item.originalSubtotal.require("originalSubtotal")
        tierDiscountAmount = item.tierDiscountAmount
        membershipDiscountAmount = item.membershipDiscountAmount
        promoDiscountAmount = item.promoDiscountAmount
        appliedDiscountSource = item.appliedDiscountSource
        paymentType = item.paymentType
        paymentStatus = item.paymentStatus
        currencyCode = item.currency?.code

        assignedEmployees = item.assignedEmployees ?? []
        statusHistory = item.statusHistory ?? []
        review = try item.review.map(CustomerOrderReview.init)
        preferredOffer = item.preferredOffer
    }
}

extension CustomerOrderService {
    init(_ details: ServiceDetails) throws {
        id = details.id
        name = details.name
        description = details.description
        estimatedMinutes = try details.estimatedTime.require("estimatedTime")
        translations = details.translations
    }
}

extension CustomerOrderPackage {
    init(_ details: PackageDetails) throws {
        id = details.id
        name = details.name
        description = details.description
        price = try details.price.require("price")
        estimatedMinutes = try details.estimatedTime.require("estimatedTime")
        currencyCode = details.currencyCode
        includedServices = details.includedServices ?? []
        // Plain map, NOT `.require(...)`: a package with no listed items is ordinary, and an absent
        // array must not refuse the whole order detail the way a missing price deliberately does.
        includedServiceItems = (details.includedServiceItems ?? []).map {
            CustomerOrderPackageService(id: $0.id, name: $0.name)
        }
        translations = details.translations
    }
}

extension CustomerOrderReview {
    init(_ dto: OrderReviewDto) throws {
        rating = try dto.rating.require("rating")
        comment = dto.comment
        // Plain map: a review with no per-item scores is ordinary, and a line missing its serviceId
        // is dropped rather than refusing the whole review.
        lines = (dto.lines ?? []).compactMap { line in
            guard let serviceId = line.serviceId else { return nil }
            return OrderItemLineScore(
                serviceId: serviceId,
                packageId: line.packageId,
                rating: line.rating ?? 0
            )
        }
        // Unknown codes are DROPPED, not refused: a newer server may name a chip this build has never
        // heard of, and losing one label is not worth failing the whole order detail over.
        tags = (dto.tags ?? []).compactMap { CustomerReviewTag(rawValue: $0.rawValue) }
    }
}
