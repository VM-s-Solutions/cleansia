import SwiftUI
import UIKit

/// Phone field with region-aware format-as-you-type (`CleansiaPhoneInput.kt`
/// parity). The bound `value` is always the wire format — a leading `+` plus
/// digits — while the mask lives only in the visible text.
public struct CleansiaPhoneInput: View {
    @Binding private var value: String
    private let label: String
    private let helper: String?
    private let errorText: String?
    private let enabled: Bool
    private let transparentContainer: Bool
    private let engine: PhoneMaskEngine

    @State private var focused = false

    public init(
        value: Binding<String>,
        label: String,
        helper: String? = nil,
        errorText: String? = nil,
        enabled: Bool = true,
        transparentContainer: Bool = false,
        engine: PhoneMaskEngine = .phoneNumberKit
    ) {
        _value = value
        self.label = label
        self.helper = helper
        self.errorText = errorText
        self.enabled = enabled
        self.transparentContainer = transparentContainer
        self.engine = engine
    }

    private var isError: Bool {
        errorText != nil
    }

    private var floating: Bool {
        focused || !value.isEmpty
    }

    private var borderColor: Color {
        if !enabled { return CleansiaColors.disabledOutline }
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

                field
                    .padding(.top, floating ? Spacing.s : 0)
            }
            .animation(.easeOut(duration: 0.2), value: focused)
            .padding(.horizontal, Spacing.m)
            .frame(minHeight: 56)
            .background(transparentContainer ? .clear : CleansiaColors.surface)
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
        .disabled(!enabled)
    }

    // Deliberately NOT normalising the bound value on load. A number stored by another client can
    // carry separators ("+420 728089247" from web), and rewriting it on appear would silently change
    // what a save submits even when the user never touched the field — while server-side phone
    // uniqueness is an EXACT string match (UserRepository.GetByPhoneNumber / ExistsWithPhoneNumber)
    // that UpdateCurrentUser runs on EVERY profile save. That combination could rewrite a user's
    // stored number, or fail an unrelated save against another account holding the sanitised form.
    // The value is formatted for DISPLAY only; the wire value changes solely through a user edit,
    // which already yields the sanitised form via MaskedPhoneTextField's edit path.

    private var floatingLabelColor: Color {
        if !enabled { return CleansiaColors.disabledInk }
        if isError { return CleansiaColors.error }
        return focused ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant
    }

    private var inputFont: UIFont {
        CleansiaFont.uiFont(.nunito, weight: .regular, size: 16)
    }

    private var field: some View {
        MaskedPhoneTextField(
            value: $value,
            focused: $focused,
            engine: engine,
            isEnabled: enabled,
            font: inputFont,
            textColor: enabled ? CleansiaColors.onSurface : CleansiaColors.disabledInk,
            tintColor: CleansiaColors.primary
        )
        .frame(maxWidth: .infinity)
        .frame(height: inputFont.lineHeight.rounded(.up))
    }
}

struct MaskedPhoneTextField: UIViewRepresentable {
    @Binding var value: String
    @Binding var focused: Bool
    let engine: PhoneMaskEngine
    let isEnabled: Bool
    let font: UIFont
    let textColor: Color
    let tintColor: Color

    func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }

    func makeUIView(context: Context) -> UITextField {
        let field = UITextField()
        field.delegate = context.coordinator
        field.addTarget(
            context.coordinator,
            action: #selector(Coordinator.editingChanged(_:)),
            for: .editingChanged
        )
        // Born disabled when it should be — without this the field is enabled until the first
        // updateUIView, and every rebuild of the form subtree reopens that window.
        field.isEnabled = isEnabled
        field.borderStyle = .none
        field.backgroundColor = .clear
        field.clearButtonMode = .never
        field.keyboardType = .phonePad
        field.textContentType = .telephoneNumber
        field.setContentHuggingPriority(.defaultLow, for: .horizontal)
        field.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
        return field
    }

    func updateUIView(_ field: UITextField, context: Context) {
        context.coordinator.parent = self
        seat(value, in: field)
        field.font = font
        field.textColor = UIColor(textColor)
        field.tintColor = UIColor(tintColor)
        field.isEnabled = isEnabled
        syncFirstResponder(field)
    }

    /// Only re-seat when the field is showing something else, so the caret
    /// survives the re-render that each keystroke triggers.
    func seat(_ wireValue: String, in field: UITextField) {
        guard PhoneNumberSanitizer.sanitize(field.text ?? "") != wireValue else { return }
        let edit = engine.reformat(wireValue)
        field.text = edit.text
        field.moveCaret(to: edit.caret)
    }

    private func syncFirstResponder(_ field: UITextField) {
        guard field.window != nil else { return }
        // A disabled field never takes focus, and gives it up if it somehow holds it.
        guard isEnabled else {
            if field.isFirstResponder {
                DispatchQueue.main.async { field.resignFirstResponder() }
            }
            return
        }
        if focused, !field.isFirstResponder {
            DispatchQueue.main.async {
                // Re-checked: this runs a runloop turn later than the guard above.
                if field.isEnabled, !field.isFirstResponder { field.becomeFirstResponder() }
            }
        } else if !focused, field.isFirstResponder {
            DispatchQueue.main.async {
                if field.isFirstResponder { field.resignFirstResponder() }
            }
        }
    }

    final class Coordinator: NSObject, UITextFieldDelegate {
        var parent: MaskedPhoneTextField

        init(_ parent: MaskedPhoneTextField) {
            self.parent = parent
        }

        func textField(
            _ field: UITextField,
            shouldChangeCharactersIn nsRange: NSRange,
            replacementString string: String
        ) -> Bool {
            let current = field.text ?? ""
            guard let range = Range(nsRange, in: current) else { return false }
            let edit = parent.engine.edit(
                current: current,
                range: current.distance(from: current.startIndex, to: range.lowerBound) ..<
                    current.distance(from: current.startIndex, to: range.upperBound),
                replacement: string
            )
            apply(edit, to: field)
            return false
        }

        /// AutoFill and the QuickType bar can seat text without going through
        /// `shouldChangeCharactersIn`, which would leave an unmasked value in a
        /// field the binding never hears about.
        @objc func editingChanged(_ field: UITextField) {
            apply(parent.engine.reformat(field.text ?? ""), to: field)
        }

        private func apply(_ edit: PhoneMaskEdit, to field: UITextField) {
            guard parent.isEnabled else { return }
            if field.text != edit.text {
                field.text = edit.text
                field.moveCaret(to: edit.caret)
            }
            if parent.value != edit.wireValue { parent.value = edit.wireValue }
        }

        func textFieldDidBeginEditing(_ field: UITextField) {
            guard parent.isEnabled else {
                field.resignFirstResponder()
                return
            }
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

private extension UITextField {
    func moveCaret(to characterOffset: Int) {
        let content = text ?? ""
        let index = content.index(
            content.startIndex,
            offsetBy: max(characterOffset, 0),
            limitedBy: content.endIndex
        ) ?? content.endIndex
        let utf16Offset = content.utf16.distance(from: content.utf16.startIndex, to: index)
        guard let caret = position(from: beginningOfDocument, offset: utf16Offset) else { return }
        selectedTextRange = textRange(from: caret, to: caret)
    }
}

#if DEBUG
    struct CleansiaPhoneInput_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper("+420728089247") { binding in
                VStack(spacing: Spacing.m) {
                    CleansiaPhoneInput(value: binding, label: "Phone")
                    CleansiaPhoneInput(
                        value: .constant(""),
                        label: "Phone",
                        helper: "We only use it to reach you about a booking"
                    )
                    CleansiaPhoneInput(value: .constant("+420"), label: "Phone", errorText: "Enter a phone number")
                }
                .padding()
            }
        }
    }
#endif
