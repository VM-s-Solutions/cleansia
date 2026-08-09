import Foundation

/// Mirrors `Cleansia.Core.Domain.Orders.JobProximity`; the server refuses anything outside the range
/// with `employee.job_radius_out_of_range`.
enum JobRadiusBounds {
    static let minimumKm = 1
    static let maximumKm = 500

    /// Where the slider starts for a cleaner who has never set one. Never written without a tap —
    /// an unset radius stays unset, because a number nobody chose would quietly shrink their digest.
    static let startingKm = 25

    static func clamp(_ value: Int) -> Int {
        min(max(value, minimumKm), maximumKm)
    }
}

/// The radius narrows the "new jobs near you" digest and nothing else — not the board, not the take
/// gate. `anywhere` is a first-class choice and carries no distance at all — never a zero, which the
/// server refuses as out of range.
enum JobRadiusSelection: Equatable {
    case anywhere
    case within(Int)

    init(radiusKm: Int?) {
        self = radiusKm.map { .within(JobRadiusBounds.clamp($0)) } ?? .anywhere
    }

    var wireValue: Int? {
        switch self {
        case .anywhere: nil
        case let .within(kilometres): kilometres
        }
    }
}

struct JobRadiusForm: Equatable {
    private(set) var isLimited: Bool
    private(set) var kilometres: Int

    init(radiusKm: Int?) {
        isLimited = radiusKm != nil
        kilometres = radiusKm.map(JobRadiusBounds.clamp) ?? JobRadiusBounds.startingKm
    }

    var selection: JobRadiusSelection {
        isLimited ? .within(kilometres) : .anywhere
    }

    /// The slider keeps its position while the cleaner is on "no limit", so turning the limit back on
    /// restores the distance they had rather than snapping to the floor.
    mutating func setLimited(_ limited: Bool) {
        isLimited = limited
    }

    mutating func setKilometres(_ value: Double) {
        kilometres = JobRadiusBounds.clamp(Int(value.rounded()))
    }
}
