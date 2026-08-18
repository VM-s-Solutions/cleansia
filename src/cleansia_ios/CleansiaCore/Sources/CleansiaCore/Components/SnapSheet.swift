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

public enum SnapResolver {
    static func sheetTop(for anchor: SnapAnchor, containerHeight: CGFloat) -> CGFloat {
        containerHeight * (1 - anchor.coveredFraction)
    }

    public static func resolve(
        from current: SnapAnchor,
        dragTranslation: CGFloat,
        predictedEndTranslation: CGFloat,
        containerHeight: CGFloat
    ) -> SnapAnchor {
        guard containerHeight > 0 else { return current }

        // A fling carries the predicted-end well past the finger; a slow settle
        // ends near the finger. Take whichever travels further so velocity wins
        // when present and the final drag position governs when it doesn't.
        let travel = abs(predictedEndTranslation) >= abs(dragTranslation)
            ? predictedEndTranslation
            : dragTranslation
        let currentTop = sheetTop(for: current, containerHeight: containerHeight)
        let projectedTop = (currentTop + travel)
            .clamped(to: minTop(containerHeight) ... maxTop(containerHeight))

        return nearestAnchor(toTop: projectedTop, containerHeight: containerHeight)
    }

    private static func minTop(_ containerHeight: CGFloat) -> CGFloat {
        sheetTop(for: .expanded, containerHeight: containerHeight)
    }

    private static func maxTop(_ containerHeight: CGFloat) -> CGFloat {
        sheetTop(for: .mapFocus, containerHeight: containerHeight)
    }

    private static func nearestAnchor(toTop top: CGFloat, containerHeight: CGFloat) -> SnapAnchor {
        SnapAnchor.allCases.min { lhs, rhs in
            let lhsDistance = abs(sheetTop(for: lhs, containerHeight: containerHeight) - top)
            let rhsDistance = abs(sheetTop(for: rhs, containerHeight: containerHeight) - top)
            return lhsDistance < rhsDistance
        } ?? .peek
    }
}

public struct SnapSheet<Background: View, Ornament: View, Content: View>: View {
    @Binding private var anchor: SnapAnchor
    @State private var dragOffset: CGFloat = 0
    private let ornamentSize: CGFloat
    private let background: Background
    private let ornament: Ornament
    private let content: Content

    public init(
        anchor: Binding<SnapAnchor>,
        ornamentSize: CGFloat = SnapSheetOrnament.defaultSize,
        @ViewBuilder background: () -> Background,
        @ViewBuilder ornament: () -> Ornament,
        @ViewBuilder content: () -> Content
    ) {
        _anchor = anchor
        self.ornamentSize = ornamentSize
        self.background = background()
        self.ornament = ornament()
        self.content = content()
    }

    public var body: some View {
        GeometryReader { geometry in
            let height = geometry.size.height
            let restingTop = SnapResolver.sheetTop(for: anchor, containerHeight: height)
            let currentTop = (restingTop + dragOffset)
                .clamped(to: topBound(height) ... bottomBound(height))

            ZStack(alignment: .top) {
                background
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .ignoresSafeArea()

                sheet(bottomOverhang: currentTop)
                    .frame(height: height)
                    .offset(y: currentTop)
                    .gesture(dragGesture(containerHeight: height))

                ornament
                    .frame(width: ornamentSize, height: ornamentSize)
                    .offset(y: SnapSheetOrnament.offsetY(sheetTop: currentTop, size: ornamentSize))
                    .allowsHitTesting(false)
                    .padding(.trailing, Spacing.m)
                    .frame(maxWidth: .infinity, alignment: .trailing)
            }
            .animation(.interactiveSpring(response: 0.35, dampingFraction: 0.85), value: anchor)
            .animation(.interactiveSpring(response: 0.35, dampingFraction: 0.85), value: dragOffset)
        }
    }

    /// The full-height frame hangs `bottomOverhang` points below the container
    /// once offset; insetting the content by the same amount keeps the bottom
    /// row (the sticky footer) pinned inside the visible area at every anchor.
    private func sheet(bottomOverhang: CGFloat) -> some View {
        VStack(spacing: 0) {
            DragHandle(anchor: $anchor)
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
                    containerHeight: containerHeight
                )
                dragOffset = 0
                anchor = resolved
            }
    }

    private func topBound(_ height: CGFloat) -> CGFloat {
        SnapResolver.sheetTop(for: .expanded, containerHeight: height)
    }

    private func bottomBound(_ height: CGFloat) -> CGFloat {
        SnapResolver.sheetTop(for: .mapFocus, containerHeight: height)
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
        @ViewBuilder background: () -> Background,
        @ViewBuilder content: () -> Content
    ) {
        self.init(anchor: anchor, background: background, ornament: { EmptyView() }, content: content)
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
