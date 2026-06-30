import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct SubmitReviewSheet: View {
    let existingReview: OrderReviewDto?
    let isSubmitting: Bool
    let errorMessage: String?
    let onConfirm: (Int, String?) -> Void
    let onDismiss: () -> Void

    @State private var rating: Int
    @State private var comment: String

    private let maxCommentLength = 2000

    init(
        existingReview: OrderReviewDto?,
        isSubmitting: Bool,
        errorMessage: String?,
        onConfirm: @escaping (Int, String?) -> Void,
        onDismiss: @escaping () -> Void
    ) {
        self.existingReview = existingReview
        self.isSubmitting = isSubmitting
        self.errorMessage = errorMessage
        self.onConfirm = onConfirm
        self.onDismiss = onDismiss
        _rating = State(initialValue: existingReview?.rating ?? 0)
        _comment = State(initialValue: existingReview?.comment ?? "")
    }

    private var isEdit: Bool {
        existingReview != nil
    }

    private var canSubmit: Bool {
        (1 ... 5).contains(rating) && !isSubmitting
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                Text(isEdit ? L10n.OrderReview.editTitle : L10n.OrderReview.sheetTitle)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onSurface)

                StarPicker(rating: $rating, enabled: !isSubmitting)

                Text(L10n.OrderReview.ratingDescription(rating))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity, alignment: .center)

                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    Text(L10n.OrderReview.commentLabel)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    TextEditor(text: $comment)
                        .frame(minHeight: 88)
                        .scrollContentBackground(.hidden)
                        .padding(Spacing.xs)
                        .background(CleansiaColors.surface)
                        .overlay(
                            RoundedRectangle(cornerRadius: CornerRadius.medium)
                                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
                        )
                        .disabled(isSubmitting)
                        .onChange(of: comment) { value in
                            if value.count > maxCommentLength { comment = String(value.prefix(maxCommentLength)) }
                        }
                }

                if let errorMessage, !errorMessage.isBlank {
                    Text(errorMessage)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.error)
                }

                CleansiaOutlinedButton(L10n.OrderReview.cancel, enabled: !isSubmitting, action: onDismiss)

                CleansiaPrimaryButton(
                    isEdit ? L10n.OrderReview.save : L10n.OrderReview.submit,
                    loading: isSubmitting,
                    enabled: canSubmit,
                    action: submit
                )
            }
            .padding(Spacing.l)
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(isSubmitting)
    }

    private func submit() {
        guard canSubmit else { return }
        let trimmed = comment.trimmingCharacters(in: .whitespacesAndNewlines)
        onConfirm(rating, trimmed.isEmpty ? nil : trimmed)
    }
}

private struct StarPicker: View {
    @Binding var rating: Int
    let enabled: Bool

    var body: some View {
        HStack(spacing: Spacing.xs) {
            ForEach(1 ... 5, id: \.self) { star in
                Button {
                    rating = star
                } label: {
                    Image(systemName: rating >= star ? "star.fill" : "star")
                        .font(.system(size: 32))
                        .foregroundColor(rating >= star ? CleansiaColors.warningStar : CleansiaColors.outlineVariant)
                }
                .buttonStyle(.plain)
                .disabled(!enabled)
                .accessibilityLabel(Text(L10n.OrderReview.starContentDesc(star)))
            }
        }
        .frame(maxWidth: .infinity, alignment: .center)
    }
}
