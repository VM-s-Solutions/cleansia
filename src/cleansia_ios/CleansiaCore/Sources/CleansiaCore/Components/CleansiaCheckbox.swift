import SwiftUI

public struct CleansiaCheckbox: View {
    @Binding private var checked: Bool
    private let label: String

    public init(checked: Binding<Bool>, label: String) {
        _checked = checked
        self.label = label
    }

    public var body: some View {
        Button {
            checked.toggle()
        } label: {
            HStack(spacing: Spacing.xxs) {
                Image(systemName: checked ? "checkmark.square.fill" : "square")
                    .font(.system(size: 22))
                    .foregroundColor(checked ? CleansiaColors.primary : CleansiaColors.outline)
                Text(label)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            .padding(.vertical, Spacing.xxs)
        }
        .buttonStyle(.plain)
    }
}

#if DEBUG
    struct CleansiaCheckbox_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper(true) { binding in
                CleansiaCheckbox(checked: binding, label: "I agree to the terms")
                    .padding()
            }
        }
    }
#endif
