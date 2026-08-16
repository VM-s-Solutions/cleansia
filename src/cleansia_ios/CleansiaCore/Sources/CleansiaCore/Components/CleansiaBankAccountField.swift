import SwiftUI

/// A Czech bank account entered as ONE control: `prefix – number / bank code`.
///
/// Three fields, one border. The separators are drawn rather than typed, because the format belongs to
/// the bank and not to the person copying an account off a statement.
///
/// **The border and the focus colour belong to the container, never to a segment.** A focus outline
/// around one third of the control would undo the grouping the control exists to create — which is the
/// whole point: the account is one thing to the person entering it, even though it is three columns to
/// the server.
///
/// It binds the three values separately rather than one joined string. They are three columns
/// server-side, each with its own validation, and joining them here would mean splitting them again on
/// save — a round trip that can only lose information.
///
/// **Each segment carries its own placeholder**, because one border around three boxes removes the only
/// other cue for which box is which. Czech online banking (Raiffeisen among them) names all three in
/// place; without that, an empty control is three anonymous gaps around a dash and a slash. The segment
/// widths below are sized to the *placeholder*, not to the digits, for the same reason — a hint that is
/// clipped to "Předčí…" answers nothing.
///
/// Twins: `cleansia-bank-account` (web) and `CleansiaBankAccountInput` (Android). Keep the three in step.
public struct CleansiaBankAccountField: View {
    /// Czech account maxima — prefix 6 digits, number 10, bank code 4.
    private static let prefixMaxLength = 6
    private static let numberMaxLength = 10
    private static let bankCodeMaxLength = 4

    @Binding private var prefix: String
    @Binding private var number: String
    @Binding private var bankCode: String

    private let label: String
    private let prefixPlaceholder: String
    private let numberPlaceholder: String
    private let bankCodePlaceholder: String
    private let helper: String?
    private let errorText: String?
    private let enabled: Bool

    @FocusState private var focusedSegment: Segment?

    private enum Segment: Hashable {
        case prefix, number, bankCode
    }

    public init(
        prefix: Binding<String>,
        number: Binding<String>,
        bankCode: Binding<String>,
        label: String,
        prefixPlaceholder: String = "",
        numberPlaceholder: String = "",
        bankCodePlaceholder: String = "",
        helper: String? = nil,
        errorText: String? = nil,
        enabled: Bool = true
    ) {
        _prefix = prefix
        _number = number
        _bankCode = bankCode
        self.label = label
        self.prefixPlaceholder = prefixPlaceholder
        self.numberPlaceholder = numberPlaceholder
        self.bankCodePlaceholder = bankCodePlaceholder
        self.helper = helper
        self.errorText = errorText
        self.enabled = enabled
    }

    private var isError: Bool {
        errorText != nil
    }

    private var isFocused: Bool {
        focusedSegment != nil
    }

    private var borderColor: Color {
        if isError { return CleansiaColors.error }
        return isFocused ? CleansiaColors.primary : CleansiaColors.outline
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)

            HStack(spacing: 0) {
                // Prefix is right-aligned: it is optional and usually empty, so left-aligning it would
                // leave a gap before the dash and break the account's shape. Its width is set by the
                // longest placeholder we ship ("Predčíslie"), not by its six digits.
                segment(
                    text: $prefix,
                    placeholder: prefixPlaceholder,
                    maxLength: Self.prefixMaxLength,
                    focus: .prefix,
                    alignment: .trailing
                )
                .frame(width: 76)

                separator("–")

                segment(
                    text: $number,
                    placeholder: numberPlaceholder,
                    maxLength: Self.numberMaxLength,
                    focus: .number,
                    alignment: .leading
                )
                .frame(maxWidth: .infinity)

                separator("/")

                segment(
                    text: $bankCode,
                    placeholder: bankCodePlaceholder,
                    maxLength: Self.bankCodeMaxLength,
                    focus: .bankCode,
                    alignment: .leading
                )
                .frame(width: 52)
            }
            .padding(.horizontal, Spacing.m)
            .frame(minHeight: 56)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(borderColor, lineWidth: isFocused ? 2 : 1)
            )
            .animation(.easeOut(duration: 0.2), value: isFocused)

            if let message = errorText ?? helper {
                Text(message)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
            }
        }
    }

    /// The placeholder is drawn as an overlay rather than passed to `TextField`, so it can carry its own
    /// (smaller) font — the segments are narrow and the system placeholder inherits `bodyLarge`, which
    /// clips "Predčíslie". It stays visible while the segment is empty and focused: the person is mid-way
    /// through an account number, and "which box am I in" is exactly what a caret does not answer.
    private func segment(
        text: Binding<String>,
        placeholder: String,
        maxLength: Int,
        focus: Segment,
        alignment: TextAlignment
    ) -> some View {
        TextField("", text: text)
            .background(alignment: alignment == .trailing ? .trailing : .leading) {
                if text.wrappedValue.isEmpty, !placeholder.isEmpty {
                    Text(placeholder)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                        .allowsHitTesting(false)
                }
            }
            .keyboardType(.numberPad)
            .textContentType(nil)
            .multilineTextAlignment(alignment)
            .font(CleansiaTypography.bodyLarge)
            .foregroundColor(CleansiaColors.onSurface)
            .disabled(!enabled)
            .focused($focusedSegment, equals: focus)
            // Digits only, clamped to the segment's own maximum. Doing it here rather than in the
            // view model keeps every caller's binding a plain String while making an over-long or
            // pasted-with-punctuation entry impossible to produce.
            // Single-parameter onChange: the package floor is iOS 16 (Package.swift), where the
            // two-parameter overload does not exist yet.
            .onChange(of: text.wrappedValue) { newValue in
                let digits = String(newValue.filter(\.isNumber).prefix(maxLength))
                if digits != newValue { text.wrappedValue = digits }
            }
    }

    private func separator(_ symbol: String) -> some View {
        Text(symbol)
            .font(CleansiaTypography.bodyLarge)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .padding(.horizontal, Spacing.xs)
            .accessibilityHidden(true)
    }
}
