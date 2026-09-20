import Foundation

/// One crew member's acceptance of the contract for work, as the customer's detail states it.
struct WorkContractAcceptanceLine: Equatable, Identifiable {
    let id: String
    /// The crew entry's name as the server masked it; nil when it sent none.
    let cleanerName: String?
    let acceptedOn: Date
    let documentVersion: String
}

extension CustomerOrderDetail {
    /// The acceptance carries no name; the crew entry whose id is the acceptance's seat does, already
    /// masked for the customer's eyes. Pairing them here keeps one masking path. An acceptance naming
    /// no current seat is dropped — a line that cannot say who says nothing the crew card does not.
    func workContractAcceptanceLines() -> [WorkContractAcceptanceLine] {
        workContractAcceptances.compactMap { acceptance in
            guard let seat = assignedEmployees.first(where: { $0.id == acceptance.orderEmployeeId }) else {
                return nil
            }
            return WorkContractAcceptanceLine(
                id: acceptance.id,
                cleanerName: seat.fullName.flatMap { $0.isBlank ? nil : $0 },
                acceptedOn: acceptance.acceptedOn,
                documentVersion: acceptance.documentVersion
            )
        }
    }
}
