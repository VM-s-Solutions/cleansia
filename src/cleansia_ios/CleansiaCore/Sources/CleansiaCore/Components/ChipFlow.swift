import SwiftUI

/// Compose `FlowRow` parity — lays its subviews left to right and wraps onto a new line only when the
/// proposed width runs out. Shared by both apps: the partner order chips and the customer membership
/// perk pills.
public struct ChipFlow: Layout {
    public var spacing: CGFloat

    public init(spacing: CGFloat = Spacing.xxs) {
        self.spacing = spacing
    }

    public func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let rows = rows(for: subviews, in: proposal.width ?? .infinity)
        let width = rows.map(\.width).max() ?? 0
        let height = rows.map(\.height).reduce(0, +) + spacing * CGFloat(max(rows.count - 1, 0))
        return CGSize(width: proposal.width ?? width, height: height)
    }

    public func placeSubviews(
        in bounds: CGRect,
        proposal _: ProposedViewSize,
        subviews: Subviews,
        cache _: inout ()
    ) {
        var rowTop = bounds.minY
        for row in rows(for: subviews, in: bounds.width) {
            var cursorX = bounds.minX
            for index in row.indices {
                let size = subviews[index].sizeThatFits(.unspecified)
                subviews[index].place(
                    at: CGPoint(x: cursorX, y: rowTop + (row.height - size.height) / 2),
                    proposal: .unspecified
                )
                cursorX += size.width + spacing
            }
            rowTop += row.height + spacing
        }
    }

    private func rows(for subviews: Subviews, in maxWidth: CGFloat) -> [ChipFlowPacking.Row] {
        ChipFlowPacking.rows(
            sizes: subviews.map { $0.sizeThatFits(.unspecified) },
            spacing: spacing,
            maxWidth: maxWidth
        )
    }
}
