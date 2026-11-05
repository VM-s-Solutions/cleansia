import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit

extension View {
    /// Every state of every tab sits in one scroll view: `.refreshable` only reaches a scrollable view, so a bare
    /// state swallowed the pull and could never be refreshed. The pane reports whether anything has scrolled under
    /// the header, which then draws its rule.
    func ordersScroll(onRefresh: @escaping () async -> Void) -> some View {
        GeometryReader { pane in
            ScrollView {
                frame(maxWidth: .infinity, minHeight: pane.size.height, alignment: .top)
                    .background(GeometryReader { content in
                        Color.clear.preference(
                            key: OrdersScrolledKey.self,
                            value: content.frame(in: .named(OrdersScrolledKey.space)).minY < -0.5
                        )
                    })
            }
            .coordinateSpace(name: OrdersScrolledKey.space)
            .refreshable { await onRefresh() }
        }
    }

    /// The app's card: surface, the medium corners and a hairline edge; a reserved job's edge is the selected one.
    func ordersCard(edge: Color = CleansiaColors.outlineVariant, width: CGFloat = 1) -> some View {
        background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(RoundedRectangle(cornerRadius: CornerRadius.medium).strokeBorder(edge, lineWidth: width))
    }

    /// Centres a small mark (a dot, a bullet) on the x-height band of the line its stack aligns on.
    func xHeightAligned(_ line: VerticalAlignment = .firstTextBaseline, of style: CleansiaTextStyle) -> some View {
        modifier(XHeightBand(line: line, style: style))
    }
}

/// Whether anything has scrolled under a pinned header: the Orders tabs', the take sheet's and the job panel's head.
struct OrdersScrolledKey: PreferenceKey {
    static let space = "ordersScroll"
    static let defaultValue = false

    static func reduce(value: inout Bool, nextValue: () -> Bool) {
        value = value || nextValue()
    }
}

private struct XHeightBand: ViewModifier {
    @Environment(\.dynamicTypeSize) private var typeSize
    let line: VerticalAlignment
    let style: CleansiaTextStyle

    func body(content: Content) -> some View {
        let traits = UITraitCollection(preferredContentSizeCategory: UIContentSizeCategory(typeSize))
        let size = UIFontMetrics.default.scaledValue(for: style.size, compatibleWith: traits).rounded()
        let xHeight = CleansiaFont.uiFont(style.family, weight: style.weight, size: size).xHeight
        content.alignmentGuide(line) { $0[VerticalAlignment.center] + xHeight / 2 }
    }
}

/// Items with a dot between neighbours on the same line only: each item stays whole, and a wrapped line never
/// starts or ends with a dot. Its subviews are the items with a dot between each pair (item, dot, item, …).
/// `tiesLast` makes the last dot a no-break one: the item before it joins a line only with the last item beside it,
/// unless the two fit no line.
struct OrdersSeparatorFlow: Layout {
    var tiesLast = false

    static func lines(widths: [CGFloat], separator: CGFloat, maxWidth: CGFloat, tiesLast: Bool = false) -> [[Int]] {
        var lines: [[Int]] = []
        var used: CGFloat = 0
        for (index, width) in widths.enumerated() {
            let pair = width + separator + (widths.last ?? 0)
            let needs = tiesLast && index == widths.count - 2 && pair <= maxWidth ? pair : width
            if !lines.isEmpty, used + separator + needs <= maxWidth {
                lines[lines.count - 1].append(index)
                used += separator + width
            } else {
                lines.append([index])
                used = width
            }
        }
        return lines
    }

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let arranged = arrange(subviews, maxWidth: proposal.width ?? .infinity)
        return CGSize(width: proposal.width ?? arranged.size.width, height: arranged.size.height)
    }

    /// Its first and last text baselines are those of its first and last lines, so a row can align a flow by its text.
    func explicitAlignment(
        of guide: VerticalAlignment,
        in bounds: CGRect,
        proposal _: ProposedViewSize,
        subviews: Subviews,
        cache _: inout ()
    ) -> CGFloat? {
        let baselines = arrange(subviews, maxWidth: bounds.width).baselines
        switch guide {
        case .firstTextBaseline: return baselines.first.map { bounds.minY + $0 }
        case .lastTextBaseline: return baselines.last.map { bounds.minY + $0 }
        default: return nil
        }
    }

    func placeSubviews(in bounds: CGRect, proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) {
        let arranged = arrange(subviews, maxWidth: bounds.width)
        for index in subviews.indices {
            guard let frame = arranged.frames[index] else {
                subviews[index].place(at: CGPoint(x: bounds.minX - 10000, y: bounds.minY), proposal: .unspecified)
                continue
            }
            subviews[index].place(
                at: CGPoint(x: bounds.minX + frame.minX, y: bounds.minY + frame.minY),
                proposal: ProposedViewSize(frame.size)
            )
        }
    }

    private struct Arrangement {
        var frames: [Int: CGRect]
        var size: CGSize
        var baselines: [CGFloat]
    }

    private func arrange(_ subviews: Subviews, maxWidth: CGFloat) -> Arrangement {
        let items = Array(stride(from: 0, to: subviews.count, by: 2))
        let dot = subviews.count > 1 ? subviews[1].sizeThatFits(.unspecified) : .zero
        let sizes = items.map { index -> CGSize in
            let ideal = subviews[index].sizeThatFits(.unspecified)
            guard ideal.width > maxWidth else { return ideal }
            return subviews[index].sizeThatFits(ProposedViewSize(width: maxWidth, height: nil))
        }
        var frames: [Int: CGRect] = [:]
        var lineBaselines: [CGFloat] = []
        var top: CGFloat = 0
        var widest: CGFloat = 0
        for line in Self.lines(
            widths: sizes.map(\.width),
            separator: dot.width,
            maxWidth: maxWidth,
            tiesLast: tiesLast
        ) {
            let baselines = line
                .map { subviews[items[$0]].dimensions(in: ProposedViewSize(sizes[$0]))[.firstTextBaseline] }
            let ascent = baselines.max() ?? 0
            let descent = zip(line, baselines).map { sizes[$0].height - $1 }.max() ?? 0
            var left: CGFloat = 0
            for (position, item) in line.enumerated() {
                if position > 0 {
                    let dotBaseline = subviews[items[item] - 1].dimensions(in: .unspecified)[.firstTextBaseline]
                    frames[items[item] - 1] = CGRect(origin: CGPoint(x: left, y: top + ascent - dotBaseline), size: dot)
                    left += dot.width
                }
                let size = sizes[item]
                frames[items[item]] = CGRect(
                    origin: CGPoint(x: left, y: top + ascent - baselines[position]),
                    size: size
                )
                left += size.width
            }
            widest = max(widest, left)
            lineBaselines.append(top + ascent)
            top += ascent + descent
        }
        return Arrangement(frames: frames, size: CGSize(width: widest, height: top), baselines: lineBaselines)
    }
}

/// A line of texts in the separator flow: "Praha 2 · Jana Nováková", "Setting off · in 3 h 40 min".
struct OrdersDotFlow: View {
    let items: [Text]
    var style = CleansiaTypography.scale.bodyMedium
    var tiesLast = false

    var body: some View {
        OrdersSeparatorFlow(tiesLast: tiesLast) {
            ForEach(items.indices, id: \.self) { index in
                if index > 0 {
                    Text(verbatim: "·")
                        .cleansiaFont(style)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .padding(.horizontal, 5)
                        .accessibilityHidden(true)
                }
                items[index].cleansiaFont(style)
            }
        }
        .accessibilityElement(children: .combine)
    }
}

/// Amber words on 12 % of their own amber, the icon on the first line: the stale strip and the cash notice.
struct OrdersNotice<Content: View>: View {
    let icon: String
    @ViewBuilder let content: () -> Content

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 10) {
            Image(systemName: icon)
                .font(CleansiaTypography.titleMedium)
                .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                .accessibilityHidden(true)
            content().frame(maxWidth: .infinity, alignment: .leading)
        }
        .font(CleansiaTypography.bodyMedium)
        .foregroundColor(CleansiaColors.pendingInk)
        .padding(.vertical, Spacing.s)
        .padding(.horizontal, Spacing.m)
        .background(CleansiaColors.pendingInk.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

/// Rows are held and a refresh failed: the rows stay, dated, with Retry.
struct OrdersStaleStrip: View {
    let text: String
    let onRetry: () -> Void

    var body: some View {
        OrdersNotice(icon: "arrow.clockwise") {
            HStack(alignment: .center, spacing: Spacing.xs) {
                Text(L10n.tie(text)).fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 0)
                CleansiaTextLink(L10n.retry, action: onRetry).padding(.vertical, -Spacing.s)
            }
        }
    }
}

/// "Later · 2 jobs": a heading on the ground over a list, in the section title's token, its count quiet.
struct OrdersSectionHeader: View {
    let title: String
    var trailing: String?

    var body: some View {
        (Text(title).font(CleansiaTypography.titleMedium).foregroundColor(CleansiaColors.onBackground)
            + Text(verbatim: trailing.map { "\(OrdersBoardRules.dot)\($0)" } ?? "")
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant))
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.top, Spacing.xs)
            .accessibilityAddTraits(.isHeader)
    }
}

/// A value still loading: a flat bar at the value's real size, no shimmer.
struct OrdersBar: View {
    let width: CGFloat?
    let height: CGFloat
    var fill = CleansiaColors.surfaceVariant

    var body: some View {
        RoundedRectangle(cornerRadius: CornerRadius.extraSmall)
            .fill(fill)
            .frame(width: width, height: height)
            .frame(maxWidth: width == nil ? .infinity : nil, alignment: .leading)
            .accessibilityHidden(true)
    }
}

/// The cash she holds against her limit: the fill at its true ratio, both ends labelled. With no cap only the
/// amount; over the cap the notice draws it instead.
struct OrdersCashPanel: View {
    let held: String
    let limit: String?
    let ratio: Double

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                Text(L10n.Orders.cashHeldTitle)
                Spacer(minLength: 0)
                Text(held).monospacedDigit()
            }
            .font(CleansiaTypography.titleMedium)
            .foregroundColor(CleansiaColors.onSurface)
            if let limit {
                OrdersCashMeter(ratio: ratio).padding(.top, Spacing.xs)
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    Text(verbatim: "0")
                    Spacer(minLength: 0)
                    Text(L10n.Orders.cashHeldOfLimit(held, limit)).multilineTextAlignment(.trailing)
                }
                .font(CleansiaTypography.labelMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.top, Spacing.xxs)
                Text(L10n.tie(L10n.Orders.cashHeldHint))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, Spacing.xs)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .ordersCard()
        .accessibilityElement(children: .combine)
    }
}

/// Over the limit, under the finding that says the cash jobs are hidden: the held amount over the bar, the limit
/// at its tick, the part past it red, and how the jobs come back.
struct OrdersCashNotice: View {
    let held: String
    let limit: String
    let ratio: Double

    var body: some View {
        OrdersNotice(icon: "banknote") {
            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    Text(L10n.Orders.cashHeldTitle)
                    Spacer(minLength: 0)
                    Text(held).monospacedDigit()
                }
                .font(CleansiaTypography.titleMedium)
                OrdersCashMeter(ratio: ratio, over: true).padding(.top, 6)
                GeometryReader { space in
                    ZStack(alignment: .topLeading) {
                        Text(verbatim: "0")
                        Text(L10n.Orders.cashLimitAt(limit))
                            .fixedSize()
                            .frame(width: space.size.width * CGFloat(min(max(ratio, 0), 1)), alignment: .trailing)
                    }
                }
                .frame(height: 17)
                .font(CleansiaTypography.labelMedium)
                .monospacedDigit()
                .padding(.top, Spacing.xxs)
                Text(L10n.tie(L10n.Orders.cashHiddenBack(limit)))
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, 6)
            }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel([
            "\(L10n.Orders.cashHeldTitle), \(L10n.Orders.cashHeldOfLimit(held, limit)).",
            L10n.Orders.cashHiddenBack(limit)
        ].joined(separator: " "))
    }
}

/// A 10 pt bar: the share held in amber; over the limit amber to an ink tick at the limit and red past it.
struct OrdersCashMeter: View {
    let ratio: Double
    var over = false

    var body: some View {
        GeometryReader { space in
            let share = space.size.width * CGFloat(min(max(ratio, 0), 1))
            ZStack(alignment: .leading) {
                Capsule().fill(over ? CleansiaColors.surface : CleansiaColors.surfaceVariant)
                if over {
                    Capsule().fill(CleansiaColors.error)
                    Rectangle().fill(CleansiaColors.pendingInk).frame(width: share)
                } else if ratio > 0 {
                    Capsule().fill(CleansiaColors.pendingInk).frame(width: max(share, 10))
                }
            }
            .clipShape(Capsule())
            .overlay(alignment: .leading) {
                if over {
                    RoundedRectangle(cornerRadius: 1)
                        .fill(CleansiaColors.onSurface)
                        .frame(width: 2, height: 18)
                        .offset(x: share - 1)
                }
            }
        }
        .frame(height: 10)
        .accessibilityHidden(true)
    }
}

extension OrdersBoardRules {
    /// A window that never breaks at its dash.
    static func unbreakable(_ text: String) -> String {
        text.replacingOccurrences(of: "–", with: "\u{2060}–\u{2060}")
    }

    /// A flag's facts, each its own run: it breaks between them before it breaks inside one, so "2 cleaners needed ·
    /// 1 already on it" never strands the count at a line's end.
    static func flagParts(_ text: String) -> [String] {
        text.components(separatedBy: " · ").map { L10n.tie(unbreakable($0)) }
    }
}
