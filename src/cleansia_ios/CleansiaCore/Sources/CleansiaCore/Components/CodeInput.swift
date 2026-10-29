import SwiftUI
import UIKit

public struct CodeInput: View {
    @Binding private var code: String
    private let length: Int
    private let showsPasteAction: Bool
    @State private var focused = false

    public init(code: Binding<String>, length: Int = 6, showsPasteAction: Bool = true) {
        _code = code
        self.length = length
        self.showsPasteAction = showsPasteAction
    }

    /// The field is the control and the boxes only draw it, so a tap focuses the field and a long press opens
    /// its edit menu.
    public var body: some View {
        VStack(spacing: Spacing.s) {
            ZStack {
                HStack(spacing: Spacing.xs) {
                    ForEach(0 ..< length, id: \.self) { index in
                        box(at: index)
                    }
                }
                .allowsHitTesting(false)
                .accessibilityHidden(true)

                CodeTextField(code: $code, focused: $focused, length: length)
                    .frame(maxWidth: .infinity)
                    .frame(height: 56)
            }
            if showsPasteAction {
                PasteButton(payloadType: String.self) { pasted in
                    guard let text = pasted.first else { return }
                    Task { @MainActor in code = Self.pasting(text, onto: code, length: length) }
                }
                .labelStyle(.iconOnly)
                .buttonBorderShape(.capsule)
                .tint(CleansiaColors.primary)
                .dynamicTypeSize(DynamicTypeSize.accessibility1...)
            }
        }
        .frame(maxWidth: .infinity)
        .onAppear { focused = true }
    }

    /// The clipboard's code replaces what was typed, so a fragment shows as itself, incomplete, and is never
    /// completed from digits already in the boxes; text without digits leaves the code as it is. The field's
    /// own Paste still inserts at the caret: that is editing the text, and this a gesture of its own.
    static func pasting(_ text: String, onto code: String, length: Int) -> String {
        let pasted = OneTimeCode.accept(text, replacing: "", length: length)
        return pasted.isEmpty ? code : pasted
    }

    private func box(at index: Int) -> some View {
        let character = index < code.count
            ? String(Array(code)[index])
            : ""
        let isFocusedBox = index == code.count
        return Text(character)
            .cleansiaFont(.poppins(.semibold, size: 24))
            .foregroundColor(CleansiaColors.onSurface)
            .frame(width: 44, height: 56)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(
                        isFocusedBox ? CleansiaColors.primary : CleansiaColors.outline,
                        lineWidth: isFocusedBox ? 2 : 1
                    )
            )
    }
}

/// A UIKit field, not SwiftUI's: on iOS 26 a SwiftUI `TextField` keeps the raw text of an edit its binding
/// rewrote and reports it again later, so a code pasted onto a half-typed one came back run together with it.
/// Its text is invisible and one point high, and the caret is kept after the code, where the boxes say the next
/// digit goes.
struct CodeTextField: UIViewRepresentable {
    @Binding var code: String
    @Binding var focused: Bool
    let length: Int

    func makeCoordinator() -> Coordinator {
        Coordinator(self)
    }

    func makeUIView(context: Context) -> FocusingTextField {
        let field = FocusingTextField()
        field.delegate = context.coordinator
        field.addTarget(
            context.coordinator,
            action: #selector(Coordinator.editingChanged(_:)),
            for: .editingChanged
        )
        field.keyboardType = .numberPad
        field.textContentType = .oneTimeCode
        field.accessibilityLabel = CoreL10n.localized("code_input.label")
        field.font = .systemFont(ofSize: 1)
        field.textColor = .clear
        field.tintColor = .clear
        field.borderStyle = .none
        field.backgroundColor = .clear
        for axis in [NSLayoutConstraint.Axis.horizontal, .vertical] {
            field.setContentHuggingPriority(.defaultLow, for: axis)
            field.setContentCompressionResistancePriority(.defaultLow, for: axis)
        }
        return field
    }

    func updateUIView(_ field: FocusingTextField, context: Context) {
        context.coordinator.parent = self
        context.coordinator.seat(code, in: field)
        field.wantsFocus = focused
        syncFirstResponder(field)
    }

    private func syncFirstResponder(_ field: UITextField) {
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
        var parent: CodeTextField
        /// The code the field last showed, which an edit UIKit made on its own is measured against.
        private var seated = ""

        init(_ parent: CodeTextField) {
            self.parent = parent
        }

        func seat(_ code: String, in field: UITextField) {
            seated = code
            if field.text != code { field.text = code }
        }

        func textField(
            _ field: UITextField,
            shouldChangeCharactersIn nsRange: NSRange,
            replacementString string: String
        ) -> Bool {
            let current = field.text ?? ""
            guard let range = Range(nsRange, in: current) else { return false }
            accept(current.replacingCharacters(in: range, with: string), in: field)
            return false
        }

        /// AutoFill can seat text without asking the delegate first.
        @objc func editingChanged(_ field: UITextField) {
            accept(field.text ?? "", in: field)
        }

        func textFieldDidChangeSelection(_ field: UITextField) {
            guard let selection = field.selectedTextRange,
                  field.offset(from: selection.start, to: field.endOfDocument) != 0
            else { return }
            field.selectedTextRange = field.textRange(from: field.endOfDocument, to: field.endOfDocument)
        }

        func textFieldDidBeginEditing(_: UITextField) {
            if !parent.focused { parent.focused = true }
        }

        func textFieldDidEndEditing(_: UITextField) {
            if parent.focused { parent.focused = false }
        }

        private func accept(_ raw: String, in field: UITextField) {
            let code = OneTimeCode.accept(raw, replacing: seated, length: parent.length)
            seat(code, in: field)
            if parent.code != code { parent.code = code }
        }
    }

    /// The screen asks for focus as it appears, which can be before the field is in a window to take it.
    final class FocusingTextField: UITextField {
        var wantsFocus = false

        override func didMoveToWindow() {
            super.didMoveToWindow()
            if wantsFocus, window != nil, !isFirstResponder { becomeFirstResponder() }
        }
    }
}

#if DEBUG
    struct CodeInput_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper("123") { binding in
                CodeInput(code: binding)
                    .padding()
            }
        }
    }
#endif
