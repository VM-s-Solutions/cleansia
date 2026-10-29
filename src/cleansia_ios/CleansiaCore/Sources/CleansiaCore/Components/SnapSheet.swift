import SwiftUI

public enum SnapAnchor: CaseIterable, Equatable {
    case mapFocus
    case peek
    case expanded

    public var coveredFraction: CGFloat {
        switch self {
        case .mapFocus: 0.30
        case .peek: 0.75
        case .expanded: 0.95
        }
    }

    /// Tapping the handle is a two-way trip between the backdrop and the content,
    /// so a single tap always changes something and a second tap always undoes it.
    public var tapToggled: SnapAnchor {
        self == .mapFocus ? .peek : .mapFocus
    }

    public var moreExpanded: SnapAnchor {
        switch self {
        case .mapFocus: .peek
        case .peek, .expanded: .expanded
        }
    }

    public var lessExpanded: SnapAnchor {
        switch self {
        case .expanded: .peek
        case .peek, .mapFocus: .mapFocus
        }
    }

    public var accessibilityValue: String {
        switch self {
        case .mapFocus: CoreL10n.localized("snap_sheet.anchor.map_focus")
        case .peek: CoreL10n.localized("snap_sheet.anchor.peek")
        case .expanded: CoreL10n.localized("snap_sheet.anchor.expanded")
        }
    }
}

/// Where an edge-anchored ornament sits: its centre rides the sheet's top edge, so
/// half of it overlaps the backdrop and half the sheet, and it tracks every drag.
public enum SnapSheetOrnament {
    /// The ornament's edge length, and the one place it is written down: because its centre rides the
    /// sheet edge, the content below has to clear half of it, so the sheet's own top inset is derived
    /// from this number and the two silently mis-align if either is retyped.
    public static let defaultSize: CGFloat = 128

    public static func offsetY(sheetTop: CGFloat, size: CGFloat) -> CGFloat {
        sheetTop - size / 2
    }
}

/// The column the sheet becomes beside its backdrop, which keeps the rest. It stands there only in a wide, short
/// container: wider than it is tall, with room for a map beside the column. Anywhere else the sheet lies over the
/// backdrop. Decided by the container's size, never by the device.
enum SnapSheetColumn {
    static let width: CGFloat = 440
    static let minimumContainerWidth: CGFloat = 700

    static func standsBeside(in container: CGSize) -> Bool {
        container.width > container.height && container.width >= minimumContainerWidth
    }
}

public enum SnapResolver {
    static func sheetTop(
        for anchor: SnapAnchor,
        containerHeight: CGFloat,
        minimumSheetHeight: CGFloat = 0
    ) -> CGFloat {
        let height = max(0, containerHeight)
        let minimum = min(max(0, minimumSheetHeight), height)
        return min(height * (1 - anchor.coveredFraction), height - minimum)
    }

    public static func resolve(
        from current: SnapAnchor,
        dragTranslation: CGFloat,
        predictedEndTranslation: CGFloat,
        containerHeight: CGFloat,
        minimumSheetHeight: CGFloat = 0
    ) -> SnapAnchor {
        guard containerHeight > 0 else { return current }

        // A fling carries the predicted-end well past the finger; a slow settle
        // ends near the finger. Take whichever travels further so velocity wins
        // when present and the final drag position governs when it doesn't.
        let travel = abs(predictedEndTranslation) >= abs(dragTranslation)
            ? predictedEndTranslation
            : dragTranslation
        let currentTop = sheetTop(
            for: current, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight
        )
        let projectedTop = (currentTop + travel)
            .clamped(to: minTop(containerHeight, minimumSheetHeight) ... maxTop(containerHeight, minimumSheetHeight))

        return nearestAnchor(
            toTop: projectedTop,
            from: current,
            containerHeight: containerHeight,
            minimumSheetHeight: minimumSheetHeight
        )
    }

    private static func minTop(_ containerHeight: CGFloat, _ minimumSheetHeight: CGFloat) -> CGFloat {
        sheetTop(for: .expanded, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight)
    }

    private static func maxTop(_ containerHeight: CGFloat, _ minimumSheetHeight: CGFloat) -> CGFloat {
        sheetTop(for: .mapFocus, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight)
    }

    private static func nearestAnchor(
        toTop top: CGFloat,
        from current: SnapAnchor,
        containerHeight: CGFloat,
        minimumSheetHeight: CGFloat
    ) -> SnapAnchor {
        let nearest = SnapAnchor.allCases.min { lhs, rhs in
            let lhsDistance = abs(sheetTop(
                for: lhs, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight
            ) - top)
            let rhsDistance = abs(sheetTop(
                for: rhs, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight
            ) - top)
            return lhsDistance < rhsDistance
        } ?? .peek
        let nearestTop = sheetTop(
            for: nearest, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight
        )
        let currentTop = sheetTop(
            for: current, containerHeight: containerHeight, minimumSheetHeight: minimumSheetHeight
        )
        return nearestTop == currentTop ? current : nearest
    }
}

public struct SnapSheet<Background: View, Ornament: View, Content: View>: View {
    @Binding private var anchor: SnapAnchor
    @State private var dragOffset: CGFloat = 0
    private let ornamentSize: CGFloat
    private let minimumSheetHeight: CGFloat
    private let background: Background
    private let ornament: Ornament
    private let content: Content

    public init(
        anchor: Binding<SnapAnchor>,
        ornamentSize: CGFloat = SnapSheetOrnament.defaultSize,
        minimumSheetHeight: CGFloat = 0,
        @ViewBuilder background: () -> Background,
        @ViewBuilder ornament: () -> Ornament,
        @ViewBuilder content: () -> Content
    ) {
        _anchor = anchor
        self.ornamentSize = ornamentSize
        self.minimumSheetHeight = minimumSheetHeight
        self.background = background()
        self.ornament = ornament()
        self.content = content()
    }

    public var body: some View {
        GeometryReader { geometry in
            let height = geometry.size.height
            let beside = SnapSheetColumn.standsBeside(in: geometry.size)
            let restingTop = SnapResolver.sheetTop(
                for: anchor, containerHeight: height, minimumSheetHeight: minimumSheetHeight
            )
            let currentTop = beside ? 0 : (restingTop + dragOffset)
                .clamped(to: topBound(height) ... bottomBound(height))
            let columnInset = beside ? SnapSheetColumn.width + geometry.safeAreaInsets.trailing : 0

            ZStack(alignment: .top) {
                background
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .padding(.trailing, columnInset)
                    .ignoresSafeArea()
                    // Where the sheet's top edge sits IN THE BACKDROP'S OWN COORDINATES. The backdrop
                    // ignores the safe area, so its origin is the top of the screen, while `currentTop`
                    // is measured from the safe-area top — the inset converts between the two. Without
                    // this a backdrop cannot lay anything out against the sheet without silently
                    // guessing, and a guess is only visibly wrong at the anchors where the uncovered
                    // strip is small. Beside the backdrop the sheet covers none of it, so its top is the safe
                    // area's foot: 0 means no sheet at all, and a backdrop laid out against it collapses.
                    .environment(\.snapSheetTop, (beside ? height : currentTop) + geometry.safeAreaInsets.top)
                    .environment(\.snapSheetSafeTop, geometry.safeAreaInsets.top)
                    .environment(\.snapSheetBeside, beside)

                sheet(bottomOverhang: currentTop, beside: beside)
                    .frame(width: beside ? SnapSheetColumn.width : geometry.size.width, height: height)
                    .frame(maxWidth: .infinity, alignment: .trailing)
                    .offset(y: currentTop)
                    .gesture(dragGesture(containerHeight: height), including: beside ? .subviews : .all)

                if !beside {
                    ornament
                        .frame(width: ornamentSize, height: ornamentSize)
                        .opacity(Double(max(0, min(1, (currentTop - ornamentSize / 2) / (ornamentSize / 2)))))
                        .offset(y: SnapSheetOrnament.offsetY(sheetTop: currentTop, size: ornamentSize))
                        .allowsHitTesting(false)
                        .padding(.trailing, Spacing.m)
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topTrailing)
                        .clipped()
                }
            }
            .animation(.interactiveSpring(response: 0.35, dampingFraction: 0.85), value: anchor)
            .animation(.interactiveSpring(response: 0.35, dampingFraction: 0.85), value: dragOffset)
        }
    }

    /// The full-height frame hangs `bottomOverhang` points below the container
    /// once offset; insetting the content by the same amount keeps the bottom
    /// row (the sticky footer) pinned inside the visible area at every anchor.
    private func sheet(bottomOverhang: CGFloat, beside: Bool) -> some View {
        VStack(spacing: 0) {
            if !beside {
                DragHandle(anchor: $anchor)
            }
            content
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
        .padding(.bottom, bottomOverhang)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .background(
            UnevenRoundedRectangle(
                topLeadingRadius: CornerRadius.large,
                topTrailingRadius: CornerRadius.large
            )
            .fill(CleansiaColors.surface)
            .ignoresSafeArea(edges: beside ? [.top, .bottom, .trailing] : [])
        )
        // FLATTEN BEFORE SHADOWING. `.shadow` applies to every opaque thing drawn in the subtree, not
        // to the composite silhouette — so without this the sheet's shadow was re-drawn around each
        // card inside it, and the gap between two neighbouring cards caught both. That is the "shadow
        // on every inner container" the order-detail panel showed, and why removing the cards' own
        // borders never made any difference: the halo was never theirs. `compositingGroup` renders the
        // subtree into one layer first, so the shadow falls where it was meant to — the sheet's edge.
        .compositingGroup()
        .shadow(color: .black.opacity(0.12), radius: 12, y: -2)
    }

    private func dragGesture(containerHeight: CGFloat) -> some Gesture {
        DragGesture()
            .onChanged { value in
                dragOffset = value.translation.height
            }
            .onEnded { value in
                let resolved = SnapResolver.resolve(
                    from: anchor,
                    dragTranslation: value.translation.height,
                    predictedEndTranslation: value.predictedEndTranslation.height,
                    containerHeight: containerHeight,
                    minimumSheetHeight: minimumSheetHeight
                )
                dragOffset = 0
                anchor = resolved
            }
    }

    private func topBound(_ height: CGFloat) -> CGFloat {
        SnapResolver.sheetTop(for: .expanded, containerHeight: height, minimumSheetHeight: minimumSheetHeight)
    }

    private func bottomBound(_ height: CGFloat) -> CGFloat {
        SnapResolver.sheetTop(for: .mapFocus, containerHeight: height, minimumSheetHeight: minimumSheetHeight)
    }
}

/// A drag is unreachable under VoiceOver and undiscoverable without one, so the
/// handle is also a button (tap toggles backdrop/content) and an adjustable
/// element (swipe up/down walks the anchors).
private struct DragHandle: View {
    @Binding var anchor: SnapAnchor

    var body: some View {
        Button {
            anchor = anchor.tapToggled
        } label: {
            Capsule()
                .fill(CleansiaColors.outline)
                .frame(width: 36, height: 5)
                .padding(.top, Spacing.xs)
                .padding(.bottom, Spacing.xxs)
                .frame(maxWidth: .infinity)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(Text(CoreL10n.localized("snap_sheet.handle.label")))
        .accessibilityValue(Text(anchor.accessibilityValue))
        .accessibilityHint(Text(CoreL10n.localized("snap_sheet.handle.hint")))
        .accessibilityAdjustableAction { direction in
            switch direction {
            case .increment: anchor = anchor.moreExpanded
            case .decrement: anchor = anchor.lessExpanded
            @unknown default: break
            }
        }
    }
}

public extension SnapSheet where Ornament == EmptyView {
    init(
        anchor: Binding<SnapAnchor>,
        minimumSheetHeight: CGFloat = 0,
        @ViewBuilder background: () -> Background,
        @ViewBuilder content: () -> Content
    ) {
        self.init(
            anchor: anchor,
            minimumSheetHeight: minimumSheetHeight,
            background: background,
            ornament: { EmptyView() },
            content: content
        )
    }
}

private extension Comparable {
    func clamped(to range: ClosedRange<Self>) -> Self {
        min(max(self, range.lowerBound), range.upperBound)
    }
}

#if DEBUG
    struct SnapSheet_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper(SnapAnchor.peek) { binding in
                SnapSheet(anchor: binding) {
                    CleansiaColors.primaryContainer
                } content: {
                    VStack(spacing: Spacing.m) {
                        Text("Order detail content")
                            .font(CleansiaTypography.titleMedium)
                        ForEach(0 ..< 8, id: \.self) { index in
                            Text("Section \(index)")
                                .frame(maxWidth: .infinity)
                                .padding()
                                .background(CleansiaColors.surfaceVariant)
                        }
                    }
                    .padding(Spacing.m)
                }
            }
        }
    }
#endif

private struct SnapSheetTopKey: EnvironmentKey {
    /// 0 means "no sheet above me", so a backdrop used outside a SnapSheet lays out against its own
    /// full height rather than collapsing to nothing.
    static let defaultValue: CGFloat = 0
}

private struct SnapSheetSafeTopKey: EnvironmentKey {
    static let defaultValue: CGFloat = 0
}

private struct SnapSheetBesideKey: EnvironmentKey {
    static let defaultValue = false
}

extension EnvironmentValues {
    /// Whether the sheet stands beside the backdrop as a column rather than over it.
    var snapSheetBeside: Bool {
        get { self[SnapSheetBesideKey.self] }
        set { self[SnapSheetBesideKey.self] = newValue }
    }
}

public extension EnvironmentValues {
    /// The top of the safe viewport in the full-bleed backdrop's coordinates.
    var snapSheetSafeTop: CGFloat {
        get { self[SnapSheetSafeTopKey.self] }
        set { self[SnapSheetSafeTopKey.self] = newValue }
    }

    /// The y of the sheet's top edge, in the coordinate space of the SnapSheet's BACKGROUND view.
    ///
    /// Published so a backdrop can centre its content in the strip the sheet actually leaves visible,
    /// at any anchor and mid-drag, instead of recomputing it from an anchor constant and its own
    /// geometry — which is measured in a different space and only agrees by luck.
    var snapSheetTop: CGFloat {
        get { self[SnapSheetTopKey.self] }
        set { self[SnapSheetTopKey.self] = newValue }
    }
}
