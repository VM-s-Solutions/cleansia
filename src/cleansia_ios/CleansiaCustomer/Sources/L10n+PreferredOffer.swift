import Foundation

extension L10n {
    enum PreferredOffer {
        static var sectionTitle: String {
            localized("preferred_offer_section_title")
        }

        static func askedTitle(_ name: String) -> String {
            format("preferred_offer_asked_title", name)
        }

        static func askedBody(_ respondBy: String) -> String {
            format("preferred_offer_asked_body", respondBy)
        }

        static func acceptedTitle(_ name: String) -> String {
            format("preferred_offer_accepted_title", name)
        }

        static var closedTitle: String {
            localized("preferred_offer_closed_title")
        }

        static var closedBody: String {
            localized("preferred_offer_closed_body")
        }
    }
}
