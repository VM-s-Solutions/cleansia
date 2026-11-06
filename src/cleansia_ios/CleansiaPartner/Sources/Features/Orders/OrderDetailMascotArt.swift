import CleansiaCore
import CleansiaPartnerApi

/// Which mascot stands on the detail's band, by the state it shows: leaning over a job not taken, waving at one
/// asked for her and at Confirmed, ready on the way, resting once a lockout is reported, on a cancelled job and on
/// a page whose read failed, the cleaning loop while she cleans (its still under Reduce Motion), then the slip for a
/// cash job or the spray and cloth while finishing, the thumbs-up once done.
enum OrderDetailMascotArt {
    static func art(
        for status: OrderStatus?,
        isMine: Bool = true,
        offered: Bool = false,
        reported: Bool = false,
        cash: Bool = false,
        failed: Bool = false,
        cleaning: Bool = false,
        reduceMotion: Bool = false
    ) -> MascotArt? {
        if status == ._6 || failed { return .still(.resting) }
        guard isMine else { return .still(offered ? .waving : .leaning) }
        switch status {
        case ._0, ._1: return .still(.leaning)
        case ._2: return .still(.waving)
        case ._3: return .still(reported ? .resting : .ready)
        case ._4 where cleaning:
            return reduceMotion ? .still(.cleaning) : .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        case ._4: return .still(cash ? .invoice : .sprayAndCloth)
        default: return .still(.thumbsUp)
        }
    }
}
