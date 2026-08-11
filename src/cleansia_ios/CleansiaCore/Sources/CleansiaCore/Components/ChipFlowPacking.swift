import CoreGraphics

/// The greedy first-fit wrap packing behind `ChipFlow`, over plain sizes so
/// the algorithm is unit-testable without a view tree. An item wider than
/// `maxWidth` still gets a row of its own (never dropped).
public enum ChipFlowPacking {
    public struct Row: Equatable {
        public var indices: [Int] = []
        public var width: CGFloat = 0
        public var height: CGFloat = 0
    }

    public static func rows(sizes: [CGSize], spacing: CGFloat, maxWidth: CGFloat) -> [Row] {
        var rows: [Row] = []
        var current = Row()
        for (index, size) in sizes.enumerated() {
            if !current.indices.isEmpty, current.width + spacing + size.width > maxWidth {
                rows.append(current)
                current = Row()
            }
            current.width = current.indices.isEmpty ? size.width : current.width + spacing + size.width
            current.height = max(current.height, size.height)
            current.indices.append(index)
        }
        if !current.indices.isEmpty {
            rows.append(current)
        }
        return rows
    }
}
