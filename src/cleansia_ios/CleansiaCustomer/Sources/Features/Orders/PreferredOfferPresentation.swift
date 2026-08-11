import CleansiaCustomerApi
import Foundation

/// What the customer is told about the cleaner they asked for.
///
/// Three states, and deliberately not a fourth. The server knows why a reservation ended and does not
/// say — a decline and a silence are the same `Closed` here and the same sentence on screen — because
/// reporting it would tell one person what another person did.
enum PreferredOfferDisclosure: Equatable {
    case asked(cleanerName: String, respondBy: Date)
    case accepted(cleanerName: String)
    case closed
}

enum PreferredOfferPresentation {
    /// The block is scoped to a booking that is still looking for a cleaner. Every sentence in it is
    /// about one, and the closed sentence makes a forward-looking claim about the platform — "now open
    /// to our whole team" — that a cancelled booking contradicts and a finished one outlived. The
    /// reservation state itself carries no order status, so the narrowing is made here.
    ///
    /// A state whose sentence cannot be completed is not disclosed at all: "We've asked " is worse than
    /// silence, and the sentence is the whole feature.
    static func disclosure(for order: OrderItem) -> PreferredOfferDisclosure? {
        guard OrderStatusGroup.isUpcoming(order.status), let offer = order.preferredOffer else {
            return nil
        }
        switch offer.state {
        case ._1:
            guard let name = named(offer.cleanerName), let respondBy = offer.respondByUtc else { return nil }
            return .asked(cleanerName: name, respondBy: respondBy)
        case ._2:
            guard let name = named(offer.cleanerName) else { return nil }
            return .accepted(cleanerName: name)
        case ._3:
            return .closed
        case ._0, .none:
            return nil
        }
    }

    private static func named(_ value: String?) -> String? {
        guard let value, !value.isBlank else { return nil }
        return value
    }
}

extension PreferredOfferDisclosure {
    var systemImage: String {
        switch self {
        case .asked: "clock"
        case .accepted: "checkmark.seal"
        case .closed: "person.2"
        }
    }

    var headline: String {
        switch self {
        case let .asked(name, _): L10n.PreferredOffer.askedTitle(name)
        case let .accepted(name): L10n.PreferredOffer.acceptedTitle(name)
        case .closed: L10n.PreferredOffer.closedTitle
        }
    }

    /// The reservation's END, stated as an instant and never counted down. The hold's real expiry is the
    /// server's, so a remaining-time label on a screen left open drifts; and it is the end of the ask,
    /// never a time by which anyone will be assigned — nothing in a pull board can keep that promise.
    ///
    /// The accepted state has no second line on purpose: the cleaner took the job, and everything else
    /// worth saying about it is on the assigned-cleaners card.
    func detail(locale: Locale) -> String? {
        switch self {
        case let .asked(_, respondBy):
            L10n.PreferredOffer.askedBody(OrdersFormat.dateTime(respondBy, locale: locale))
        case .accepted:
            nil
        case .closed:
            L10n.PreferredOffer.closedBody
        }
    }
}
