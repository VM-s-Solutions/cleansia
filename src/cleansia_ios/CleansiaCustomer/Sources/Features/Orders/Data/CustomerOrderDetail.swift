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

struct CustomerOrderPackage: Equatable, Hashable {
    let id: String?
    let name: String?
    let description: String?
    let price: Double
    let estimatedMinutes: Int
    let currencyCode: String?
    let includedServices: [String]
    let translations: [String: Translation]?
}

/// A review the customer left. `rating` is the whole card — a coerced `0` draws five empty stars over
/// their own comment, which reads as a one-star verdict they never gave.
struct CustomerOrderReview: Equatable {
    let rating: Int
    let comment: String?
}

/// The order detail as the screen renders it, with the mobile API contract re-asserted at the
/// boundary so no view is left guessing what a missing number meant.
///
/// **Refuse the money and the scope.** One order, so there is no page to refuse and no row to drop.
/// `totalPrice` and `originalSubtotal` are what the customer agreed to pay and the figure it was
/// struck through from; the breakdown card prints them next to each other, so a coerced `0` renders
/// "0 Kč" under a struck-through 2 100 and nothing anywhere goes red. `rooms` and `bathrooms` are the
/// scope the price was quoted against, and a package line's `price` is an addend of that same total.
///
/// **`estimatedTime` is refused too, and that is the judgement call in here.** On the screens it is a
/// rendered absence: `OrdersFormat.dateRange` drops the end of the window, the details row and the
/// in-progress subhead render "—", and the progress bar resolves to nil — `0` and absent are the same
/// fact and coercing it would cost nothing. `EtaWindow.forOrder` is where that stops being true: it
/// takes `max(minutes, 1)`, so an absent estimate becomes a **one-minute cleaning** whose Live
/// Activity counts down to an end a minute after the appointment starts. One field, one decision, and
/// the reader that fabricates is the one that decides it.
///
/// **Identity is NOT refused, and that is a ruling rather than an omission.** This screen is routed
/// with the order id and keeps it, so a null `id` has an equally authoritative replacement to hand;
/// refusing would blank a screen we can navigate perfectly. The partner detail refuses its id because
/// it has no such second source.
///
/// **Collections keep `?? []`.** An absent list and an empty one render identically here and falsify
/// no arithmetic: nothing on this screen sums the lines, because the total, the subtotal and the
/// three discount amounts each arrive as their own field.
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
        translations = details.translations
    }
}

extension CustomerOrderReview {
    init(_ dto: OrderReviewDto) throws {
        rating = try dto.rating.require("rating")
        comment = dto.comment
    }
}
