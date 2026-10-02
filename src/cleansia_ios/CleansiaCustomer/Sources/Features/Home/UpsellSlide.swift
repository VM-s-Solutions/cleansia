import CleansiaCore
import Foundation

/// One card of the home smart-upsell carousel (the `upsellKinds` / `UpsellSlide` pair in `HomeTab.kt`).
/// Pure data — the view maps `action` onto the HomeTab callbacks, so the predicate/order/content logic
/// stays unit-testable. → /product/features
struct UpsellSlide: Equatable, Identifiable {
    enum Kind: Equatable, CaseIterable {
        case notifications
        case credit
        case express
        case setupRecurring
        case plus
        case referral
        case quickSize
    }

    enum Action: Equatable {
        case turnOnNotifications
        case book
        case setupRecurring
        case subscribePlus
        /// The CTA says "Share my code", so the card shares it once the code has loaded.
        case shareReferral(code: String)
        /// Until then it opens Rewards, where the code appears.
        case openReferral
        /// "See my price" — the quick-size card sends the size its steppers hold.
        case bookSize(rooms: Int, bathrooms: Int)
    }

    /// Everything that decides which slides show and what they say.
    struct Inputs: Equatable {
        var isPlus = false
        /// The free trial this customer can still get, 0 for none.
        var plusTrialDays = 0
        var showSetupRecurring = false
        /// Push alerts are not allowed — the system says so, re-read on every foreground.
        var notificationsOff = false
        /// The balance held in the currency Home prices in; nil when there is none to spend here.
        var credit: CustomerCredit.Balance?
        /// The server's share of an order credit may settle.
        var creditShare: Double = 0
        /// Express waivers this member has left this month; 0 when none (or not a member).
        var expressRemaining = 0
        /// The customer's referral code once it has loaded.
        var referralCode: String?
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

    /// How many slides may come before the quick-size closer, which always shows: a longer set is a
    /// longer auto-advance cycle and a row of dots nobody counts.
    static let leadingCap = 4

    /// Which slides show, in order — most relevant first, so the slide on screen at t=0 is the one the
    /// customer is most likely to act on. The first ``leadingCap`` eligible slides show, then quick-size
    /// closes the set, so there are never more than five. Referral is always eligible but is the first
    /// to give way.
    static func kinds(_ inputs: Inputs) -> [Kind] {
        var leading: [Kind] = []
        if inputs.notificationsOff { leading.append(.notifications) }
        if inputs.credit != nil { leading.append(.credit) }
        if inputs.expressRemaining > 0 { leading.append(.express) }
        if inputs.showSetupRecurring { leading.append(.setupRecurring) }
        if !inputs.isPlus { leading.append(.plus) }
        leading.append(.referral)
        return Array(leading.prefix(leadingCap)) + [.quickSize]
    }

    static func slides(_ inputs: Inputs) -> [UpsellSlide] {
        kinds(inputs).map { slide($0, inputs) }
    }

    /// Every slide draws its own mascot, so no two visible slides repeat one. Same mapping as Android.
    static func mascot(_ kind: Kind) -> Mascot {
        switch kind {
        case .notifications: .waving
        case .credit: .invoice
        case .express: .floorScrubber
        case .setupRecurring: .idea
        case .plus: .plus
        case .referral: .thumbsUp
        case .quickSize: .vacuuming
        }
    }

    static func gradient(_ kind: Kind) -> BrandGradient {
        switch kind {
        case .notifications: .orange
        case .credit: .emerald
        // Members only, so it never sits beside the Plus slide that shares the Plus hero.
        case .express, .plus: .plusHero
        case .setupRecurring: .purple
        case .referral: .cyan
        case .quickSize: .blue
        }
    }

    private static func slide(_ kind: Kind, _ inputs: Inputs) -> UpsellSlide {
        let copy = copy(kind, inputs)
        return UpsellSlide(
            kind: kind,
            top: copy.top,
            title: copy.title,
            cta: copy.cta,
            gradient: gradient(kind),
            mascot: mascot(kind),
            action: action(kind, inputs)
        )
    }

    private static func action(_ kind: Kind, _ inputs: Inputs) -> Action {
        switch kind {
        case .notifications: .turnOnNotifications
        // Credit is spent by itself and express is a perk of any booking, so both just open booking.
        case .credit, .express: .book
        case .setupRecurring: .setupRecurring
        case .plus: .subscribePlus
        case .referral: inputs.referralCode.map { .shareReferral(code: $0) } ?? .openReferral
        case .quickSize: .bookSize(rooms: 1, bathrooms: 1)
        }
    }

    // swiftlint:disable:next large_tuple
    private static func copy(_ kind: Kind, _ inputs: Inputs) -> (top: String, title: String, cta: String) {
        switch kind {
        case .notifications:
            (L10n.Home.upsellNotificationsTop, L10n.Home.upsellNotificationsTitle, L10n.Home.upsellNotificationsCta)
        case .credit:
            // The server's balance and share, never a figure of the copy's own.
            (
                L10n.Credit.yourCredit,
                L10n.Home.upsellCreditTitle(
                    OrdersFormat.price(inputs.credit?.amount ?? 0, currencyCode: inputs.credit?.currencyCode),
                    share: inputs.creditShare
                ),
                L10n.Home.upsellBookCta
            )
        case .express:
            // Plus waives the express surcharge N times a month, on a slot 2–4 h out.
            (
                L10n.Home.upsellExpressTop(Int(BookingPricing.expressLeadHours), Int(BookingPricing.standardLeadHours)),
                L10n.Home.upsellExpressTitle(inputs.expressRemaining),
                L10n.Home.upsellBookCta
            )
        case .setupRecurring:
            (L10n.Home.upsellSetupRecurringTop, L10n.Home.upsellSetupRecurringTitle, L10n.Home.upsellSetupRecurringCta)
        case .plus:
            inputs.plusTrialDays > 0
                ? (
                    L10n.Home.upsellPlusTop,
                    L10n.Home.upsellPlusTitleTrial(inputs.plusTrialDays),
                    L10n.Home.upsellPlusCtaTrial
                )
                : (L10n.Home.upsellPlusTop, L10n.Home.upsellPlusTitle, L10n.Home.upsellPlusCta)
        case .referral:
            (L10n.Home.upsellReferralTop, L10n.Home.upsellReferralTitle, L10n.Home.upsellReferralCta)
        case .quickSize:
            // The closer replaces the old generic "Book" slide, which only duplicated the FAB.
            ("", L10n.Home.quickSizeTitle, L10n.Home.quickSizeCta)
        }
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

    /// The page to show when the slide set changes: the slide on screen if it is still in the set —
    /// slides arrive at the front, so its index moves — else the same position, or the last slide if
    /// the set shrank past it.
    static func page(afterChangeFrom page: Int, old: [Kind], new: [Kind]) -> Int {
        guard !new.isEmpty else { return 0 }
        let logical = logicalIndex(page: page, count: old.count)
        let kept = old.indices.contains(logical) ? new.firstIndex(of: old[logical]) : nil
        return self.page(logical: kept ?? min(logical, new.count - 1), count: new.count)
    }
}
