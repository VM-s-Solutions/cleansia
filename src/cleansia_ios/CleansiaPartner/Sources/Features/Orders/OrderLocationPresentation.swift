import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// What the order's location resolved to for *this* caller.
///
/// The server nulls `address` for a caller the order does not belong to and sends the coarse
/// `customerAddressApproximate` zone to everyone, so the arrival of a street address is the
/// discriminator. It is deliberately not `isAssignedToCurrentUser`: an employee who booked a
/// cleaning for their own home reaches this screen as the order's *customer*, with a full address
/// and that flag false, and gating on it would hide their own data from them — a second
/// authorization implementation next to the server's.
enum OrderLocation: Equatable {
    struct Precise: Equatable {
        let line: String
        let coordinate: Coordinate?
    }

    case precise(Precise)
    /// City + ZIP prefix — enough to judge the travel, nothing to navigate to.
    case approximate(String)
    case none

    var line: String? {
        switch self {
        case let .precise(precise): precise.line
        case let .approximate(zone): zone
        case .none: nil
        }
    }

    /// Navigation needs a door, not a district.
    var navigationTarget: Precise? {
        guard case let .precise(precise) = self else { return nil }
        return precise
    }

    /// Where the map backdrop centres, or nil when there is nothing precise to point at.
    ///
    /// Status does not enter into it. A cancelled order used to be forced to nil here on the
    /// reasoning that the visit never happened — but the job still had a place, and suppressing
    /// the map read as a map that had failed to load rather than as a decision. Owner ruling,
    /// 2026-08-27. Entitlement is still enforced upstream by `OrderPiiRedaction`, which withholds
    /// the address itself from a cleaner who has not taken the job, in every status.
    func mapPoint() -> Coordinate? {
        navigationTarget?.coordinate
    }
}

extension OrderLocation {
    init(_ item: OrderItem) {
        if let street = Self.streetLine(item.address) {
            self = .precise(Precise(line: street, coordinate: Self.coordinate(item.address)))
        } else if let zone = item.customerAddressApproximate, !zone.isBlank {
            // BuildApproximateAddress sends "" — not null — for an order with no city.
            self = .approximate(zone)
        } else {
            self = .none
        }
    }

    private static func streetLine(_ address: OrderAddress?) -> String? {
        let parts = [address?.street, address?.city, address?.zipCode]
            .compactMap { $0?.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
        return parts.isEmpty ? nil : parts.joined(separator: ", ")
    }

    private static func coordinate(_ address: OrderAddress?) -> Coordinate? {
        guard let latitude = address?.latitude, let longitude = address?.longitude else { return nil }
        return Coordinate(latitude: latitude, longitude: longitude)
    }
}
