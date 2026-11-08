import SwiftUI

/// Compose `FlowRow` parity — lays its subviews left to right and wraps onto a new line only when the
/// proposed width runs out. Shared by both apps: the partner order chips and the customer membership
/// perk pills.
///
/// With `wrapsWideItems` an item wider than the line is offered the line's width, so its own words wrap inside it
/// instead of running past the edge; off by default, so an item keeps its ideal size.
public struct ChipFlow: Layout {
    public var spacing: CGFloat
    public var wrapsWideItems: Bool

    public init(spacing: CGFloat = Spacing.xxs, wrapsWideItems: Bool = false) {
        self.spacing = spacing
        self.wrapsWideItems = wrapsWideItems
    }

    public func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache _: inout ()) -> CGSize {
        let rows = rows(for: subviews, in: proposal.width ?? .infinity)
        let width = rows.map(\.width).max() ?? 0
        let rowsHeight: CGFloat = rows.map(\.height).reduce(0, +)
        let height = rowsHeight + spacing * CGFloat(max(rows.count - 1, 0))
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
                let size = size(of: subviews[index], in: bounds.width)
                subviews[index].place(
                    at: CGPoint(x: cursorX, y: rowTop + (row.height - size.height) / 2),
                    proposal: wrapsWideItems ? ProposedViewSize(size) : .unspecified
                )
                cursorX += size.width + spacing
            }
            rowTop += row.height + spacing
        }
    }

    private func size(of subview: LayoutSubview, in maxWidth: CGFloat) -> CGSize {
        let ideal = subview.sizeThatFits(.unspecified)
        guard wrapsWideItems, ideal.width > maxWidth else { return ideal }
        return subview.sizeThatFits(ProposedViewSize(width: maxWidth, height: nil))
    }

    private func rows(for subviews: Subviews, in maxWidth: CGFloat) -> [ChipFlowPacking.Row] {
        ChipFlowPacking.rows(
            sizes: subviews.map { size(of: $0, in: maxWidth) },
            spacing: spacing,
            maxWidth: maxWidth
        )
    }
}
