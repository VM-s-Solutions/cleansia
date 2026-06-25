import SwiftUI

public struct CleansiaPhoneInput: View {
    @Binding private var value: String
    private let label: String
    private let helper: String?
    private let errorText: String?
    private let enabled: Bool
    private let transparentContainer: Bool

    @FocusState private var focused: Bool

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

    private var isError: Bool { errorText != nil }
    private var floating: Bool { focused || !value.isEmpty }

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
                    .animation(.easeOut(duration: 0.15), value: floating)

                TextField("", text: Binding(
                    get: { PhoneNumberFormatter.display(value) },
                    set: { value = PhoneNumberSanitizer.sanitize($0) }
                ))
                .keyboardType(.phonePad)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .tint(CleansiaColors.primary)
                .focused($focused)
                .disabled(!enabled)
                .offset(y: floating ? 8 : 0)
            }
            .padding(.horizontal, Spacing.m)
            .frame(minHeight: 56)
            .background(transparentContainer ? .clear : CleansiaColors.surface)
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
}

enum PhoneNumberFormatter {
    static func display(_ raw: String) -> String {
        guard !raw.isEmpty else { return "" }
        let hasPlus = raw.hasPrefix("+")
        let digits = Array(raw.filter(\.isNumber))
        guard !digits.isEmpty else { return hasPlus ? "+" : "" }
        var groups: [String] = []
        var index = 0
        if hasPlus {
            let countryLength = min(3, digits.count)
            groups.append("+" + String(digits[0 ..< countryLength]))
            index = countryLength
        }
        while index < digits.count {
            let end = min(index + 3, digits.count)
            groups.append(String(digits[index ..< end]))
            index = end
        }
        return groups.joined(separator: " ")
    }
}

#if DEBUG
struct CleansiaPhoneInput_Previews: PreviewProvider {
    static var previews: some View {
        StatefulPreviewWrapper("+420728089247") { binding in
            CleansiaPhoneInput(value: binding, label: "Phone")
                .padding()
        }
    }
}
#endif
