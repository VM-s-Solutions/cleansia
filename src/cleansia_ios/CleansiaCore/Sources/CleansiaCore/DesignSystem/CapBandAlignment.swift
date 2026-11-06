import SwiftUI

private struct CapBandAlignment: ViewModifier {
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    let line: VerticalAlignment
    let style: CleansiaTextStyle

    func body(content: Content) -> some View {
        let capHeight = style.uiFont(for: dynamicTypeSize).capHeight
        content.alignmentGuide(line) { $0[VerticalAlignment.center] + capHeight / 2 }
    }
}

public extension View {
    /// Centres a glyph on the cap band of the text line its stack aligns on: the baseline less half the cap height of
    /// `style`, the visual centre of a capitalised word. In an `HStack(alignment: .firstTextBaseline)` that is the
    /// first line, for a leading glyph; in `.lastTextBaseline` it is the last line, for a trailing one.
    func capBandAligned(_ line: VerticalAlignment = .firstTextBaseline, of style: CleansiaTextStyle) -> some View {
        modifier(CapBandAlignment(line: line, style: style))
    }
}
