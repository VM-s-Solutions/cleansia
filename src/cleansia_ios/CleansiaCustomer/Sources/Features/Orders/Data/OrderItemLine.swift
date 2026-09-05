import Foundation

/// One item of an order the customer can point at — for a dispute ("this part was not done
/// properly") and for a review ("this part was excellent, that one was not").
///
/// The identity is the PAIR the server uses, `(serviceId, packageId?)`. A service bought on its own
/// leaves `packageId` nil; a service that came inside a bundle carries both. That is not ceremony:
/// the same service can be on one order twice, bought alone AND inside a package, and they are
/// different lines — an admin refunding one must not refund the other.
///
/// → `CreateDispute.DisputeLineSelection`, `SubmitOrderReview.ReviewLineScore`
struct OrderItemLine: Equatable, Identifiable, Hashable {
    let serviceId: String
    let packageId: String?
    /// What the customer reads.
    let label: String
    /// The bundle it came in, so two rows sharing a name are distinguishable. Nil when standalone.
    let packageLabel: String?

    /// Stable within one order: the server's own identity, flattened.
    var id: String { "\(packageId ?? "")|\(serviceId)" }
}

extension OrderItemLine {
    /// Every item on the order that can be named: the standalone services, then the services inside
    /// each package.
    ///
    /// Reads `includedServiceItems` and not `includedServices` — the latter is a list of names and
    /// cannot be sent back to the server. An item with no id is SKIPPED rather than shown: a row the
    /// customer can tick but the server would reject as not-on-this-order is worse than no row,
    /// because the error it produces names a box they cannot un-tick.
    static func lines(of order: CustomerOrderDetail?) -> [OrderItemLine] {
        guard let order else { return [] }

        var lines: [OrderItemLine] = []

        for service in order.services {
            guard let id = service.id else { continue }
            lines.append(
                OrderItemLine(
                    serviceId: id,
                    packageId: nil,
                    label: service.name ?? "",
                    packageLabel: nil
                )
            )
        }

        for package in order.packages {
            guard let packageId = package.id else { continue }
            let packageLabel = package.name ?? ""
            for included in package.includedServiceItems {
                guard let id = included.id else { continue }
                lines.append(
                    OrderItemLine(
                        serviceId: id,
                        packageId: packageId,
                        label: included.name ?? "",
                        packageLabel: packageLabel
                    )
                )
            }
        }

        return lines
    }
}

/// A score a customer gave to ONE item of an order.
///
/// Same identity as `OrderItemLine`, carried separately because a score is what the customer chose
/// and a line is what the order contains — one is input, the other is data.
///
/// → `SubmitOrderReview.ReviewLineScore`
struct OrderItemLineScore: Equatable, Hashable {
    let serviceId: String
    let packageId: String?
    /// 1...5. An item the customer did not score simply carries no score at all.
    let rating: Int
}
