import SwiftUI

public struct CleansiaTextField: View {
    @Binding private var value: String
    private let label: String
    private let helper: String?
    private let errorText: String?
    private let keyboardType: UIKeyboardType
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
        isPassword: Bool = false,
        enabled: Bool = true,
        transparentContainer: Bool = false
    ) {
        _value = value
        self.label = label
        self.helper = helper
        self.errorText = errorText
        self.keyboardType = keyboardType
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
                    .offset(y: floating ? -14 : 0)
                    // Animate the float on FOCUS, not on the derived `floating`
                    // state. Keying on `floating` animated the label whenever the
                    // value arrived programmatically (pre-fill / async binding),
                    // so on first appearance the hint visibly "dragged" into place
                    // instead of rendering already-floated. Focus is the real
                    // user-interaction trigger; programmatic value changes now snap.
                    .animation(.easeOut(duration: 0.15), value: focused)

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
                .offset(y: floating ? 8 : 0)
                .animation(.easeOut(duration: 0.15), value: focused)
            }
            .padding(.horizontal, Spacing.m)
            .frame(minHeight: 56)
            .background(containerColor)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(borderColor, lineWidth: focused ? 2 : 1)
            )

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
                    CleansiaTextField(value: binding, label: "Email", keyboardType: .emailAddress)
                    CleansiaTextField(value: .constant("secret"), label: "Password", isPassword: true)
                    CleansiaTextField(value: .constant("bad"), label: "Code", errorText: "Invalid code")
                }
                .padding()
            }
        }
    }
#endif
