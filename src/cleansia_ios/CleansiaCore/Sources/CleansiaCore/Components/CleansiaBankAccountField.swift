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
    /// Bumped by every edit the control writes itself, to rebuild the segments. On iOS 26 a TextField whose
    /// binding setter rewrites the edit it is handling keeps drawing the raw text — the whole pasted account,
    /// over the box's placeholder — until focus moves, whether or not that box's own value changed.
    @State private var pasteRevision = 0

    enum Segment: Hashable {
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
        // An edit is read here, before the caller's binding sees it: callers sanitise to digits on write, so by
        // the time onChange below runs a pasted account's slash is gone and its bank code has been run into the
        // number.
        TextField("", text: Binding(
            get: { text.wrappedValue },
            set: { raw in
                let held = Boxes(prefix: prefix, number: number, bankCode: bankCode)
                let (edited, rebuilds) = Self.boxes(afterEditing: focus, to: raw, from: held)
                guard rebuilds else {
                    text.wrappedValue = raw
                    return
                }
                if edited.prefix != prefix { prefix = edited.prefix }
                if edited.number != number { number = edited.number }
                if edited.bankCode != bankCode { bankCode = edited.bankCode }
                // The rebuild ends the edit, so the keyboard goes with it: say so, rather than leave
                // the focus state to catch up with a field that no longer exists.
                pasteRevision += 1
                focusedSegment = nil
            }
        ))
        .id(pasteRevision)
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
            let digits = Self.clampSegment(newValue, maxLength: maxLength)
            if digits != newValue { text.wrappedValue = digits }
        }
    }

    /// What a segment keeps of an edit that is not an account: ASCII digits, up to `maxLength`. Not
    /// `isNumber`, which also takes the Arabic-Indic and full-width digits a keyboard can type and the
    /// server refuses; the paste splitter is ASCII-only already. Android's `clampSegment`.
    static func clampSegment(_ raw: String, maxLength: Int) -> String {
        String(raw.filter { $0.isASCII && $0.isNumber }.prefix(maxLength))
    }

    /// What an edit of one box leaves in the control.
    enum Edit: Equatable {
        /// The box holds this text, which the digit clamp then takes.
        case box(String)
        /// The number holds this run, and the box keeps its text.
        case number(String)
        /// A written-out account or an IBAN across the three boxes; a nil part is left as it is.
        case account(prefix: String?, number: String, bankCode: String?)
    }

    /// The text of the three boxes.
    struct Boxes: Equatable {
        var prefix: String
        var number: String
        var bankCode: String

        subscript(box: Segment) -> String {
            get {
                switch box {
                case .prefix: prefix
                case .number: number
                case .bankCode: bankCode
                }
            }
            set {
                switch box {
                case .prefix: prefix = newValue
                case .number: number = newValue
                case .bankCode: bankCode = newValue
                }
            }
        }
    }

    /// What the boxes hold once `box` is edited to `raw`, and whether the control writes them itself. An edit the
    /// digit clamp alone turns into what the box keeps goes through as typed, and the keyboard stays; any other is
    /// written by the control, which rebuilds the boxes and so ends the edit.
    static func boxes(afterEditing box: Segment, to raw: String, from held: Boxes) -> (boxes: Boxes, rebuilds: Bool) {
        let boxLength = maxLength(of: box)
        let typed = clampSegment(raw, maxLength: boxLength)
        var edited = held
        switch splitPastedAccount(raw, replacing: held[box], into: box) {
        case let .box(kept) where clampSegment(kept, maxLength: boxLength) == typed:
            edited[box] = typed
            return (edited, false)
        case let .box(kept):
            edited[box] = kept
        case let .number(run):
            edited.number = run
        case let .account(prefix, number, bankCode):
            edited.prefix = prefix ?? held.prefix
            edited.number = number
            edited.bankCode = bankCode ?? held.bankCode
        }
        return (edited, true)
    }

    /// What an edit of `box` does; `raw` is the box's new text and `current` the text it replaces. Android's
    /// `splitPastedAccount` and the web control's paste handler follow the same rule, case for case.
    ///
    /// New text that fits the box is ordinary editing, kept as typed or pasted. New text too long for the box
    /// is read by what the edit put in, the new text less the start and the end it shares with `current`:
    /// - one character is typing into a full box, which keeps its text;
    /// - a written-out account (`prefix-number/code`, `number/code`, `prefix-number`) or a CZ or SK IBAN, with
    ///   every kind of whitespace dropped and an en or em dash read as a hyphen, is split across the three
    ///   boxes; one without a prefix clears the old one, and one without a bank code keeps it;
    /// - a run of digits that fits the box becomes its content;
    /// - a run of digits too long for the box but not for the number becomes the number, the box keeping its
    ///   text;
    /// - anything else leaves the box as it was.
    ///
    /// Nothing is validated beyond shape: the server owns mod-11, the bank code and the IBAN cross-check.
    /// -> /partner-app/onboarding
    static func splitPastedAccount(_ raw: String, replacing current: String = "", into box: Segment) -> Edit {
        let boxLength = maxLength(of: box)
        guard raw.count > boxLength else { return .box(raw) }
        let run = insertedRun(raw, replacing: current)
        guard run.count > 1 else { return .box(current) }
        let pasted = String(run.filter { !$0.isWhitespace }.map { $0 == "\u{2013}" || $0 == "\u{2014}" ? "-" : $0 })
        if let account = account(in: domesticForm(ofIban: pasted) ?? pasted) { return account }
        guard isDigits(pasted, upTo: numberMaxLength) else { return .box(current) }
        return pasted.count <= boxLength ? .box(pasted) : .number(pasted)
    }

    /// `[prefix-]number/code` or `prefix-number`: the number pad types neither separator, so their presence is
    /// what marks a written-out account. Nil for any other text.
    private static func account(in text: String) -> Edit? {
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
        return .account(prefix: prefix, number: number, bankCode: bankCode)
    }

    private static func maxLength(of box: Segment) -> Int {
        switch box {
        case .prefix: prefixMaxLength
        case .number: numberMaxLength
        case .bankCode: bankCodeMaxLength
        }
    }

    /// `text` less the start and the end it shares with `current`, the two never overlapping: what the edit
    /// put in.
    private static func insertedRun(_ text: String, replacing current: String) -> String {
        let new = Array(text)
        let old = Array(current)
        let start = zip(new, old).prefix { $0 == $1 }.count
        let end = min(
            zip(new.reversed(), old.reversed()).prefix { $0 == $1 }.count,
            min(new.count, old.count) - start
        )
        return String(new[start ..< new.count - end])
    }

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
