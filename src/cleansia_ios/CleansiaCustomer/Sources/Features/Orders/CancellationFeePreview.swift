import Foundation

enum CancellationFeeTier: Equatable {
    case oops
    case free
    case half(refund: Double)
    case full
    case neutral
}

enum CancellationFeePreview {
    /// Client-side estimate of the cancellation tier, mirroring
    /// `CancelOrderSheet.kt:344-404`. The backend recomputes authoritatively on
    /// submit — this only drives the sheet's preview copy. nil timestamps →
    /// `.neutral` (no numeric claim we can't substantiate).
    static func tier(
        cleaningAt: Date?,
        createdAt: Date?,
        totalPrice: Double,
        now: Date
    ) -> CancellationFeeTier {
        guard let cleaningAt, let createdAt else { return .neutral }
        let hoursUntilStart = cleaningAt.timeIntervalSince(now) / 3600
        let minutesSinceBooking = now.timeIntervalSince(createdAt) / 60
        if minutesSinceBooking <= 15 {
            return .oops
        }
        if hoursUntilStart >= 24 {
            return .free
        }
        if hoursUntilStart >= 4 {
            return .half(refund: totalPrice * 0.5)
        }
        return .full
    }
}
