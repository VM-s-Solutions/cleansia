import SwiftUI

/// A box and its label. The box sits on the cap band of the label's first line, so a label that wraps hangs under
/// it; the label is set in `bodyMedium`, which the box's alignment reads.
public struct CleansiaCheckbox<Label: View>: View {
    @Binding private var checked: Bool
    private let enabled: Bool
    private let label: Label

    public init(checked: Binding<Bool>, enabled: Bool = true, @ViewBuilder label: () -> Label) {
        _checked = checked
        self.enabled = enabled
        self.label = label()
    }

    public var body: some View {
        Button {
            checked.toggle()
        } label: {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                box.capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                label
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(.vertical, Spacing.xxs)
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }

    private var box: some View {
        Image(systemName: checked ? "checkmark.square.fill" : "square")
            .font(.system(size: 22))
            .foregroundColor(checked ? CleansiaColors.primary : CleansiaColors.quiet)
    }
}

public extension CleansiaCheckbox where Label == Text {
    init(checked: Binding<Bool>, label: String, enabled: Bool = true) {
        self.init(checked: checked, enabled: enabled) { Text(verbatim: label) }
    }
}

#if DEBUG
    struct CleansiaCheckbox_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper(true) { binding in
                VStack(alignment: .leading) {
                    CleansiaCheckbox(checked: binding, label: "I agree to the terms")
                    CleansiaCheckbox(checked: .constant(false), label: "Kitchen, two bathrooms and the hall")
                        .frame(width: 180)
                    CleansiaCheckbox(checked: .constant(true), label: "Done by your crew-mate", enabled: false)
                }
                .padding()
            }
        }
    }
#endif
