import CleansiaCore
import CleansiaPartnerApi

/// Which mascot rides the sheet edge, per order status — Android's `FloatingMascot.mascotForStatus`,
/// including its rule that InProgress is the only status that animates and that Cancelled shows
/// nothing at all. Completed, and anything unmapped, gets the thumbs-up.
///
/// Confirmed uses `waving`: Android names it `mascot_waving_friendly`, which is a byte-identical
/// copy of its `mascot_waving` — the same art under two names, not a substitution.
enum OrderDetailMascotArt {
    static func art(for status: OrderStatus?) -> MascotArt? {
        switch status {
        case ._0, ._1: .still(.leaning)
        case ._2: .still(.waving)
        case ._3: .still(.sprayAndCloth)
        case ._4: .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        case ._6: nil
        default: .still(.thumbsUp)
        }
    }
}
