import SwiftUI

public struct CleansiaSecureField: View {
    @Binding private var value: String
    private let label: String
    private let helper: String?
    private let errorText: String?
    private let enabled: Bool
    private let transparentContainer: Bool

    public init(
        value: Binding<String>,
        label: String,
        helper: String? = nil,
        errorText: String? = nil,
        enabled: Bool = true,
        transparentContainer: Bool = false
    ) {
        _value = value
        self.label = label
        self.helper = helper
        self.errorText = errorText
        self.enabled = enabled
        self.transparentContainer = transparentContainer
    }

    public var body: some View {
        CleansiaTextField(
            value: $value,
            label: label,
            helper: helper,
            errorText: errorText,
            isPassword: true,
            enabled: enabled,
            transparentContainer: transparentContainer
        )
    }
}

public struct LabelledDivider: View {
    private let label: String

    public init(_ label: String) {
        self.label = label
    }

    public var body: some View {
        HStack(spacing: Spacing.m) {
            line
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            line
        }
        .padding(.vertical, Spacing.xs)
    }

    private var line: some View {
        Rectangle()
            .fill(CleansiaColors.outlineVariant)
            .frame(height: 1)
            .frame(maxWidth: .infinity)
    }
}

#if DEBUG
    struct Components_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper("secret") { binding in
                VStack(spacing: Spacing.m) {
                    CleansiaSecureField(value: binding, label: "Password")
                    LabelledDivider("OR")
                }
                .padding()
            }
        }
    }
#endif
