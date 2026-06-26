import SwiftUI

public struct PasswordRule: Identifiable {
    public let label: String
    public let isSatisfied: Bool
    public var id: String {
        label
    }

    public init(label: String, isSatisfied: Bool) {
        self.label = label
        self.isSatisfied = isSatisfied
    }
}

public struct PasswordRuleList: View {
    private let rules: [PasswordRule]
    private let hasInput: Bool

    public init(rules: [PasswordRule], hasInput: Bool) {
        self.rules = rules
        self.hasInput = hasInput
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            ForEach(rules) { rule in
                PasswordRuleRow(rule: rule, hasInput: hasInput)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, Spacing.xxs)
        .padding(.vertical, Spacing.xxs)
    }
}

private struct PasswordRuleRow: View {
    let rule: PasswordRule
    let hasInput: Bool

    private var icon: String {
        if rule.isSatisfied { return "checkmark.circle.fill" }
        return hasInput ? "xmark.circle.fill" : "circle"
    }

    private var tint: Color {
        if rule.isSatisfied { return CleansiaColors.successText }
        return hasInput ? CleansiaColors.error : CleansiaColors.onSurfaceVariant
    }

    private var textColor: Color {
        rule.isSatisfied || !hasInput ? CleansiaColors.onSurfaceVariant : CleansiaColors.error
    }

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: icon)
                .font(.system(size: 14))
                .foregroundColor(tint)
            Text(rule.label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(textColor)
        }
    }
}

#if DEBUG
    struct PasswordRuleList_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                list(satisfied: false, hasInput: false)
                    .previewDisplayName("Untouched")
                list(satisfied: false, hasInput: true)
                    .previewDisplayName("Input — failing")
                list(satisfied: true, hasInput: true)
                    .previewDisplayName("Satisfied")
            }
            .padding()
        }

        private static func list(satisfied: Bool, hasInput: Bool) -> some View {
            PasswordRuleList(
                rules: [
                    PasswordRule(label: "At least 8 characters", isSatisfied: satisfied),
                    PasswordRule(label: "Contains a letter", isSatisfied: satisfied),
                    PasswordRule(label: "Contains a number", isSatisfied: satisfied)
                ],
                hasInput: hasInput
            )
        }
    }
#endif
