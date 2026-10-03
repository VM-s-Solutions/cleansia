import CleansiaCore
import SwiftUI

/// Every slide is this tall, so the pager never changes height between slides — 196 rather than 180 so
/// the quick-size steppers fit. The skeleton matches.
let upsellCardHeight: CGFloat = 196

/// The smart-upsell pager (`SmartUpsellCarousel`, `HomeTab.kt`) — an inner `TabView(.page)` per the
/// ADR-0018 D3 HorizontalPager mapping, with the Android custom dot row below (active dot grows wide)
/// rather than the stock overlaid `UIPageControl`.
///
/// It loops. Sentinel clones (see `UpsellSlide.pageCount(slides:)`) sit past both ends, and a page that
/// settles on one jumps, unanimated, to the real slide it copies. The page list never changes during a
/// scroll — mutating a page TabView's source mid-scroll is the iOS 17.4 page-skip bug.
struct UpsellCarousel: View {
    @Environment(\.locale) private var locale
    @Environment(\.accessibilityVoiceOverEnabled) private var voiceOverEnabled
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let inputs: UpsellSlide.Inputs
    let onAction: (UpsellSlide.Action) -> Void

    @State private var page: Int
    /// The slide set `page` was laid out for, so a change can keep the slide on screen.
    @State private var anchoredKinds: [UpsellSlide.Kind]
    /// The quick-size slide's own steppers, starting where the booking wizard starts.
    @State private var quickRooms = 1
    @State private var quickBathrooms = 1
    /// A page change this view made itself (auto-advance, a re-anchor); any other change is the customer.
    @State private var programmaticPage: Int?
    /// Set by the first swipe, tap or VoiceOver page action; auto-advance then stops for this visit.
    @State private var userInteracted = false

    private static let autoRotateSeconds: UInt64 = 6
    /// Long enough for the page to finish settling before a clone is swapped for its real slide.
    private static let reanchorDelayNanoseconds: UInt64 = 350_000_000

    init(inputs: UpsellSlide.Inputs, onAction: @escaping (UpsellSlide.Action) -> Void) {
        self.inputs = inputs
        self.onAction = onAction
        let kinds = UpsellSlide.kinds(inputs)
        _page = State(initialValue: UpsellSlide.page(logical: 0, count: kinds.count))
        _anchoredKinds = State(initialValue: kinds)
    }

    /// Rebuilt each render so the per-slide `L10n` strings re-resolve against the
    /// live `L10n.bundle` on an in-app language switch, rather than freezing at
    /// the parent's first paint (the trust-strip/loyalty live-i18n fix, applied
    /// to the carousel — the `.id(locale.identifier)` below drives the re-run).
    private var slides: [UpsellSlide] {
        UpsellSlide.slides(inputs)
    }

    var body: some View {
        let slides = slides
        let count = slides.count
        VStack(spacing: 0) {
            TabView(selection: $page) {
                ForEach(0 ..< UpsellSlide.pageCount(slides: count), id: \.self) { index in
                    let logical = UpsellSlide.logicalIndex(page: index, count: count)
                    card(slides[logical], position: L10n.Home.upsellPageA11y(logical + 1, count), count: count)
                        // A clone repeats a real slide; VoiceOver reads each offer once.
                        .accessibilityHidden(UpsellSlide.reanchor(page: index, count: count) != nil)
                        .tag(index)
                }
            }
            .tabViewStyle(.page(indexDisplayMode: .never))
            .frame(height: upsellCardHeight)

            if count > 1 {
                dotRow(count: count, selected: UpsellSlide.logicalIndex(page: page, count: count))
                    .padding(.top, 10)
                    .accessibilityHidden(true)
            }
        }
        .id(locale.identifier)
        .onChange(of: slides.map(\.kind)) { kinds in
            let target = UpsellSlide.page(afterChangeFrom: page, old: anchoredKinds, new: kinds)
            anchoredKinds = kinds
            jump(to: target)
        }
        .onChange(of: page) { newPage in
            if programmaticPage == newPage {
                programmaticPage = nil
            } else {
                userInteracted = true
            }
            scheduleReanchor(from: newPage, count: count)
        }
        .onDisappear { userInteracted = false }
        .task(id: AutoRotateKey(
            count: count,
            page: page,
            stopped: userInteracted || voiceOverEnabled || reduceMotion
        )) {
            await autoAdvance(count: count)
        }
    }

    /// The quick-size card holds its own steppers; the referral card is the share sheet once the code
    /// has loaded; every other card is one button.
    ///
    /// Each card states its position and takes VoiceOver's swipe up/down to change offer — on the card
    /// itself, or on the quick-size title, since that card holds controls of its own.
    @ViewBuilder
    private func card(_ slide: UpsellSlide, position: String, count: Int) -> some View {
        let page = PageAccessibility(position: position) { direction in
            guard count > 1 else { return }
            userInteracted = true
            switch direction {
            case .increment: show(page: self.page + 1)
            case .decrement: show(page: self.page - 1)
            @unknown default: break
            }
        }
        switch slide.action {
        case .bookSize:
            QuickSizeSlideCard(
                slide: slide,
                rooms: $quickRooms,
                bathrooms: $quickBathrooms,
                page: page,
                onInteract: { userInteracted = true },
                onSeePrice: {
                    userInteracted = true
                    onAction(.bookSize(rooms: quickRooms, bathrooms: quickBathrooms))
                }
            )
        case let .shareReferral(code):
            ShareLink(item: RewardsShare.message(code: code)) {
                UpsellSlideFace(slide: slide)
            }
            .buttonStyle(.plain)
            .simultaneousGesture(TapGesture().onEnded { userInteracted = true })
            .modifier(page)
        default:
            UpsellSlideCard(slide: slide) {
                userInteracted = true
                onAction(slide.action)
            }
            .modifier(page)
        }
    }

    private func dotRow(count: Int, selected: Int) -> some View {
        HStack(spacing: 0) {
            ForEach(0 ..< count, id: \.self) { index in
                Capsule()
                    .fill(index == selected ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                    .frame(width: index == selected ? 24 : 8, height: 8)
                    .padding(.horizontal, 3)
            }
        }
        .frame(maxWidth: .infinity)
        .animation(.default, value: selected)
    }

    /// The 6 s auto-advance, always forward: past the last slide it lands on the first slide's clone,
    /// which then re-anchors, so it never rewinds across every slide. It is moving content, so it stops
    /// for the rest of the visit once the customer swipes or taps (WCAG 2.2.2) and never runs under
    /// VoiceOver or Reduce Motion. Keying the task on the page restarts the countdown after any change.
    private func autoAdvance(count: Int) async {
        guard count > 1, !userInteracted, !voiceOverEnabled, !reduceMotion else { return }
        try? await Task.sleep(nanoseconds: Self.autoRotateSeconds * 1_000_000_000)
        guard !Task.isCancelled, UpsellSlide.reanchor(page: page, count: count) == nil else { return }
        programmaticPage = page + 1
        withAnimation { page += 1 }
    }

    private func show(page target: Int) {
        let pages = UpsellSlide.pageCount(slides: anchoredKinds.count)
        guard (0 ..< pages).contains(target) else { return }
        withAnimation { page = target }
    }

    /// A clone stands in for its real slide only until the page has settled on it.
    private func scheduleReanchor(from settled: Int, count: Int) {
        guard let target = UpsellSlide.reanchor(page: settled, count: count) else { return }
        Task { @MainActor in
            try? await Task.sleep(nanoseconds: Self.reanchorDelayNanoseconds)
            guard page == settled else { return }
            jump(to: target)
        }
    }

    /// Unanimated, so the swap from a clone to the slide it copies is invisible.
    private func jump(to target: Int) {
        guard target != page else { return }
        programmaticPage = target
        var transaction = Transaction()
        transaction.disablesAnimations = true
        withTransaction(transaction) { page = target }
    }

    private struct AutoRotateKey: Equatable {
        let count: Int
        let page: Int
        let stopped: Bool
    }
}

private struct UpsellSlideCard: View {
    let slide: UpsellSlide
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            UpsellSlideFace(slide: slide)
        }
        .buttonStyle(.plain)
    }
}

/// The card itself, without the control that wraps it — a button, or the referral share sheet.
///
/// Inside 196 pt, 20 pt of padding leaves 156: the eyebrow (16) + 6 + the title (two 18 pt lines) + 4 +
/// the description (two lines) + the CTA pill, with the slack above the pill. The mascot's 84 pt square
/// sits bottom-trailing, below where the title's second line ends, so the title runs the full width; the
/// fact chip sits top-trailing, beside the eyebrow, and the eyebrow, the description and the pill stop
/// short of that column. A title or description too long for its two lines shrinks to 80% — Android's
/// twin steps down the same way.
private struct UpsellSlideFace: View {
    let slide: UpsellSlide

    private static let mascotSize: CGFloat = 84
    /// What the eyebrow, the description and the pill leave free at the card's end: the mascot's column
    /// and a 4 pt gap.
    private static let mascotColumn: CGFloat = 88

    var body: some View {
        ZStack(alignment: .topLeading) {
            slide.mascot.image
                .resizable()
                .scaledToFit()
                .frame(width: Self.mascotSize, height: Self.mascotSize)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottomTrailing)
                .accessibilityHidden(true)
            UpsellFactChip(symbol: slide.chipSymbol, text: slide.chipText)
                .frame(maxWidth: .infinity, alignment: .topTrailing)
            textColumn
        }
        .padding(Spacing.ml)
        .frame(height: upsellCardHeight)
        .background(slide.gradient.linearGradient, in: RoundedRectangle(cornerRadius: 22))
        .padding(.horizontal, Spacing.ml)
    }

    private var textColumn: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(slide.top)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(.white.opacity(0.85))
                .lineLimit(1)
                .padding(.trailing, Self.mascotColumn)
            Text(slide.title)
                .cleansiaFont(.poppins(.bold, size: 18))
                .foregroundColor(.white)
                .multilineTextAlignment(.leading)
                .lineLimit(2)
                .minimumScaleFactor(0.8)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.top, 6)
            Text(slide.description)
                .cleansiaFont(.nunito(.regular, size: 13))
                .foregroundColor(.white.opacity(0.9))
                .multilineTextAlignment(.leading)
                .lineLimit(2)
                .minimumScaleFactor(0.8)
                .fixedSize(horizontal: false, vertical: true)
                .padding(.top, Spacing.xxs)
                .padding(.trailing, Self.mascotColumn)
            Spacer(minLength: Spacing.xs)
            UpsellCtaPill(text: slide.cta)
                .padding(.trailing, Self.mascotColumn)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

/// The slide's fact: an icon, and its figure when it has one ("−5%", "2 h", "+150"). Decorative to
/// VoiceOver — the title and description already say it.
private struct UpsellFactChip: View {
    let symbol: String
    let text: String?

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: symbol)
                .font(.system(size: 13, weight: .semibold))
            if let text {
                Text(text)
                    .font(CleansiaTypography.labelMedium)
                    .fontWeight(.heavy)
                    .lineLimit(1)
            }
        }
        .foregroundColor(.white)
        .padding(.horizontal, text == nil ? 6 : 10)
        .padding(.vertical, 3)
        .background(Color.white.opacity(0.22), in: Capsule())
        .accessibilityHidden(true)
    }
}

private struct UpsellCtaPill: View {
    let text: String

    var body: some View {
        HStack(spacing: 6) {
            // One line: beside the mascot on a narrow phone, a long label shrinks rather than wraps.
            Text(text)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(.white)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
            Image(systemName: "arrow.right")
                .font(.system(size: 14, weight: .semibold))
                .foregroundColor(.white)
                .accessibilityHidden(true)
        }
        .padding(.horizontal, 14)
        .padding(.vertical, Spacing.xs)
        .background(Color.white.opacity(0.22), in: Capsule())
    }
}

/// "Offer 2 of 3", and VoiceOver's swipe up/down to the next or previous offer.
private struct PageAccessibility: ViewModifier {
    let position: String
    let adjust: (AccessibilityAdjustmentDirection) -> Void

    func body(content: Content) -> some View {
        content
            .accessibilityValue(Text(position))
            .accessibilityAdjustableAction(adjust)
    }
}

/// "How big is your home?" — two −/+ capsules and "See my price", which opens booking with the size
/// already set. Taps only (a drag would fight the pager), and the card itself is not a button, so a
/// stepper tap never opens booking. The mascot sits bottom-right beside the price button, under the
/// steppers rather than behind them, so the steppers keep the card's full width. It takes the room the
/// steppers leave, up to 72 pt: a title that wraps (uk and ru on a narrow phone) pushes the steppers
/// down, and the mascot shrinks rather than reaching into them — Android's twin shrinks it too.
private struct QuickSizeSlideCard: View {
    let slide: UpsellSlide
    @Binding var rooms: Int
    @Binding var bathrooms: Int
    let page: PageAccessibility
    let onInteract: () -> Void
    let onSeePrice: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(slide.title)
                .cleansiaFont(.poppins(.bold, size: 18))
                .foregroundColor(.white)
                .lineLimit(2)
                .frame(maxWidth: .infinity, alignment: .leading)
                .modifier(page)
            HStack(spacing: Spacing.xs) {
                QuickSizeStepper(
                    label: L10n.Booking.roomsShort(rooms),
                    lessLabel: L10n.Home.quickSizeRoomsLess,
                    moreLabel: L10n.Home.quickSizeRoomsMore,
                    value: rooms,
                    range: 1 ... PropertySize.maxRooms
                ) { next in
                    onInteract()
                    rooms = next
                }
                QuickSizeStepper(
                    label: L10n.Booking.bathShort(bathrooms),
                    lessLabel: L10n.Home.quickSizeBathsLess,
                    moreLabel: L10n.Home.quickSizeBathsMore,
                    value: bathrooms,
                    range: 1 ... PropertySize.maxBathrooms
                ) { next in
                    onInteract()
                    bathrooms = next
                }
            }
            .padding(.top, Spacing.xs)
            HStack(alignment: .top, spacing: Spacing.xs) {
                Button(action: onSeePrice) {
                    UpsellCtaPill(text: slide.cta)
                }
                .buttonStyle(.plain)
                .padding(.top, 10)
                Spacer(minLength: 0)
                slide.mascot.image
                    .resizable()
                    .scaledToFit()
                    .frame(maxWidth: 72, maxHeight: 72)
                    .padding(.top, Spacing.xxs)
                    .frame(maxHeight: .infinity, alignment: .bottom)
                    .accessibilityHidden(true)
            }
        }
        .padding(Spacing.ml)
        .frame(height: upsellCardHeight)
        .background(slide.gradient.linearGradient, in: RoundedRectangle(cornerRadius: 22))
        .padding(.horizontal, Spacing.ml)
    }
}

private struct QuickSizeStepper: View {
    let label: String
    let lessLabel: String
    let moreLabel: String
    let value: Int
    let range: ClosedRange<Int>
    let onChange: (Int) -> Void

    var body: some View {
        HStack(spacing: 0) {
            button("minus", label: lessLabel, enabled: value > range.lowerBound) { onChange(value - 1) }
            // On a 320 pt phone the label has 44 pt, and uk and ru counts do not fit one line
            // ("1 ванна кімната", "3 комнаты"), so it takes two, which the 44 pt capsule holds; a word
            // still too wide for its line shrinks to 80%. Android's twin does the same.
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .fontWeight(.semibold)
                .foregroundColor(.white)
                .lineLimit(2)
                .multilineTextAlignment(.center)
                .minimumScaleFactor(0.8)
                .frame(maxWidth: .infinity)
            button("plus", label: moreLabel, enabled: value < range.upperBound) { onChange(value + 1) }
        }
        .frame(height: 44)
        .background(Color.white.opacity(0.22), in: Capsule())
    }

    /// A 44 pt square target, the HIG's smallest, laid out 36 pt wide: the 4 pt either side reach into the
    /// label's margin and the gap beside the capsule, so the label keeps the width (Android's 40 dp step
    /// with its 48 dp target). The bound greys the button rather than hiding it.
    private func button(
        _ systemImage: String,
        label: String,
        enabled: Bool,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: 14, weight: .bold))
                .foregroundColor(.white.opacity(enabled ? 1 : 0.38))
                .frame(width: 44, height: 44)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .accessibilityLabel(Text(label))
        .padding(.horizontal, -4)
    }
}

#if DEBUG
    struct UpsellCarousel_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                UpsellCarousel(inputs: UpsellSlide.Inputs(plusTrialDays: 14), onAction: { _ in })
                    .previewDisplayName("Free")
                UpsellCarousel(
                    inputs: UpsellSlide.Inputs(isPlus: true, showSetupRecurring: true, expressRemaining: 2),
                    onAction: { _ in }
                )
                .previewDisplayName("Plus, no recurring")
            }
            .padding(.vertical, Spacing.m)
            .background(CleansiaColors.background)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
