import CleansiaCore
import Foundation

/// One card of the home smart-upsell carousel (the `UpsellSlide` model in
/// `HomeTab.kt:426-500`). Pure data — the view maps `action` onto the HomeTab
/// callbacks, so the predicate/order/content logic stays unit-testable.
struct UpsellSlide: Equatable, Identifiable {
    enum Kind: Equatable {
        case plus
        case setupRecurring
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
    /// `plusTrialDays` is the free trial this customer can still get, 0 for none.
    static func slides(isPlus: Bool, plusTrialDays: Int = 0, showSetupRecurring: Bool) -> [UpsellSlide] {
        var slides: [UpsellSlide] = []
        if !isPlus {
            let offersTrial = plusTrialDays > 0
            slides.append(UpsellSlide(
                kind: .plus,
                top: L10n.Home.upsellPlusTop,
                title: offersTrial ? L10n.Home.upsellPlusTitleTrial(plusTrialDays) : L10n.Home.upsellPlusTitle,
                cta: offersTrial ? L10n.Home.upsellPlusCtaTrial : L10n.Home.upsellPlusCta,
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

/// The loop's page arithmetic. With n > 1 slides the pager shows n + 2 pages — a clone of the last slide
/// before the first and a clone of the first after the last — so a swipe past either end lands on a
/// clone that is pixel-identical to where it then jumps. Android loops over a bounded run of virtual
/// pages instead; TabView's page style is not lazy, so hundreds of pages would all be built.
extension UpsellSlide {
    static func pageCount(slides count: Int) -> Int {
        count > 1 ? count + 2 : count
    }

    /// The slide a page shows: page 0 is the last slide's clone, page n + 1 the first's.
    static func logicalIndex(page: Int, count: Int) -> Int {
        guard count > 1 else { return 0 }
        return ((page - 1) % count + count) % count
    }

    /// The page showing slide `logical`.
    static func page(logical: Int, count: Int) -> Int {
        count > 1 ? logical + 1 : 0
    }

    /// The real page a settled clone stands for; nil on a real page, which stays where it is.
    static func reanchor(page: Int, count: Int) -> Int? {
        guard count > 1 else { return nil }
        if page <= 0 { return count }
        if page >= count + 1 { return 1 }
        return nil
    }

    /// The page to show when the slide count changes, keeping the slide on screen — or the last one,
    /// when the slide on screen is the one that left.
    static func page(afterCountChangeFrom page: Int, oldCount: Int, newCount: Int) -> Int {
        let logical = logicalIndex(page: page, count: oldCount)
        return self.page(logical: min(logical, max(newCount - 1, 0)), count: newCount)
    }
}
