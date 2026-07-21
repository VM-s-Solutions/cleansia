import SwiftUI

public struct CleansiaTextField: View {
    @Binding private var value: String
    private let label: String
    private let helper: String?
    private let errorText: String?
    private let keyboardType: UIKeyboardType
    private let textContentType: UITextContentType?
    private let isPassword: Bool
    private let enabled: Bool
    private let transparentContainer: Bool

    @State private var passwordVisible = false
    @FocusState private var focused: Bool

    public init(
        value: Binding<String>,
        label: String,
        helper: String? = nil,
        errorText: String? = nil,
        keyboardType: UIKeyboardType = .default,
        textContentType: UITextContentType? = nil,
        isPassword: Bool = false,
        enabled: Bool = true,
        transparentContainer: Bool = false
    ) {
        _value = value
        self.label = label
        self.helper = helper
        self.errorText = errorText
        self.keyboardType = keyboardType
        self.textContentType = textContentType
        self.isPassword = isPassword
        self.enabled = enabled
        self.transparentContainer = transparentContainer
    }

    private var isError: Bool {
        errorText != nil
    }

    private var floating: Bool {
        focused || !value.isEmpty
    }

    private var borderColor: Color {
        if isError { return CleansiaColors.error }
        return focused ? CleansiaColors.primary : CleansiaColors.outline
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            ZStack(alignment: .leading) {
                Text(label)
                    .font(floating ? CleansiaTypography.labelMedium : CleansiaTypography.bodyLarge)
                    .foregroundColor(floating ? floatingLabelColor : CleansiaColors.onSurfaceVariant)
                    .offset(y: floating ? -13 : 0)

                HStack {
                    field
                    if isPassword {
                        Button {
                            passwordVisible.toggle()
                        } label: {
                            Image(systemName: passwordVisible ? "eye.slash" : "eye")
                                .font(.system(size: 20))
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.top, floating ? Spacing.s : 0)
            }
            // One shared transaction for the label + field so they move as a
            // single coordinated motion, never a two-part "drag". Keyed on
            // FOCUS, not the derived `floating`: a programmatic value change
            // (pre-fill / async binding) doesn't touch focus, so the label snaps
            // into its floated place instead of sliding in on first appearance.
            .animation(.easeOut(duration: 0.2), value: focused)
            .padding(.horizontal, Spacing.m)
            .frame(minHeight: 56)
            .background(containerColor)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(borderColor, lineWidth: focused ? 2 : 1)
            )
            .contentShape(Rectangle())
            .onTapGesture {
                if enabled { focused = true }
            }

            if let supporting = errorText ?? helper {
                Text(supporting)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
                    .padding(.horizontal, Spacing.m)
            }
        }
    }

    private var floatingLabelColor: Color {
        if isError { return CleansiaColors.error }
        return focused ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant
    }

    private var containerColor: Color {
        transparentContainer ? .clear : CleansiaColors.surface
    }

    private var field: some View {
        Group {
            if isPassword, !passwordVisible {
                SecureField("", text: $value)
            } else {
                TextField("", text: $value)
                    .keyboardType(keyboardType)
            }
        }
        .textContentType(textContentType)
        .font(CleansiaTypography.bodyLarge)
        .foregroundColor(CleansiaColors.onSurface)
        .tint(CleansiaColors.primary)
        .focused($focused)
        .disabled(!enabled)
        .frame(maxWidth: .infinity)
    }
}

#if DEBUG
    struct CleansiaTextField_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper("") { binding in
                VStack(spacing: Spacing.m) {
                    CleansiaTextField(
                        value: binding,
                        label: "Email",
                        keyboardType: .emailAddress,
                        textContentType: .emailAddress
                    )
                    CleansiaTextField(
                        value: .constant("secret"),
                        label: "Password",
                        textContentType: .password,
                        isPassword: true
                    )
                    CleansiaTextField(value: .constant("bad"), label: "Code", errorText: "Invalid code")
                }
                .padding()
            }
        }
    }
#endif
