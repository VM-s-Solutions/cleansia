import CleansiaCore
import SwiftUI

struct CodeSheetShell<Message: View>: View {
    let title: String
    @Binding var code: String
    let border: Color
    let isError: Bool
    let isSubmitting: Bool
    let isValid: Bool
    let applyTitle: String
    let cancelTitle: String
    let doneTitle: String
    let onEdit: () -> Void
    let onApply: () -> Void
    let onDone: () -> Void
    let onCancel: () -> Void
    @ViewBuilder let message: () -> Message

    @State private var contentHeight: CGFloat = 320

    private var canApply: Bool {
        !code.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !isSubmitting
    }

    var body: some View {
        content
            .frame(maxWidth: .infinity)
            .fixedSize(horizontal: false, vertical: true)
            .background(GeometryReader { proxy in
                Color.clear.preference(key: CodeSheetHeightKey.self, value: proxy.size.height)
            })
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
            .background(CleansiaColors.surface.ignoresSafeArea())
            .onPreferenceChange(CodeSheetHeightKey.self) { contentHeight = $0 }
            .presentationDetents([.height(contentHeight)])
    }

    private var content: some View {
        VStack(spacing: Spacing.m) {
            Text(title)
                .font(CleansiaTypography.titleLarge)
                .fontWeight(.bold)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity)

            TextField(title, text: Binding(
                get: { code },
                set: { next in
                    onEdit()
                    code = next.uppercased()
                }
            ))
            .textInputAutocapitalization(.characters)
            .autocorrectionDisabled()
            .disabled(isSubmitting || isValid)
            .font(CleansiaTypography.bodyLarge)
            .padding(Spacing.m)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(border, lineWidth: isError || isValid ? 2 : 1)
            )

            message()
                .frame(maxWidth: .infinity, alignment: .leading)

            if isValid {
                CleansiaPrimaryButton(doneTitle, action: onDone)
            } else {
                CleansiaOutlinedButton(cancelTitle, enabled: !isSubmitting, action: onCancel)
                CleansiaPrimaryButton(applyTitle, loading: isSubmitting, enabled: canApply, action: onApply)
            }
        }
        .padding(Spacing.l)
    }
}

private struct CodeSheetHeightKey: PreferenceKey {
    static var defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) {
        value = max(value, nextValue())
    }
}

enum CodeSheetMessage {
    static func helper(_ text: String) -> some View {
        Text(text)
            .font(CleansiaTypography.labelMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
    }

    static func validating(_ text: String) -> some View {
        HStack(spacing: Spacing.xs) {
            ProgressView().scaleEffect(0.8)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    static func success(_ text: String) -> some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "checkmark.circle.fill")
                .foregroundColor(CleansiaColors.primary)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .fontWeight(.semibold)
                .foregroundColor(CleansiaColors.primary)
        }
    }

    static func error(_ text: String) -> some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "xmark.circle.fill")
                .foregroundColor(CleansiaColors.error)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.error)
        }
    }
}
