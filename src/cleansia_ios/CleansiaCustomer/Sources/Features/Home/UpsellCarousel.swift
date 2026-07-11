import CleansiaCore
import SwiftUI

private let upsellCardHeight: CGFloat = 180

/// The smart-upsell pager (`SmartUpsellCarousel`, `HomeTab.kt:399-568`) — an
/// inner `TabView(.page)` per the ADR-0018 D3 HorizontalPager mapping, with the
/// Android custom dot row below (active dot grows wide) rather than the stock
/// overlaid `UIPageControl`.
struct UpsellCarousel: View {
    @Environment(\.locale) private var locale
    let isPlus: Bool
    let hasAnyOrders: Bool
    let showSetupRecurring: Bool
    let onAction: (UpsellSlide.Action) -> Void

    @State private var selection = 0

    private static let autoRotateSeconds: UInt64 = 6

    /// Rebuilt each render so the per-slide `L10n` strings re-resolve against the
    /// live `L10n.bundle` on an in-app language switch, rather than freezing at
    /// the parent's first paint (the trust-strip/loyalty live-i18n fix, applied
    /// to the carousel — the `.id(locale.identifier)` below drives the re-run).
    private var slides: [UpsellSlide] {
        UpsellSlide.slides(isPlus: isPlus, hasAnyOrders: hasAnyOrders, showSetupRecurring: showSetupRecurring)
    }

    var body: some View {
        VStack(spacing: 0) {
            TabView(selection: $selection) {
                ForEach(Array(slides.enumerated()), id: \.element.id) { index, slide in
                    UpsellSlideCard(slide: slide) { onAction(slide.action) }
                        .tag(index)
                }
            }
            .tabViewStyle(.page(indexDisplayMode: .never))
            .frame(height: upsellCardHeight)

            if slides.count > 1 {
                dotRow(count: slides.count)
                    .padding(.top, 10)
            }
        }
        .id(locale.identifier)
        .onChange(of: slides.count) { count in
            selection = min(selection, max(count - 1, 0))
        }
        .task(id: AutoRotateKey(count: slides.count, page: selection)) {
            await autoAdvance(count: slides.count)
        }
    }

    private func dotRow(count: Int) -> some View {
        HStack(spacing: 0) {
            ForEach(0 ..< count, id: \.self) { index in
                Capsule()
                    .fill(index == selection ? CleansiaColors.primary : CleansiaColors.outlineVariant)
                    .frame(width: index == selection ? 24 : 8, height: 8)
                    .padding(.horizontal, 3)
            }
        }
        .frame(maxWidth: .infinity)
        .animation(.default, value: selection)
    }

    /// The 6s auto-rotate (`HomeTab.kt:513-526`). Keying the task on the
    /// current page restarts the countdown after any page change, so a user
    /// swipe earns a fresh 6s before the next advance (the Android
    /// pause-while-touching intent, without a drag-state hook TabView lacks).
    private func autoAdvance(count: Int) async {
        guard count > 1 else { return }
        try? await Task.sleep(nanoseconds: Self.autoRotateSeconds * 1_000_000_000)
        guard !Task.isCancelled else { return }
        withAnimation { selection = (selection + 1) % count }
    }

    private struct AutoRotateKey: Equatable {
        let count: Int
        let page: Int
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
                .font(CleansiaFont.poppins(.bold, size: 18))
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
                    hasAnyOrders: false,
                    showSetupRecurring: false,
                    onAction: { _ in }
                )
                .previewDisplayName("Free, no orders")
                UpsellCarousel(
                    isPlus: true,
                    hasAnyOrders: true,
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
