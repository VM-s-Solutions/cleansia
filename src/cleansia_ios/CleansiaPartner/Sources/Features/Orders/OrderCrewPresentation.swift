import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// How many cleaners the job needs and whether a seat is still open — `TakeOrder`'s free-seat
/// conjunct refuses a take, and neither number is derivable client-side.
///
/// Every member is read off the wire; none is computed from another. The server sends both the
/// count and the flag from one source, and a client that reads one shape and derives the other is
/// how the two come to disagree.
enum OrderCrew: Equatable {
    case spotsOpen(crewSize: Int, openSpots: Int)
    case full(crewSize: Int)

    var crewSize: Int {
        switch self {
        case let .spotsOpen(crewSize, _): crewSize
        case let .full(crewSize): crewSize
        }
    }
}

extension OrderCrew {
    /// Nil when the wire carried no seat block at all — a server that predates it, where the crew
    /// line simply does not render and nothing is claimed. Once the block IS present the rest of it
    /// is refused rather than defaulted: `0` open seats and `false` both read as "this job is full",
    /// which is a claim about whether the cleaner can take it.
    init?(_ item: OrderItem) throws {
        guard let crewSize = item.requiredEmployees, crewSize > 0 else { return nil }
        let openSpots = try item.availableSpots.require("availableSpots")
        if try item.hasAvailableSpots.require("hasAvailableSpots"), openSpots > 0 {
            self = .spotsOpen(crewSize: crewSize, openSpots: openSpots)
        } else {
            self = .full(crewSize: crewSize)
        }
    }
}
