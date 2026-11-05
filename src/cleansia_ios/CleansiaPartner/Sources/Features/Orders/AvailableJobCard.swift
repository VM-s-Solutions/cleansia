import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// One job on the Available tab, as a row of its day's panel: the window with its pay trailing, where and how far,
/// what and the flags, and a tinted chevron column on the trailing edge. The whole row is one button that opens the
/// job, where it is taken. With `reservedUntil` it is the reserved card: the selected edge, the hold above, the job's
/// day beside its window, and Confirm on the pay tile. Its body opens the detail and speaks one sentence; the tile
/// and Decline are its siblings, each naming the job it acts on.
struct AvailableJobCard: View {
    @Environment(\.locale) private var locale
    @Environment(\.colorScheme) private var scheme
    @Environment(\.dynamicTypeSize) private var typeSize
    @Environment(\.ordersTileWidth) private var tileWidth
    let row: OrderListItem
    let now: Date
    var clash: OrderListItem?
    var isTaking = false
    /// "Yours until 12:06 today": set only on a reserved card.
    var reservedUntil: String?
    let onOpen: () -> Void
    /// Confirm on a reserved card: it opens the take sheet.
    var onTake: () -> Void = {}
    var onDecline: () -> Void = {}

    private var flags: [OrdersBoardRules.Flag] {
        OrdersBoardRules.flags(row, clash: clash, now: now, locale: locale)
    }

    var body: some View {
        if let reservedUntil {
            VStack(alignment: .leading, spacing: 0) {
                reservedHead(reservedUntil)
                reservedRow
            }
            .ordersCard(edge: CleansiaColors.primary, width: 1.5)
        } else {
            openRow
        }
    }

    // MARK: - An open row

    private var openRow: some View {
        Button(action: onOpen) {
            VStack(alignment: .leading, spacing: 0) {
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    timeAndPay
                    areaLine
                }
                scope.padding(.top, hasScope ? Spacing.xs : 0)
                flagList.padding(.top, flags.isEmpty ? 0 : 10)
            }
            .padding(.leading, Spacing.m)
            .padding(.trailing, Self.chevronColumn + Spacing.s)
            .padding(.top, Spacing.s)
            .padding(.bottom, Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .trailing) { ChevronEdge() }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(OrdersAccessibility.openRowLabel(row, flags: flags, now: now, locale: locale))
        .accessibilityAddTraits(.isButton)
    }

    static let chevronColumn: CGFloat = 16

    /// The window and, trailing on its line, the pay; the pay under the window once both do not fit.
    private var timeAndPay: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
                lineOne
                Spacer(minLength: 0)
                payText
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                lineOne
                payText
            }
        }
    }

    @ViewBuilder
    private var payText: some View {
        if let pay = OrdersBoardRules.pay(row) {
            Text(pay)
                .font(CleansiaTypography.titleMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
                .fixedSize()
        }
    }

    private var hasScope: Bool {
        !OrdersBoardRules.scopeParts(row, locale: locale).isEmpty
    }

    private var sentence: String {
        OrdersAccessibility.cardSentence(
            row,
            flags: flags,
            reservedUntil: reservedUntil.map { "\($0), \(L10n.Orders.reservedThen)" },
            now: now,
            locale: locale
        )
    }

    // MARK: - The reserved card's head

    private func reservedHead(_ until: String) -> some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            if let start = row.cleaningDateTime {
                OrdersDateTile(date: start, size: .header, now: now)
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
                    Text(L10n.Offers.cardTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                        .frame(maxWidth: .infinity, alignment: .leading)
                    if !typeSize.isAccessibilitySize {
                        CleansiaTextLink(L10n.Offers.decline, action: onDecline)
                            .padding(.vertical, -Spacing.s)
                            .padding(.trailing, -Spacing.xxs)
                            .accessibilityLabel(actionLabel(L10n.Offers.decline))
                    }
                }
                OrdersDotFlow(
                    items: [
                        Text(until).foregroundColor(CleansiaColors.primaryText),
                        Text(L10n.Orders.reservedThen).foregroundColor(CleansiaColors.onSurfaceVariant)
                    ]
                )
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.m)
        .accessibilityHidden(true)
    }

    // MARK: - The reserved card's row

    private var reservedRow: some View {
        AvailableRowLayout(wide: typeSize.isAccessibilitySize, tileWidth: tileWidth) {
            Button(action: onOpen) { head }
                .buttonStyle(.plain)
                .accessibilityElement(children: .ignore)
                .accessibilityLabel(sentence)
                .accessibilityAddTraits(.isButton)
            tile
            scope.accessibilityHidden(true)
            flagList.accessibilityHidden(true)
            if reservedUntil != nil, typeSize.isAccessibilitySize {
                CleansiaOutlinedButton(
                    L10n.Offers.decline,
                    size: .medium,
                    contentColor: CleansiaColors.primaryText,
                    borderColor: CleansiaColors.primary,
                    action: onDecline
                )
                .accessibilityLabel(actionLabel(L10n.Offers.decline))
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Spacing.m)
        .contentShape(Rectangle())
        .onTapGesture(perform: onOpen)
        .accessibilityElement(children: .contain)
    }

    private var head: some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            lineOne
            areaLine
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .contentShape(Rectangle())
    }

    private var areaLine: some View {
        OrdersDotFlow(
            items: [
                OrdersBoardRules.areaLine(row).map { Text($0).foregroundColor(CleansiaColors.onSurface) },
                OrdersBoardRules.bandText(row.homeDistanceBand).map {
                    Text($0).foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            ].compactMap { $0 }
        )
    }

    @ViewBuilder
    private var lineOne: some View {
        let window = OrdersBoardRules.window(of: row, locale: locale) ?? ""
        if reservedUntil != nil {
            OrdersDotFlow(
                items: [
                    row.cleaningDateTime.map { Text(OrdersAccessibility.dayWord($0, now: now, locale: locale)) },
                    Text(window).monospacedDigit()
                ].compactMap { $0 },
                style: CleansiaTypography.scale.bodyLarge
            )
            .foregroundColor(CleansiaColors.onSurface)
        } else {
            let windowText = Text(window)
                .font(CleansiaTypography.bodyLarge)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize()
            if row.status == ._3 || row.status == ._4 {
                ViewThatFits(in: .horizontal) {
                    HStack(alignment: .firstTextBaseline, spacing: 10) {
                        windowText
                        underWay
                    }
                    VStack(alignment: .leading, spacing: 0) {
                        windowText
                        underWay
                    }
                }
            } else {
                windowText
            }
        }
    }

    private var underWay: some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Circle()
                .fill(scheme == .dark ? CleansiaColors.successInk : CleansiaColors.successText)
                .frame(width: 8, height: 8)
                .xHeightAligned(of: CleansiaTypography.scale.labelMedium)
            Text(L10n.Orders.underWayShort)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .fixedSize()
    }

    @ViewBuilder
    private var scope: some View {
        let parts = OrdersBoardRules.scopeParts(row, locale: locale)
        if parts.isEmpty {
            Color.clear.frame(height: 0)
        } else {
            OrdersDotFlow(items: parts.map { Text($0) }, tiesLast: true)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    @ViewBuilder
    private var flagList: some View {
        if flags.isEmpty {
            Color.clear.frame(height: 0)
        } else {
            ChipFlow(spacing: 6, wrapsWideItems: true) {
                ForEach(Array(flags.enumerated()), id: \.offset) { _, flag in
                    TintedFlag(icon: Self.symbol(flag), text: flag.text, tone: Self.tone(flag))
                }
            }
        }
    }

    private var tile: some View {
        OrdersPayTile(face: PayTileFace(row: row, isTaking: isTaking), isBusy: isTaking, action: onTake)
            .accessibilityLabel(actionLabel(L10n.Offers.confirm))
            .accessibilityHint(clash.flatMap { OrdersBoardRules.window(of: $0, locale: locale) }
                .map(L10n.Orders.flagOverlap) ?? "")
    }

    private func actionLabel(_ action: String) -> String {
        OrdersAccessibility.actionLabel(action, row: row, now: now, locale: locale)
    }

    static func symbol(_ flag: OrdersBoardRules.Flag) -> String {
        switch flag {
        case .overlap: "exclamationmark.circle"
        case .cash: "banknote"
        case .team: "person.2"
        case .dirt: "sparkles"
        case .takeUntil: "clock"
        }
    }

    static func tone(_ flag: OrdersBoardRules.Flag) -> TintedFlag.Tone {
        if case .overlap = flag { return .error }
        return flag.isAmber ? .amber : .neutral
    }
}

/// The edge an open row leads to, the row's full height: a hairline in the outline, the primary at 5 %, and the
/// chevron centred in the primary. Its loading placeholder has no chevron.
struct ChevronEdge: View {
    @Environment(\.displayScale) private var displayScale
    var showsChevron = true

    var body: some View {
        ZStack {
            CleansiaColors.primary.opacity(0.05)
            if showsChevron {
                Image(systemName: "chevron.right")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundColor(CleansiaColors.primary)
            }
        }
        .frame(width: AvailableJobCard.chevronColumn)
        .frame(maxHeight: .infinity)
        .overlay(alignment: .leading) {
            CleansiaColors.outline.frame(width: 1 / displayScale)
        }
    }
}

/// The row's grid: the text column beside the pay tile, top-aligned, then the scope and the flags full width under
/// both, whatever the scope's length. At accessibility sizes one column, the tile a full-width bar under the flags and
/// a reserved card's Decline under the tile. Its subviews are the head, the tile, the scope, the flags and, at
/// accessibility sizes, Decline.
struct AvailableRowLayout: Layout {
    var wide = false
    var tileWidth: CGFloat = 108

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let width = proposal.width ?? 360
        return CGSize(width: width, height: frames(subviews, width: width).height)
    }

    func placeSubviews(in bounds: CGRect, proposal _: ProposedViewSize, subviews: Subviews, cache _: inout ()) {
        for (index, frame) in frames(subviews, width: bounds.width).frames.enumerated() {
            subviews[index].place(
                at: CGPoint(x: bounds.minX + frame.minX, y: bounds.minY + frame.minY),
                proposal: ProposedViewSize(frame.size)
            )
        }
    }

    private func height(_ view: LayoutSubview, _ width: CGFloat) -> CGFloat {
        view.sizeThatFits(ProposedViewSize(width: width, height: nil)).height
    }

    /// A part with something in it, `gap` under the one above.
    private func below(_ view: LayoutSubview, at top: inout CGFloat, gap: CGFloat, width: CGFloat) -> CGRect {
        let height = height(view, width)
        guard height > 0 else { return CGRect(x: 0, y: top, width: width, height: 0) }
        top += gap
        defer { top += height }
        return CGRect(x: 0, y: top, width: width, height: height)
    }

    private func frames(_ subviews: Subviews, width: CGFloat) -> (frames: [CGRect], height: CGFloat) {
        guard subviews.count >= 4 else { return ([], 0) }
        let (head, tile, scope, flags) = (subviews[0], subviews[1], subviews[2], subviews[3])
        if wide {
            var top = height(head, width)
            var frames = [CGRect(x: 0, y: 0, width: width, height: top)]
            let scopeFrame = below(scope, at: &top, gap: Spacing.xs, width: width)
            let flagsFrame = below(flags, at: &top, gap: 10, width: width)
            frames.append(below(tile, at: &top, gap: Spacing.s, width: width))
            frames += [scopeFrame, flagsFrame]
            if subviews.count > 4 { frames.append(below(subviews[4], at: &top, gap: Spacing.xs, width: width)) }
            return (frames, top)
        }
        let tileW = max(tileWidth, tile.sizeThatFits(.unspecified).width)
        let textW = max(width - tileW - Spacing.s, 0)
        let headH = height(head, textW)
        let tileH = tile.sizeThatFits(ProposedViewSize(width: tileW, height: nil)).height
        var frames = [
            CGRect(x: 0, y: 0, width: textW, height: headH),
            CGRect(x: textW + Spacing.s, y: 0, width: tileW, height: tileH)
        ]
        var top = max(headH, tileH)
        frames.append(below(scope, at: &top, gap: Spacing.xs, width: width))
        frames.append(below(flags, at: &top, gap: 10, width: width))
        if subviews.count > 4 { frames.append(CGRect(x: 0, y: top, width: width, height: 0)) }
        return (frames, top)
    }
}

/// What the reserved card's pay tile says and whether it acts: Confirm beside the pay, idle while a take is on its way.
struct PayTileFace: Equatable {
    let pay: String?
    let enabled: Bool

    init(row: OrderListItem, isTaking: Bool) {
        pay = OrdersBoardRules.pay(row)
        enabled = !isTaking
    }

    var label: String {
        L10n.Offers.confirm
    }

    var icon: String {
        "chevron.right"
    }
}

/// The reserved card's one move beside its pay, on the app's tint: the pay and the move centred on one axis, the
/// chevron right after its word. Every tile of a page shares one width, the widest one's need; at accessibility sizes
/// it is a full-width bar, the pay leading and the move trailing. The whole tile is the target.
struct OrdersPayTile: View {
    @Environment(\.dynamicTypeSize) private var typeSize
    let face: PayTileFace
    var isBusy = false
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            if typeSize.isAccessibilitySize {
                bar
            } else {
                stack
                    .padding(.vertical, Spacing.xs)
                    .padding(.horizontal, Spacing.s)
                    .frame(maxWidth: .infinity)
                    .background(fill, in: RoundedRectangle(cornerRadius: CornerRadius.small))
                    .background(need)
            }
        }
        .buttonStyle(.plain)
        .contentShape(RoundedRectangle(cornerRadius: CornerRadius.small))
        .disabled(!face.enabled)
    }

    private var stack: some View {
        VStack(alignment: .center, spacing: 0) {
            payText
            move
        }
    }

    /// The tile's own width at rest, for the page's one column.
    private var need: some View {
        stack
            .padding(.horizontal, Spacing.s)
            .fixedSize()
            .hidden()
            .background(GeometryReader { space in
                Color.clear.preference(key: OrdersTileNeedKey.self, value: space.size.width)
            })
            .accessibilityHidden(true)
    }

    /// The pay leads and the move trails, on one line while they fit; else the move takes the next line, still
    /// trailing.
    private var bar: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .center, spacing: Spacing.xs) {
                payText
                Spacer(minLength: Spacing.xs)
                move
            }
            VStack(alignment: .leading, spacing: 0) {
                payText
                trailingMove
            }
        }
        .padding(.vertical, 10)
        .padding(.horizontal, Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(fill, in: RoundedRectangle(cornerRadius: CornerRadius.small))
    }

    @ViewBuilder
    private var payText: some View {
        if let pay = face.pay {
            Text(pay)
                .font(CleansiaTypography.titleMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
                .fixedSize()
        }
    }

    private var trailingMove: some View {
        HStack(spacing: 0) {
            Spacer(minLength: 0)
            move
        }
    }

    private var move: some View {
        OrdersTileMove(label: face.label, icon: face.icon, isBusy: isBusy)
    }

    private var fill: Color {
        CleansiaColors.primary.opacity(0.12)
    }
}

/// A pay tile's move, wherever a tile is drawn: the word, then its glyph on the word's cap band, the chevron at the
/// small symbol scale so its ink stays inside it. A move that `wraps` breaks into lines where its width runs out, the
/// glyph on the last line's cap band.
struct OrdersTileMove: View {
    let label: String
    var icon = "chevron.right"
    var isBusy = false
    var wraps = false

    var body: some View {
        let style = CleansiaTypography.scale.labelLarge
        let token = CleansiaTypography.labelLarge
        let line: VerticalAlignment = isBusy ? .center : wraps ? .lastTextBaseline : .firstTextBaseline
        HStack(alignment: line, spacing: Spacing.hair) {
            if isBusy {
                ProgressView().tint(CleansiaColors.primaryText)
            } else if wraps {
                Text(label).font(token).multilineTextAlignment(.trailing).fixedSize(horizontal: false, vertical: true)
            } else {
                Text(label).font(token).lineLimit(1).fixedSize()
            }
            Image(systemName: icon)
                .font(token)
                .imageScale(.small)
                .capBandAligned(wraps ? .lastTextBaseline : .firstTextBaseline, of: style)
                .accessibilityHidden(true)
        }
        .foregroundColor(CleansiaColors.primaryText)
    }
}

/// The widest pay tile's need on a page: every tile takes it, at least 108.
struct OrdersTileNeedKey: PreferenceKey {
    static let defaultValue: CGFloat = 0

    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

private struct OrdersTileWidthKey: EnvironmentKey {
    static let defaultValue: CGFloat = 108
}

extension EnvironmentValues {
    var ordersTileWidth: CGFloat {
        get { self[OrdersTileWidthKey.self] }
        set { self[OrdersTileWidthKey.self] = newValue }
    }
}
