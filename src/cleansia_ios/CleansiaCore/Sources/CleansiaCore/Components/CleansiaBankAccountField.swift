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
                // Every segment reads from the left, placeholder and digits alike. Right-aligning the
                // prefix kept its digits against the dash, but it also right-aligned its hint, so the
                // three labels in an empty control started at three different places. Its width is set
                // by the longest placeholder we ship ("Predčíslie"), not by its six digits.
                segment(
                    text: $prefix,
                    placeholder: prefixPlaceholder,
                    maxLength: Self.prefixMaxLength,
                    focus: .prefix
                )
                .frame(width: 76)

                separator("–")

                segment(
                    text: $number,
                    placeholder: numberPlaceholder,
                    maxLength: Self.numberMaxLength,
                    focus: .number
                )
                .frame(maxWidth: .infinity)

                separator("/")

                segment(
                    text: $bankCode,
                    placeholder: bankCodePlaceholder,
                    maxLength: Self.bankCodeMaxLength,
                    focus: .bankCode
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
        focus: Segment
    ) -> some View {
        // A whole account pasted into any one segment is split across all three. It has to be caught
        // here, before the caller's binding sees it: callers sanitise to digits on write, so by the
        // time onChange below runs the slash is gone and the bank code has been run into the number.
        TextField("", text: Binding(
            get: { text.wrappedValue },
            set: { raw in
                guard let pasted = Self.splitPastedAccount(raw) else {
                    text.wrappedValue = raw
                    return
                }
                prefix = pasted.prefix
                number = pasted.number
                if let code = pasted.bankCode { bankCode = code }
            }
        ))
        .background(alignment: .leading) {
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
        .multilineTextAlignment(.leading)
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

    // swiftlint:disable large_tuple
    /// A Czech or Slovak account pasted (or typed on a hardware keyboard) in one go, split into its
    /// three fields. `nil` means the text is an ordinary entry for the segment it landed in, and the
    /// digit clamp takes it — a bare number included: pasted into the number box, it is the number.
    ///
    /// Recognised: `[prefix-]number/bankcode` and `prefix-number` — the number pad types neither
    /// separator, so their presence is what marks a paste — and a CZ or SK IBAN, whose BBAN is
    /// `bankcode(4) prefix(6) number(10)`. Whitespace of every kind (NBSP included) is dropped and an en
    /// or em dash reads as a hyphen, because that is what banking apps put on the clipboard. A missing
    /// prefix clears the old one; a missing bank code keeps it. Nothing is validated beyond shape: the
    /// server owns mod-11, the bank code and the IBAN cross-check. -> /partner-app/onboarding
    static func splitPastedAccount(_ raw: String) -> (prefix: String, number: String, bankCode: String?)? {
        let pasted = String(
            raw.filter { !$0.isWhitespace }.map { $0 == "\u{2013}" || $0 == "\u{2014}" ? "-" : $0 }
        )
        let text = domesticForm(ofIban: pasted) ?? pasted
        guard text.contains("-") || text.contains("/") else { return nil }

        let slash = text.split(separator: "/", omittingEmptySubsequences: false)
        guard slash.count <= 2 else { return nil }
        let bankCode = slash.count == 2 ? String(slash[1]) : nil
        if let bankCode, !isDigits(bankCode, upTo: bankCodeMaxLength) { return nil }

        let dash = slash[0].split(separator: "-", omittingEmptySubsequences: false)
        guard dash.count <= 2 else { return nil }
        let prefix = dash.count == 2 ? String(dash[0]) : ""
        let number = String(dash[dash.count - 1])
        guard dash.count == 1 || isDigits(prefix, upTo: prefixMaxLength),
              isDigits(number, upTo: numberMaxLength) else { return nil }
        return (prefix, number, bankCode)
    }

    // swiftlint:enable large_tuple

    /// `CZ65 0800 0000 1920 0014 5399` reads as `19-2000145399/0800`: the BBAN's zero padding is not
    /// part of the written account. Nil for any other text, other countries' IBANs included.
    private static func domesticForm(ofIban text: String) -> String? {
        let iban = text.uppercased()
        guard iban.count == 24, iban.hasPrefix("CZ") || iban.hasPrefix("SK"),
              iban.dropFirst(2).allSatisfy({ $0.isASCII && $0.isNumber }) else { return nil }
        let bban = iban.dropFirst(4)
        let prefix = bban.dropFirst(bankCodeMaxLength).prefix(prefixMaxLength).drop { $0 == "0" }
        let number = bban.suffix(numberMaxLength).drop { $0 == "0" }
        return (prefix.isEmpty ? "" : "\(prefix)-") + "\(number)/\(bban.prefix(bankCodeMaxLength))"
    }

    private static func isDigits(_ value: String, upTo maxLength: Int) -> Bool {
        (1 ... maxLength).contains(value.count) && value.allSatisfy { $0.isASCII && $0.isNumber }
    }

    private func separator(_ symbol: String) -> some View {
        Text(symbol)
            .font(CleansiaTypography.bodyLarge)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .padding(.horizontal, Spacing.xs)
            .accessibilityHidden(true)
    }
}
