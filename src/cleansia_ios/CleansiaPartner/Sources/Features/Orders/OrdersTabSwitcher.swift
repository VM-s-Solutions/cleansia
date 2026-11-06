import CleansiaCore
import SwiftUI

/// Available · Active · History on one track, with a knob that slides to the chosen tab and the server's
/// counts beside the first two. The three share the track equally while each keeps 12 between its words and the
/// knob's edge; a longer one takes the room it needs from the others. A segment that still does not fit, and every one
/// at accessibility sizes, puts its badge under the label, else shows the label alone (the count then lives only in
/// the spoken label). Never an ellipsis. Each segment takes the track's whole height, so a label with no badge sits
/// centred beside stacked ones.
struct OrdersTabSwitcher: View {
    @Environment(\.colorScheme) private var scheme
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @Environment(\.dynamicTypeSize) private var typeSize
    @Namespace private var knob
    let selected: OrdersTab
    let counts: (OrdersTab) -> Int?
    /// VoiceOver focus by segment: the Available one when a take leaves no card to land on (§6.7).
    var focus: AccessibilityFocusState<OrdersTab?>.Binding
    let onSelect: (OrdersTab) -> Void

    var body: some View {
        SegmentRow {
            ForEach(OrdersTab.allCases, id: \.self) { tab in
                segment(tab)
            }
        }
        .padding(3)
        .background(scheme == .dark ? CleansiaColors.surface : CleansiaColors.outline, in: Capsule())
        .animation(reduceMotion ? nil : .timingCurve(0.22, 1, 0.36, 1, duration: 0.26), value: selected)
        .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
    }

    private func segment(_ tab: OrdersTab) -> some View {
        let isSelected = tab == selected
        return Button { onSelect(tab) } label: {
            Group {
                if typeSize.isAccessibilitySize {
                    ViewThatFits(in: .horizontal) {
                        stacked(tab, selected: isSelected)
                        label(tab, selected: isSelected)
                    }
                } else {
                    ViewThatFits(in: .horizontal) {
                        HStack(spacing: 6) {
                            label(tab, selected: isSelected)
                            badge(tab)
                        }
                        stacked(tab, selected: isSelected)
                        label(tab, selected: isSelected)
                    }
                }
            }
            .padding(.horizontal, Spacing.s)
            .padding(.vertical, typeSize.isAccessibilitySize ? 6 : Spacing.hair)
            .frame(maxWidth: .infinity, minHeight: 40, maxHeight: .infinity)
            .background {
                if isSelected {
                    Capsule()
                        .fill(scheme == .dark ? CleansiaColors.outline : CleansiaColors.surface)
                        .matchedGeometryEffect(id: "knob", in: knob)
                }
            }
            .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(spokenLabel(tab))
        .accessibilityAddTraits(isSelected ? [.isSelected] : [])
        .accessibilityShowsLargeContentViewer()
        .accessibilityFocused(focus, equals: tab)
    }

    private func stacked(_ tab: OrdersTab, selected: Bool) -> some View {
        VStack(spacing: Spacing.hair) {
            label(tab, selected: selected)
            badge(tab)
        }
    }

    private func label(_ tab: OrdersTab, selected: Bool) -> some View {
        Text(Self.title(tab))
            .font(selected ? CleansiaTypography.labelLarge : CleansiaTypography.bodyMedium)
            .foregroundColor(selected ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant)
            .lineLimit(1)
            .fixedSize()
    }

    @ViewBuilder
    private func badge(_ tab: OrdersTab) -> some View {
        if let text = OrdersBoardRules.badge(counts(tab)) {
            Text(verbatim: text)
                .font(CleansiaTypography.labelSmall)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.primaryText)
                .lineLimit(1)
                .fixedSize()
                .padding(.horizontal, 6)
                .frame(minWidth: 22, minHeight: 22)
                .background(CleansiaColors.primary.opacity(0.12), in: Capsule())
        }
    }

    /// "Available, 6 jobs".
    private func spokenLabel(_ tab: OrdersTab) -> String {
        guard let count = counts(tab), count > 0 else { return Self.title(tab) }
        return "\(Self.title(tab)), \(L10n.Orders.jobCount(count))"
    }

    /// Equal shares of `total`, except that a segment needing more than its share keeps its need and the rest split
    /// what is left, until every segment has at least its need or holds exactly it.
    static func widths(needs: [CGFloat], total: CGFloat) -> [CGFloat] {
        var held = Array(repeating: false, count: needs.count)
        while true {
            let free = needs.indices.filter { !held[$0] }
            guard !free.isEmpty else { return needs }
            let kept = needs.indices.filter { held[$0] }.reduce(CGFloat(0)) { $0 + needs[$1] }
            let share = (total - kept) / CGFloat(free.count)
            let short = free.filter { needs[$0] > share }
            guard !short.isEmpty else { return needs.indices.map { held[$0] ? needs[$0] : share } }
            short.forEach { held[$0] = true }
        }
    }

    static func title(_ tab: OrdersTab) -> String {
        switch tab {
        case .available: L10n.Orders.available
        case .active: L10n.Orders.active
        case .history: L10n.Orders.history
        }
    }
}

/// The segments side by side at `OrdersTabSwitcher.widths`, each offered its share; when even their needs overrun
/// the track they split it equally, and a segment then stacks its badge or drops it.
private struct SegmentRow: Layout {
    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let widths = widths(subviews, total: proposal.width)
        let height = zip(subviews, widths).map { $0.sizeThatFits(ProposedViewSize(width: $1, height: nil)).height }
            .max() ?? 0
        return CGSize(width: proposal.width ?? widths.reduce(0, +), height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) {
        var leading = bounds.minX
        for (subview, width) in zip(subviews, widths(subviews, total: bounds.width)) {
            subview.place(
                at: CGPoint(x: leading, y: bounds.minY),
                proposal: ProposedViewSize(width: width, height: bounds.height)
            )
            leading += width
        }
    }

    private func widths(_ subviews: Subviews, total: CGFloat?) -> [CGFloat] {
        let needs = subviews.map { $0.sizeThatFits(.unspecified).width }
        guard let total else { return needs }
        let widths = OrdersTabSwitcher.widths(needs: needs, total: total)
        guard widths.reduce(0, +) > total + 0.5, !subviews.isEmpty else { return widths }
        return Array(repeating: total / CGFloat(subviews.count), count: subviews.count)
    }
}
