import Foundation

/// The chips a customer may attach to a review. Mirrors the backend `ReviewTag`, whose INTEGER is the
/// wire contract — positive tags occupy 1...10 and negative 11...20, banded so a later insert never
/// renumbers a shipped value.
///
/// **Declared here rather than used from the generated client**, matching Android and matching how this
/// app treats every other closed set: the app-side type carries the polarity rule and the label
/// mapping, and the boundary converts. `Int` raw values ARE the wire values, so the conversion is the
/// identity and there is no hand-written table to fall out of step with the server.
///
/// There is deliberately no damage tag. Damage is a dispute reason and sits on the money path
/// (ADR-0006, ADR-0009) — a chip would give the customer the feeling of having reported it with none of
/// the mechanism.
enum CustomerReviewTag: Int, CaseIterable, Equatable {
    case onTime = 1
    case thorough = 2
    case friendly = 3
    case carefulWithBelongings = 4
    case extrasDoneWell = 5
    case followedInstructions = 6
    case greatPhotos = 7

    case arrivedLate = 11
    case missedAreas = 12
    case feltRushed = 13
    case extraNotDone = 14
    case didNotFollowInstructions = 15
    case unprofessional = 16
    case smellOrProducts = 17
    case crewSmallerThanBooked = 18

    /// The lowest rating that offers the positive set — mirrors `ReviewTagPolarity`.
    static let positiveRatingFloor = 4

    /// The server refuses more than this, so the sheet stops offering at it.
    static let maxTags = 4

    private static let negativeBandStart = 11

    var isPositive: Bool { rawValue < Self.negativeBandStart }

    /// The set to offer for `rating`; empty outside 1...5.
    static func forRating(_ rating: Int) -> [CustomerReviewTag] {
        guard (1 ... 5).contains(rating) else { return [] }
        let wantPositive = rating >= positiveRatingFloor
        return allCases.filter { $0.isPositive == wantPositive }
    }
}

extension CustomerReviewTag {
    /// The localized chip label. Lives beside the type so a view never reaches for a raw key.
    var label: String { L10n.OrderReview.tag(self) }
}
