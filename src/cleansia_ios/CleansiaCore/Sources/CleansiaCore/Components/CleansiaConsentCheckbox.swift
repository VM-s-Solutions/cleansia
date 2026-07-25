import SwiftUI

/// A consent row whose sentence carries tappable legal links. `CleansiaCheckbox`
/// wraps its whole row in one `Button`, which swallows every tap inside the
/// label — here only the glyph toggles, so the links reach `openURL`.
public struct CleansiaConsentCheckbox: View {
    @Binding private var checked: Bool
    private let sentence: AttributedString
    private let toggleAccessibilityLabel: String

    public init(checked: Binding<Bool>, markdown: String, toggleAccessibilityLabel: String) {
        _checked = checked
        sentence = Self.styled(ConsentMarkdown.attributed(markdown))
        self.toggleAccessibilityLabel = toggleAccessibilityLabel
    }

    /// Colour + underline are set on the runs themselves: a `Text`-level
    /// `foregroundColor` would otherwise flatten the links into body copy, and
    /// colour alone is not an accessible affordance.
    private static func styled(_ parsed: AttributedString) -> AttributedString {
        var styled = parsed
        let linked = styled.runs.compactMap { $0.link == nil ? nil : $0.range }
        for range in linked {
            styled[range].foregroundColor = CleansiaColors.primary
            styled[range].underlineStyle = .single
        }
        return styled
    }

    public var body: some View {
        HStack(alignment: .top, spacing: Spacing.xxs) {
            Button {
                checked.toggle()
            } label: {
                Image(systemName: checked ? "checkmark.square.fill" : "square")
                    .font(.system(size: 22))
                    .foregroundColor(checked ? CleansiaColors.primary : CleansiaColors.outline)
                    // The glyph keeps its 28pt LAYOUT footprint (so it stays aligned with the first line
                    // of the sentence) while the TAP target is padded out to the 44x44 HIG minimum. This
                    // is the only control gating registration and the row it replaces was tappable across
                    // its full width, so a glyph-sized target would read as "sign up is broken".
                    .frame(width: 28, height: 28)
                    .padding(8)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            // Cancel the hit-area padding so the row's layout is unchanged.
            .padding(-8)
            // A short label, NOT the sentence: the sentence is already its own accessibility element
            // below (and carries the actionable links), so repeating it here makes VoiceOver read the
            // whole legal text twice.
            .accessibilityLabel(Text(toggleAccessibilityLabel))
            .accessibilityAddTraits(checked ? .isSelected : [])

            Text(sentence)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .tint(CleansiaColors.primary)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.vertical, Spacing.xxs)
    }
}

#if DEBUG
    struct CleansiaConsentCheckbox_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper(true) { binding in
                VStack(alignment: .leading, spacing: Spacing.m) {
                    CleansiaConsentCheckbox(
                        checked: binding,
                        markdown: "I agree to the [Terms of Service](cleansia://terms) "
                            + "and [Privacy Policy](cleansia://privacy)",
                        toggleAccessibilityLabel: "Accept terms and privacy policy"
                    )
                    CleansiaConsentCheckbox(
                        checked: .constant(false),
                        markdown: "A translation that dropped the markup still renders",
                        toggleAccessibilityLabel: "Accept terms and privacy policy"
                    )
                }
                .padding()
            }
        }
    }
#endif
