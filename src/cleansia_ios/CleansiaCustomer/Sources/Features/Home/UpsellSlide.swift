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
        /// The "did you know" facts that fill the set after referral.
        case plusCancellation
        case expressToday
        case rewards
        case arrivalTimes
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
        /// The headline plan's discount in whole percent; 0 while unknown, and the Plus slide then names none.
        var plusDiscountPercent = 0
        /// The member's free-cancellation window when it is a benefit (shorter than the standard one);
        /// 0 for a non-member, a member whose benefits are paused, or a plan with no shorter window.
        var memberCancellationHours = 0
        /// The points each side of a referral gets, from the server; nil until the referral account loads.
        var referralPoints: Int?
    }

    let kind: Kind
    let top: String
    let title: String
    /// Two lines under the title.
    let description: String
    /// The fact chip above the mascot: an SF Symbol, with the slide's figure beside it when it has one.
    let chipSymbol: String
    let chipText: String?
    let cta: String
    let gradient: BrandGradient
    let mascot: Mascot
    let action: Action

    var id: Kind {
        kind
    }

    /// How many slides come before the quick-size closer, which always shows.
    static let leadingCap = 4

    /// Which slides show, in order — most relevant first, so the slide on screen at t=0 is the one the
    /// customer is most likely to act on. The first ``leadingCap`` eligible slides show, then quick-size
    /// closes the set: five every time, for every customer. The state-driven slides come first; the
    /// "did you know" facts after referral fill the rest, and give way first. Referral, rewards and arrival
    /// times are always eligible, and express-today is whenever the express slide is not, so four leading
    /// slides always exist. Same order as Android's `upsellKinds`.
    static func kinds(_ inputs: Inputs) -> [Kind] {
        var leading: [Kind] = []
        if inputs.notificationsOff { leading.append(.notifications) }
        if inputs.credit != nil { leading.append(.credit) }
        if inputs.expressRemaining > 0 { leading.append(.express) }
        if inputs.showSetupRecurring { leading.append(.setupRecurring) }
        if !inputs.isPlus { leading.append(.plus) }
        leading.append(.referral)
        if inputs.isPlus, inputs.memberCancellationHours > 0 { leading.append(.plusCancellation) }
        // The express slide already says it for a member with a waiver left.
        if inputs.expressRemaining == 0 { leading.append(.expressToday) }
        leading.append(.rewards)
        leading.append(.arrivalTimes)
        return Array(leading.prefix(leadingCap)) + [.quickSize]
    }

    /// The first and the last arrival time the booking offers — 08:00 and 19:45 — for the arrival-times slide.
    static var arrivalBounds: (first: String, last: String) {
        let last = BookingTimeSlots.lastWindowHour * 60 - BookingTimeSlots.bookingSlotIntervalMinutes
        return (
            String(format: "%02d:00", BookingTimeSlots.firstWindowHour),
            String(format: "%02d:%02d", last / 60, last % 60)
        )
    }

    static func slides(_ inputs: Inputs) -> [UpsellSlide] {
        kinds(inputs).map { slide($0, inputs) }
    }

    // One case per kind is the whole of `mascot` and `chip`, so their complexity is the number of slides.
    // swiftlint:disable cyclomatic_complexity

    /// Every slide draws its own mascot, so no two visible slides repeat one. Same mapping as Android.
    static func mascot(_ kind: Kind) -> Mascot {
        switch kind {
        case .notifications: .waving
        case .credit: .invoice
        case .express: .floorScrubber
        case .setupRecurring: .idea
        case .plus: .plus
        case .referral: .thumbsUp
        case .plusCancellation: .leaning
        case .expressToday: .ready
        case .rewards: .sprayAndCloth
        case .arrivalTimes: .resting
        case .quickSize: .vacuuming
        }
    }

    static func gradient(_ kind: Kind) -> BrandGradient {
        switch kind {
        case .notifications: .orange
        case .credit: .emerald
        // Members only, so it never sits beside the Plus slide that shares the Plus hero.
        case .express, .plus: .plusHero
        case .setupRecurring, .plusCancellation: .purple
        case .referral, .arrivalTimes: .cyan
        case .expressToday: .orange
        case .rewards: .emerald
        case .quickSize: .blue
        }
    }

    private static func slide(_ kind: Kind, _ inputs: Inputs) -> UpsellSlide {
        let copy = copy(kind, inputs)
        let chip = chip(kind, inputs)
        return UpsellSlide(
            kind: kind,
            top: copy.top,
            title: copy.title,
            description: copy.description,
            chipSymbol: chip.symbol,
            chipText: chip.text,
            cta: copy.cta,
            gradient: gradient(kind),
            mascot: mascot(kind),
            action: action(kind, inputs)
        )
    }

    private static func action(_ kind: Kind, _ inputs: Inputs) -> Action {
        switch kind {
        case .notifications: .turnOnNotifications
        // Credit is spent by itself and express is a perk of any booking, so both just open booking — and
        // so do the facts about booking.
        case .credit, .express, .plusCancellation, .expressToday, .arrivalTimes: .book
        case .setupRecurring: .setupRecurring
        case .plus: .subscribePlus
        case .referral: inputs.referralCode.map { .shareReferral(code: $0) } ?? .openReferral
        // Rewards is where the points are, the tab the referral slide falls back to.
        case .rewards: .openReferral
        case .quickSize: .bookSize(rooms: 1, bathrooms: 1)
        }
    }

    /// The fact chip: the slide's figure when it has one, every figure the server's or the booking's.
    private static func chip(_ kind: Kind, _ inputs: Inputs) -> (symbol: String, text: String?) {
        switch kind {
        case .notifications: ("bell", nil)
        case .credit: ("wallet.pass", nil)
        case .express: ("bolt.fill", L10n.Home.upsellChipTimes(inputs.expressRemaining))
        case .setupRecurring: ("repeat", nil)
        case .plus: (
                "star",
                inputs.plusDiscountPercent > 0 ? L10n.Home.upsellChipPercentOff(inputs.plusDiscountPercent) : nil
            )
        case .referral: ("gift", inputs.referralPoints.map(L10n.Home.upsellChipPoints))
        case .plusCancellation: ("calendar.badge.checkmark", L10n.Home.upsellChipHours(inputs.memberCancellationHours))
        case .expressToday: ("bolt.fill", L10n.Home.upsellChipHours(Int(BookingPricing.expressLeadHours)))
        case .rewards: ("trophy", nil)
        case .arrivalTimes: ("clock", L10n.Home.upsellChipMinutes(BookingTimeSlots.bookingSlotIntervalMinutes))
        case .quickSize: ("house", nil)
        }
    }

    // swiftlint:enable cyclomatic_complexity

    private struct Copy {
        let top: String
        let title: String
        let description: String
        let cta: String
    }

    // swiftlint:disable:next function_body_length cyclomatic_complexity
    private static func copy(_ kind: Kind, _ inputs: Inputs) -> Copy {
        let expressLead = Int(BookingPricing.expressLeadHours)
        let standardLead = Int(BookingPricing.standardLeadHours)
        switch kind {
        case .notifications:
            return Copy(
                top: L10n.Home.upsellNotificationsTop,
                title: L10n.Home.upsellNotificationsTitle,
                description: L10n.Home.upsellNotificationsDesc,
                cta: L10n.Home.upsellNotificationsCta
            )
        case .credit:
            // The server's balance and share, never a figure of the copy's own.
            return Copy(
                top: L10n.Credit.yourCredit,
                title: L10n.Home.upsellCreditTitle(
                    OrdersFormat.price(inputs.credit?.amount ?? 0, currencyCode: inputs.credit?.currencyCode)
                ),
                description: L10n.Home.upsellCreditDesc(share: inputs.creditShare),
                cta: L10n.Home.upsellBookCta
            )
        case .express:
            // Plus waives the express surcharge N times a month, on a slot 2–4 h out.
            return Copy(
                top: L10n.Home.upsellExpressTop(expressLead, standardLead),
                title: L10n.Home.upsellExpressTitle(inputs.expressRemaining),
                description: L10n.Home.upsellExpressDesc,
                cta: L10n.Home.upsellBookCta
            )
        case .setupRecurring:
            return Copy(
                top: L10n.Home.upsellSetupRecurringTop,
                title: L10n.Home.upsellSetupRecurringTitle,
                description: L10n.Home.upsellSetupRecurringDesc,
                cta: L10n.Home.upsellSetupRecurringCta
            )
        case .plus:
            // The discount is the headline plan's, read from the server; until the plans load the slide
            // names the two benefits no plan configures.
            let trial = inputs.plusTrialDays > 0
            return Copy(
                top: L10n.Home.upsellPlusTop,
                title: trial ? L10n.Home.upsellPlusTitleTrial(inputs.plusTrialDays) : L10n.Home.upsellPlusTitle,
                description: inputs.plusDiscountPercent > 0
                    ? L10n.Home.upsellPlusDesc(inputs.plusDiscountPercent)
                    : L10n.Home.upsellPlusDescGeneric,
                cta: trial ? L10n.Home.upsellPlusCtaTrial : L10n.Home.upsellPlusCta
            )
        case .referral:
            // The points are the server's; until they load the line names none.
            let reward = inputs.referralPoints.map(L10n.Home.upsellReferralDesc)
            return Copy(
                top: L10n.Home.upsellReferralTop,
                title: L10n.Home.upsellReferralTitle,
                description: reward ?? L10n.Home.upsellReferralDescGeneric,
                cta: L10n.Home.upsellReferralCta
            )
        case .plusCancellation:
            // "Did you know?" — the member's own window, as the membership reports it.
            return Copy(
                top: L10n.Home.upsellDidYouKnow,
                title: L10n.Home.upsellPlusCancelTitle(inputs.memberCancellationHours),
                description: L10n.Home.upsellPlusCancelDesc,
                cta: L10n.Home.upsellBookCta
            )
        case .expressToday:
            // The express band is the booking policy's (BookingPricing mirrors it, and a test pins it).
            return Copy(
                top: L10n.Home.upsellDidYouKnow,
                title: L10n.Home.upsellExpressTodayTitle(expressLead),
                description: L10n.Home.upsellExpressTodayDesc(standardLead),
                cta: L10n.Home.upsellBookCta
            )
        case .rewards:
            return Copy(
                top: L10n.Home.upsellDidYouKnow,
                title: L10n.Home.upsellRewardsTitle,
                description: L10n.Home.upsellRewardsDesc,
                cta: L10n.Home.upsellRewardsCta
            )
        case .arrivalTimes:
            // The booking's own window and grid, the times the When step offers.
            let bounds = arrivalBounds
            return Copy(
                top: L10n.Home.upsellDidYouKnow,
                title: L10n.Home.upsellTimesTitle,
                description: L10n.Home.upsellTimesDesc(bounds.first, bounds.last),
                cta: L10n.Home.upsellBookCta
            )
        case .quickSize:
            // The closer replaces the old generic "Book" slide, which only duplicated the FAB.
            return Copy(top: "", title: L10n.Home.quickSizeTitle, description: "", cta: L10n.Home.quickSizeCta)
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
