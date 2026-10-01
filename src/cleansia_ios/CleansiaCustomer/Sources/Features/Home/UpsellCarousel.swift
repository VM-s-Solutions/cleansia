import CleansiaCore
import SwiftUI

private let upsellCardHeight: CGFloat = 180

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
    let isPlus: Bool
    let plusTrialDays: Int
    let showSetupRecurring: Bool
    let onAction: (UpsellSlide.Action) -> Void

    @State private var page: Int
    /// The slide count `page` was laid out for, so a count change can keep the slide on screen.
    @State private var anchoredCount: Int
    /// A page change this view made itself (auto-advance, a re-anchor); any other change is the customer.
    @State private var programmaticPage: Int?
    /// Set by the first swipe, tap or VoiceOver page action; auto-advance then stops for this visit.
    @State private var userInteracted = false

    private static let autoRotateSeconds: UInt64 = 6
    /// Long enough for the page to finish settling before a clone is swapped for its real slide.
    private static let reanchorDelayNanoseconds: UInt64 = 350_000_000

    init(
        isPlus: Bool,
        plusTrialDays: Int,
        showSetupRecurring: Bool,
        onAction: @escaping (UpsellSlide.Action) -> Void
    ) {
        self.isPlus = isPlus
        self.plusTrialDays = plusTrialDays
        self.showSetupRecurring = showSetupRecurring
        self.onAction = onAction
        let count = UpsellSlide.slides(
            isPlus: isPlus,
            plusTrialDays: plusTrialDays,
            showSetupRecurring: showSetupRecurring
        ).count
        _page = State(initialValue: UpsellSlide.page(logical: 0, count: count))
        _anchoredCount = State(initialValue: count)
    }

    /// Rebuilt each render so the per-slide `L10n` strings re-resolve against the
    /// live `L10n.bundle` on an in-app language switch, rather than freezing at
    /// the parent's first paint (the trust-strip/loyalty live-i18n fix, applied
    /// to the carousel — the `.id(locale.identifier)` below drives the re-run).
    private var slides: [UpsellSlide] {
        UpsellSlide.slides(isPlus: isPlus, plusTrialDays: plusTrialDays, showSetupRecurring: showSetupRecurring)
    }

    var body: some View {
        let slides = slides
        let count = slides.count
        VStack(spacing: 0) {
            TabView(selection: $page) {
                ForEach(0 ..< UpsellSlide.pageCount(slides: count), id: \.self) { index in
                    let logical = UpsellSlide.logicalIndex(page: index, count: count)
                    let slide = slides[logical]
                    UpsellSlideCard(slide: slide) {
                        userInteracted = true
                        onAction(slide.action)
                    }
                    .accessibilityValue(Text(L10n.Home.upsellPageA11y(logical + 1, count)))
                    .accessibilityAdjustableAction { direction in
                        guard count > 1 else { return }
                        userInteracted = true
                        switch direction {
                        case .increment: show(page: page + 1)
                        case .decrement: show(page: page - 1)
                        @unknown default: break
                        }
                    }
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
        .onChange(of: count) { newCount in
            let target = UpsellSlide.page(afterCountChangeFrom: page, oldCount: anchoredCount, newCount: newCount)
            anchoredCount = newCount
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
        let pages = UpsellSlide.pageCount(slides: anchoredCount)
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
            GeometryReader { geo in
                ZStack(alignment: .bottomTrailing) {
                    textColumn
                        .frame(width: geo.size.width * 0.72, alignment: .leading)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    slide.mascot.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 110, height: 110)
                        .accessibilityHidden(true)
                }
            }
            .padding(Spacing.ml)
            .frame(height: upsellCardHeight)
            .background(slide.gradient.linearGradient, in: RoundedRectangle(cornerRadius: 22))
            .padding(.horizontal, Spacing.ml)
        }
        .buttonStyle(.plain)
    }

    private var textColumn: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(slide.top)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(.white.opacity(0.85))
            Text(slide.title)
                .cleansiaFont(.poppins(.bold, size: 18))
                .foregroundColor(.white)
                .multilineTextAlignment(.leading)
                .padding(.top, Spacing.xxs)
            HStack(spacing: 6) {
                Text(slide.cta)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(.white)
                Image(systemName: "arrow.right")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundColor(.white)
                    .accessibilityHidden(true)
            }
            .padding(.horizontal, 14)
            .padding(.vertical, Spacing.xs)
            .background(Color.white.opacity(0.22), in: Capsule())
            .padding(.top, 14)
        }
    }
}

#if DEBUG
    struct UpsellCarousel_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                UpsellCarousel(
                    isPlus: false,
                    plusTrialDays: 14,
                    showSetupRecurring: false,
                    onAction: { _ in }
                )
                .previewDisplayName("Free")
                UpsellCarousel(
                    isPlus: true,
                    plusTrialDays: 0,
                    showSetupRecurring: true,
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
