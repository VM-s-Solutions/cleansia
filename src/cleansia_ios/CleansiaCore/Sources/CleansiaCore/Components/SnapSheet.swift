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

public struct SnapSheet<Background: View, Content: View>: View {
    @Binding private var anchor: SnapAnchor
    @State private var dragOffset: CGFloat = 0
    private let background: Background
    private let content: Content

    public init(
        anchor: Binding<SnapAnchor>,
        @ViewBuilder background: () -> Background,
        @ViewBuilder content: () -> Content
    ) {
        _anchor = anchor
        self.background = background()
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

                sheet
                    .frame(height: height)
                    .offset(y: currentTop)
                    .gesture(dragGesture(containerHeight: height))
                    .animation(.interactiveSpring(response: 0.35, dampingFraction: 0.85), value: anchor)
                    .animation(.interactiveSpring(response: 0.35, dampingFraction: 0.85), value: dragOffset)
            }
        }
    }

    private var sheet: some View {
        VStack(spacing: 0) {
            DragHandle()
            content
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .background(
            UnevenRoundedRectangle(
                topLeadingRadius: CornerRadius.large,
                topTrailingRadius: CornerRadius.large
            )
            .fill(CleansiaColors.surface)
        )
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

private struct DragHandle: View {
    var body: some View {
        Capsule()
            .fill(CleansiaColors.outline)
            .frame(width: 36, height: 5)
            .padding(.top, Spacing.xs)
            .padding(.bottom, Spacing.xxs)
            .frame(maxWidth: .infinity)
            .contentShape(Rectangle())
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
