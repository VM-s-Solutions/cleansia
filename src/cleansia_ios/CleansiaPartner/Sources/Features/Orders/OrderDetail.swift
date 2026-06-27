import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct OrderDetail: Equatable {
    let id: String
    let orderNumber: String
    let status: OrderStatus?
    let cleaningDateTime: Date?
    let pay: Double?
    let currencyCode: String?
    let currencySymbol: String?

    let address: OrderDetailAddress?
    let coordinate: Coordinate?
    let customerName: String?
    let customerPhone: String?

    let rooms: Int
    let bathrooms: Int
    let services: [String]
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

struct OrderDetailAddress: Equatable {
    let street: String?
    let city: String?
    let zipCode: String?

    var singleLine: String? {
        let parts = [street, city, zipCode]
            .compactMap { $0?.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: ", ")
    }
}

struct OrderDetailPackage: Equatable {
    let name: String
    let price: Double?
}

struct OrderDetailPayment: Equatable {
    let subtotal: Double?
    let total: Double?
    let tierDiscount: Double?
    let membershipDiscount: Double?
    let promoDiscount: Double?
    let methodName: String?
    let statusName: String?

    var hasBreakdown: Bool {
        guard let subtotal, let total else { return false }
        return subtotal != total
    }
}

extension OrderDetail {
    /// Whether the full-bleed map is shown: coords present AND the order is not
    /// Cancelled (the visit never happened) — the `canShowMap` parity
    /// (OrderDetailScreen.kt:165).
    var canShowMap: Bool {
        coordinate != nil && status != ._6
    }
}

extension OrderDetail {
    init(_ item: OrderItem) {
        id = item.id ?? ""
        orderNumber = item.displayOrderNumber ?? item.id?.prefix(8).description ?? "—"
        status = item.status
        cleaningDateTime = item.cleaningDateTime
        pay = item.estimatedCleanerPay
        currencyCode = item.currency?.code ?? item.currency?.symbol
        currencySymbol = item.currency?.symbol

        if let address = item.address {
            self.address = OrderDetailAddress(street: address.street, city: address.city, zipCode: address.zipCode)
            if let lat = address.latitude, let lon = address.longitude {
                coordinate = Coordinate(latitude: lat, longitude: lon)
            } else {
                coordinate = nil
            }
        } else {
            address = nil
            coordinate = nil
        }
        customerName = item.customerName
        customerPhone = item.customerPhone

        rooms = item.rooms ?? 0
        bathrooms = item.bathrooms ?? 0
        services = item.selectedServices?.compactMap { service in
            service.name.flatMap { $0.isEmpty ? nil : $0 }
        } ?? []
        packages = item.selectedPackages?.map { pkg in
            OrderDetailPackage(name: pkg.name.flatMap { $0.isEmpty ? nil : $0 } ?? "—", price: pkg.price)
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
            methodName: item.paymentType?.name,
            statusName: item.paymentStatus?.name
        )

        isAssignedToCurrentUser = item.isAssignedToCurrentUser ?? false
        hasAfterPhotos = item.hasAfterPhotos ?? false

        orderNotes = item.orderNotes ?? []
        orderIssues = item.orderIssues ?? []
        statusHistory = item.statusHistory ?? []
    }
}
