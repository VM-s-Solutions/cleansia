import CleansiaCore
import Foundation

/// One card of the home smart-upsell carousel (the `UpsellSlide` model in
/// `HomeTab.kt:426-500`). Pure data — the view maps `action` onto the HomeTab
/// callbacks, so the predicate/order/content logic stays unit-testable.
struct UpsellSlide: Equatable, Identifiable {
    enum Kind: Equatable {
        case plus
        case setupRecurring
        case welcome
        case referral
        case book
    }

    enum Action: Equatable {
        case subscribePlus
        case setupRecurring
        case book
        case openReferral
    }

    let kind: Kind
    let top: String
    let title: String
    let cta: String
    let gradient: BrandGradient
    let mascot: Mascot
    let action: Action

    var id: Kind {
        kind
    }

    /// The Android `buildList` — order matters: most-relevant first so the
    /// slide on screen at t=0 is the one the user is most likely to act on.
    static func slides(isPlus: Bool, hasAnyOrders: Bool, showSetupRecurring: Bool) -> [UpsellSlide] {
        var slides: [UpsellSlide] = []
        if !isPlus {
            slides.append(UpsellSlide(
                kind: .plus,
                top: L10n.Home.upsellPlusTop,
                title: L10n.Home.upsellPlusTitle,
                cta: L10n.Home.upsellPlusCta,
                gradient: .plusHero,
                mascot: .ready,
                action: .subscribePlus
            ))
        }
        if showSetupRecurring {
            slides.append(UpsellSlide(
                kind: .setupRecurring,
                top: L10n.Home.upsellSetupRecurringTop,
                title: L10n.Home.upsellSetupRecurringTitle,
                cta: L10n.Home.upsellSetupRecurringCta,
                gradient: .purple,
                mascot: .idea,
                action: .setupRecurring
            ))
        }
        if !hasAnyOrders {
            slides.append(UpsellSlide(
                kind: .welcome,
                top: L10n.Home.upsellWelcomeTop,
                title: L10n.Home.upsellWelcomeTitle,
                cta: L10n.Home.upsellWelcomeCta,
                gradient: .purple,
                mascot: .mopping,
                action: .book
            ))
        }
        slides.append(UpsellSlide(
            kind: .referral,
            top: L10n.Home.upsellReferralTop,
            title: L10n.Home.upsellReferralTitle,
            cta: L10n.Home.upsellReferralCta,
            gradient: .cyan,
            mascot: .cleaning,
            action: .openReferral
        ))
        slides.append(UpsellSlide(
            kind: .book,
            top: L10n.Home.heroGreeting,
            title: L10n.Home.heroPrompt,
            cta: L10n.Home.heroCta,
            gradient: .blue,
            mascot: .cleaning,
            action: .book
        ))
        return slides
    }
}
