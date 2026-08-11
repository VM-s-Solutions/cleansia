import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct OrderDetail: Equatable {
    let id: String
    let orderNumber: String
    let status: OrderStatus?
    let cleaningDateTime: Date?
    /// The authoritative completion stamp (DB column, written inside the domain);
    /// the timer hero anchors the job duration on it instead of the wall clock.
    let completedAt: Date?
    let pay: Double?
    let currencyCode: String?
    let currencySymbol: String?

    let location: OrderLocation
    let customerName: String?
    let customerPhone: String?

    let rooms: Int
    let bathrooms: Int
    let crew: OrderCrew?
    let services: [OrderDetailService]
    let packages: [OrderDetailPackage]
    let extras: [String]

    let customerNotes: String?
    let specialInstructions: String?
    let accessInstructions: String?

    let payment: OrderDetailPayment

    let isAssignedToCurrentUser: Bool
    let hasAfterPhotos: Bool

    let orderNotes: [OrderNoteDto]
    let orderIssues: [OrderIssueDto]
    let statusHistory: [OrderStatusTrackDto]
}

/// Name + the stable backend id: the checklist persists ticks under id-based keys
/// (Android parity — order-independent), and the id is optional only because the
/// wire marks it nullable; a nil id falls back to name-keying.
struct OrderDetailService: Equatable, Hashable {
    let id: String?
    let name: String
    let translations: [String: Translation]?

    init(id: String?, name: String, translations: [String: Translation]? = nil) {
        self.id = id
        self.name = name
        self.translations = translations
    }
}

struct OrderDetailPackage: Equatable, Hashable {
    let id: String?
    let name: String
    let price: Double?
    let translations: [String: Translation]?

    init(id: String?, name: String, price: Double?, translations: [String: Translation]? = nil) {
        self.id = id
        self.name = name
        self.price = price
        self.translations = translations
    }
}

/// Backend `PaymentType` / `PaymentStatus` wire values (the enum ordinals the
/// `Code.value` envelope carries), the surrogate for the missing generated Swift
/// enums the cash-collection gate and the payment labels compare against.
enum PaymentTypeCode: Int, CaseIterable {
    case cash = 1
    case card = 2
}

enum PaymentStatusCode: Int, CaseIterable {
    case pending = 1
    case paid = 2
    case failed = 3
    case refunded = 4
    case disputed = 5
    case partiallyRefunded = 6
}

struct OrderDetailPayment: Equatable {
    let subtotal: Double?
    let total: Double?
    let tierDiscount: Double?
    let membershipDiscount: Double?
    let promoDiscount: Double?
    let typeCode: Int?
    let statusCode: Int?

    var hasBreakdown: Bool {
        guard let subtotal, let total else { return false }
        return subtotal != total
    }

    var isCash: Bool {
        typeCode == PaymentTypeCode.cash.rawValue
    }

    var isSettled: Bool {
        statusCode == PaymentStatusCode.paid.rawValue
    }
}

extension OrderDetail {
    /// Where the full-bleed map centres, or nil when there is nothing precise to point at — a
    /// browsing cleaner who only got the coarse zone, an order that predates the geocoding
    /// backfill, or a cancelled visit that never happened.
    var mapCoordinate: Coordinate? {
        location.mapPoint(status: status)
    }

    /// The cleaner is on this job and the job is live (Confirmed / OnTheWay / InProgress): the work
    /// tools show, and the photo rails with them. It is also the gate on the photo FETCH, because
    /// `GetOrderPhotos` serves only a caller the strict access check admits and every photo is a
    /// forwardable signed URL into a private home. One value for both, so a change to what is on
    /// screen cannot leave behind a request nobody makes — the property Android gets structurally by
    /// creating the photos view model inside the section itself.
    var showsWorkSections: Bool {
        isAssignedToCurrentUser && (status == ._2 || status == ._3 || status == ._4)
    }

    /// The fields `OrderPiiRedaction` blanks by caller class are rendered off their **own arrival**.
    /// The server decides disclosure on `CanAccessOrderAsync`; `isAssignedToCurrentUser` counts the
    /// assignment list. They disagree for the employee who booked this cleaning for their own home,
    /// and gating the render on the flag hides that person's own data from them. A blank is a
    /// redaction, not a value: the server sends `""` and `[]`, so the test is never `!= nil`.
    var showsCustomerContact: Bool {
        !(customerPhone ?? "").isBlank
    }

    /// The lifecycle conjunct answers *when is a door code useful*, not *may this caller see it*, so
    /// it stays — without it the code sits on screen forever on a finished job.
    var showsAccessCard: Bool {
        !(accessInstructions ?? "").isBlank && (status == ._3 || status == ._4)
    }

    /// The record of what was reported during the job outlives the job, so there is no lifecycle
    /// term here — only the arrival of the record, or a live invitation to start one.
    var showsNotesAndIssues: Bool {
        !orderNotes.isEmpty || !orderIssues.isEmpty || canAddNotes
    }

    /// Writing is an action, so it fails closed on the assignment, and only while the job is under
    /// way — nothing is added before the cleaner is on their way.
    var canAddNotes: Bool {
        isAssignedToCurrentUser && (status == ._3 || status == ._4)
    }

    /// The formatted sum the cleaner takes in cash, or nil when the wire carried no
    /// total — the cash-collection confirmation then asks without naming an amount
    /// rather than guessing one.
    var cashDueLabel: String? {
        guard let total = payment.total, total > 0 else { return nil }
        return OrdersFormat.money(total, symbol: currencySymbol)
    }
}

extension OrderDetail {
    init(_ item: OrderItem) {
        id = item.id ?? ""
        orderNumber = item.displayOrderNumber ?? item.id?.prefix(8).description ?? "—"
        status = item.status
        cleaningDateTime = item.cleaningDateTime
        completedAt = item.completedAt
        pay = item.estimatedCleanerPay
        currencyCode = item.currency?.code ?? item.currency?.symbol
        currencySymbol = item.currency?.symbol

        location = OrderLocation(item)
        customerName = item.customerName
        customerPhone = item.customerPhone

        rooms = item.rooms ?? 0
        bathrooms = item.bathrooms ?? 0
        crew = OrderCrew(item)
        services = item.selectedServices?.compactMap { service in
            service.name.flatMap { $0.isEmpty ? nil : $0 }
                .map { OrderDetailService(id: service.id, name: $0, translations: service.translations) }
        } ?? []
        packages = item.selectedPackages?.map { pkg in
            OrderDetailPackage(
                id: pkg.id,
                name: pkg.name.flatMap { $0.isEmpty ? nil : $0 } ?? "—",
                price: pkg.price,
                translations: pkg.translations
            )
        } ?? []
        extras = item.extras?.filter(\.value).keys.sorted() ?? []

        customerNotes = item.notes
        specialInstructions = item.specialInstructions
        accessInstructions = item.accessInstructions

        payment = OrderDetailPayment(
            subtotal: item.originalSubtotal,
            total: item.totalPrice,
            tierDiscount: item.tierDiscountAmount,
            membershipDiscount: item.membershipDiscountAmount,
            promoDiscount: item.promoDiscountAmount,
            typeCode: item.paymentType?.value,
            statusCode: item.paymentStatus?.value
        )

        isAssignedToCurrentUser = item.isAssignedToCurrentUser ?? false
        hasAfterPhotos = item.hasAfterPhotos ?? false

        orderNotes = item.orderNotes ?? []
        orderIssues = item.orderIssues ?? []
        statusHistory = item.statusHistory ?? []
    }
}
