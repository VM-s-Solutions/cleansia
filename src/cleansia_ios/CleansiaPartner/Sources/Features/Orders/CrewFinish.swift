import Foundation

/// Where the cleaners on a job stand on finishing it (owner ruling 2026-10-26): with two or more on it, each
/// marks their own part done, and once every one still on it has, any of them completes it.
enum CrewFinish: Equatable {
    /// One cleaner on the job: their Complete closes it, as it always has.
    case solo
    /// The caller has not marked their part done.
    case myPartOpen
    /// The caller has; someone else on the job has not.
    case waitingForCrew
    /// Everyone on the job has marked their part done.
    case everyoneDone
}

extension OrderDetail {
    /// Read off the seats the server sends the crew. Until the caller's own id is known their seat cannot be
    /// found, and marking again keeps the first time, so an unfinished crew then reads as the caller's part
    /// still open.
    func crewFinish(myEmployeeId: String?) -> CrewFinish {
        guard seats.count >= 2 else { return .solo }
        if seats.allSatisfy({ $0.completionConfirmedAt != nil }) { return .everyoneDone }
        let mine = seats.first { $0.employeeId == myEmployeeId }
        return mine?.completionConfirmedAt == nil ? .myPartOpen : .waitingForCrew
    }
}
