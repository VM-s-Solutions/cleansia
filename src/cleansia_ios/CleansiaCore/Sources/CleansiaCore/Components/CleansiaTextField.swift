import SwiftUI
import UIKit

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
    @State private var focused = false

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

    private var inputFont: UIFont {
        CleansiaFont.uiFont(.nunito, weight: .regular, size: 16)
    }

    private var field: some View {
        ManagedTextField(
            text: $value,
            focused: $focused,
            isSecure: isPassword && !passwordVisible,
            keyboardType: keyboardType,
            textContentType: textContentType,
            isEnabled: enabled,
            font: inputFont,
            textColor: CleansiaColors.onSurface,
            tintColor: CleansiaColors.primary
        )
        .frame(maxWidth: .infinity)
        .frame(height: inputFont.lineHeight.rounded(.up))
    }
}

/// A `UITextField`-backed field. SwiftUI's `TextField` only writes its binding
/// on user-editing events for the *focused* field, so a Password AutoFill that
/// programmatically sets a non-focused field's text never reaches the binding.
/// This bridge captures both `.editingChanged` and any programmatic text set,
/// so the credential pair always propagates.
private struct ManagedTextField: UIViewRepresentable {
    @Binding var text: String
    @Binding var focused: Bool
    let isSecure: Bool
    let keyboardType: UIKeyboardType
    let textContentType: UITextContentType?
    let isEnabled: Bool
    let font: UIFont
    let textColor: Color
    let tintColor: Color

    func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }

    func makeUIView(context: Context) -> TrackingTextField {
        let field = TrackingTextField()
        field.delegate = context.coordinator
        field.addTarget(
            context.coordinator,
            action: #selector(Coordinator.editingChanged(_:)),
            for: .editingChanged
        )
        field.onProgrammaticTextChange = { [weak coordinator = context.coordinator] newText in
            coordinator?.propagate(newText)
        }
        field.borderStyle = .none
        field.backgroundColor = .clear
        field.clearButtonMode = .never
        field.setContentHuggingPriority(.defaultLow, for: .horizontal)
        field.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        return field
    }

    func updateUIView(_ field: TrackingTextField, context: Context) {
        context.coordinator.parent = self

        if field.text != text {
            field.setTextPreservingBinding(text)
        }

        field.font = font
        field.textColor = UIColor(textColor)
        field.tintColor = UIColor(tintColor)
        field.keyboardType = keyboardType
        field.textContentType = textContentType
        field.isEnabled = isEnabled

        applySecureEntry(field)
        syncFirstResponder(field)
    }

    private func applySecureEntry(_ field: TrackingTextField) {
        guard field.isSecureTextEntry != isSecure else { return }
        let caret = field.selectedTextRange.map {
            field.offset(from: field.beginningOfDocument, to: $0.end)
        }
        field.isSecureTextEntry = isSecure
        // Toggling secure entry primes UITextField to wipe the value on the next
        // keystroke; re-seating the text clears that flag without losing content.
        if let current = field.text, !current.isEmpty {
            field.setTextPreservingBinding("")
            field.setTextPreservingBinding(current)
        }
        if field.isFirstResponder, let caret,
           let position = field.position(from: field.beginningOfDocument, offset: caret)
        {
            field.selectedTextRange = field.textRange(from: position, to: position)
        }
    }

    private func syncFirstResponder(_ field: TrackingTextField) {
        guard field.window != nil else { return }
        if focused, !field.isFirstResponder {
            DispatchQueue.main.async {
                if !field.isFirstResponder { field.becomeFirstResponder() }
            }
        } else if !focused, field.isFirstResponder {
            DispatchQueue.main.async {
                if field.isFirstResponder { field.resignFirstResponder() }
            }
        }
    }

    final class Coordinator: NSObject, UITextFieldDelegate {
        var parent: ManagedTextField

        init(_ parent: ManagedTextField) {
            self.parent = parent
        }

        @objc func editingChanged(_ field: UITextField) {
            propagate(field.text ?? "")
        }

        func propagate(_ newText: String) {
            if parent.text != newText { parent.text = newText }
        }

        func textFieldDidBeginEditing(_: UITextField) {
            if !parent.focused { parent.focused = true }
        }

        func textFieldDidEndEditing(_: UITextField) {
            if parent.focused { parent.focused = false }
        }

        func textFieldShouldReturn(_ field: UITextField) -> Bool {
            field.resignFirstResponder()
            return true
        }
    }
}

private final class TrackingTextField: UITextField {
    var onProgrammaticTextChange: ((String) -> Void)?
    private var isApplyingBinding = false

    override var text: String? {
        didSet {
            guard !isApplyingBinding else { return }
            onProgrammaticTextChange?(text ?? "")
        }
    }

    func setTextPreservingBinding(_ newText: String) {
        isApplyingBinding = true
        text = newText
        isApplyingBinding = false
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
