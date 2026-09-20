import CleansiaPartnerApi
import Foundation

/// Where the signed-in cleaner stands on this job's contract for work.
///
/// The caller's row is the acceptance whose `orderEmployeeId` is their own crew entry's id — the
/// server sends the acceptances without names, and the crew entry is where the (already masked)
/// identity lives. A crew entry with no matching acceptance IS the pending state: the only way onto a
/// crew without accepting is an administrator's placement.
enum WorkContractStanding: Equatable {
    case none
    /// On the crew, no acceptance for the seat, and the job is not over — the banner.
    case pending
    case accepted(acceptanceId: String, acceptedOn: Date, documentVersion: String)
}

extension OrderDetail {
    /// Mirrors `OrderAvailability.OfferableStatuses`: the statuses under which the standalone
    /// acceptance is allowed.
    private static let acceptableStatuses: Set<OrderStatus> = [._0, ._2, ._3, ._4]

    func workContractStanding(myEmployeeId: String?) -> WorkContractStanding {
        guard let myEmployeeId, let seat = seats.first(where: { $0.employeeId == myEmployeeId }) else {
            return .none
        }
        if let row = workContractAcceptances.first(where: { $0.orderEmployeeId == seat.id }) {
            return .accepted(acceptanceId: row.id, acceptedOn: row.acceptedOn, documentVersion: row.documentVersion)
        }
        guard let status, Self.acceptableStatuses.contains(status) else { return .none }
        return .pending
    }
}
