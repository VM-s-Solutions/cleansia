import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct SubmitReviewSheet: View {
    let existingReview: CustomerOrderReview?
    let isSubmitting: Bool
    let errorMessage: String?
    let onConfirm: (Int, String?, [CustomerReviewTag]) -> Void
    let onDismiss: () -> Void
    /// The prompt says "Not now"; the detail screen's own entry point says "Cancel".
    var dismissLabel: String = L10n.OrderReview.cancel
    /// Non-nil on a prompt the customer did not ask for, which leads with the question rather than the
    /// editorial title. Android makes the same split.
    var titleOverride: String?

    @State private var rating: Int
    @State private var comment: String
    @State private var selectedTags: Set<CustomerReviewTag>
    /// The rating the current selection was made under — `onChange` gives the new value only, and the
    /// decision needs both sides to know whether the polarity actually flipped.
    @State private var previousRating: Int

    /// 1000, matching `SubmitOrderReview.Validator` and `OrderReview.Comment`'s `[MaxLength]`.
    ///
    /// Counted in UTF-16, as the server counts it — see `String.cappedToUtf16`. A `count` cap would let
    /// 600 emoji through the field and have them refused at submit.
    private let maxCommentUtf16Length = 1000

    init(
        existingReview: CustomerOrderReview?,
        isSubmitting: Bool,
        errorMessage: String?,
        onConfirm: @escaping (Int, String?, [CustomerReviewTag]) -> Void,
        onDismiss: @escaping () -> Void,
        dismissLabel: String = L10n.OrderReview.cancel,
        titleOverride: String? = nil
    ) {
        self.existingReview = existingReview
        self.isSubmitting = isSubmitting
        self.errorMessage = errorMessage
        self.onConfirm = onConfirm
        self.onDismiss = onDismiss
        self.dismissLabel = dismissLabel
        self.titleOverride = titleOverride
        _rating = State(initialValue: existingReview?.rating ?? 0)
        _comment = State(initialValue: existingReview?.comment ?? "")
        _selectedTags = State(initialValue: Set(existingReview?.tags ?? []))
        _previousRating = State(initialValue: existingReview?.rating ?? 0)
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
                Text(titleOverride ?? (isEdit ? L10n.OrderReview.editTitle : L10n.OrderReview.sheetTitle))
                    .cleansiaFont(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onSurface)

                StarPicker(rating: $rating, enabled: !isSubmitting)
                    .onChange(of: rating) { next in
                        clearTagsOnPolarityChange(from: previousRating, to: next)
                        previousRating = next
                    }

                Text(L10n.OrderReview.ratingDescription(rating))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity, alignment: .center)

                tagSection

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
                            let capped = value.cappedToUtf16(maxCommentUtf16Length)
                            if capped != value { comment = capped }
                        }
                }

                if let errorMessage, !errorMessage.isBlank {
                    Text(errorMessage)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.error)
                }

                CleansiaOutlinedButton(dismissLabel, enabled: !isSubmitting, action: onDismiss)

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

    /// Chips appear only once a rating exists, because the set they offer IS a function of it: 1-3 asks
    /// what went wrong, 4-5 what went well. Laid out with `ChipFlow` rather than a fixed grid — these
    /// labels are localized into five languages and a Czech or Ukrainian one routinely runs half again
    /// the width of its English original.
    @ViewBuilder
    private var tagSection: some View {
        let offered = CustomerReviewTag.forRating(rating)
        if !offered.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(
                    rating >= CustomerReviewTag.positiveRatingFloor
                        ? L10n.OrderReview.tagsPositivePrompt
                        : L10n.OrderReview.tagsNegativePrompt
                )
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity, alignment: .center)

                ChipFlow(spacing: Spacing.xs) {
                    ForEach(offered, id: \.rawValue) { tag in
                        let isSelected = selectedTags.contains(tag)
                        CleansiaChip(
                            label: tag.label,
                            isSelected: isSelected,
                            // At the cap the unselected chips go inert rather than disappearing: a row
                            // that reflows as you tap moves the targets under the customer's finger.
                            enabled: !isSubmitting
                                && (isSelected || selectedTags.count < CustomerReviewTag.maxTags)
                        ) {
                            toggle(tag)
                        }
                    }
                }
            }
        }
    }

    /// The offered set flips polarity at `positiveRatingFloor`, so tags picked under the old rating are
    /// no longer offerable — and the server REFUSES a mismatched tag rather than dropping it
    /// (`order.review.tag_rating_mismatch`). Left in place they also hold the cap, which is what made
    /// every chip go inert after a rating change: four positive tags still counted, so the newly
    /// offered negative ones were all disabled and none of them was selected to explain why.
    private func clearTagsOnPolarityChange(from previous: Int, to next: Int) {
        let wasPositive = previous >= CustomerReviewTag.positiveRatingFloor
        let isPositive = next >= CustomerReviewTag.positiveRatingFloor
        if wasPositive != isPositive { selectedTags.removeAll() }
    }

    private func toggle(_ tag: CustomerReviewTag) {
        if selectedTags.contains(tag) {
            selectedTags.remove(tag)
        } else if selectedTags.count < CustomerReviewTag.maxTags {
            selectedTags.insert(tag)
        }
    }

    private func submit() {
        guard canSubmit else { return }
        let trimmed = comment.trimmingCharacters(in: .whitespacesAndNewlines)
        // Ordered by wire value so two identical selections submit identically, whatever order the
        // chips happened to be tapped in.
        let tags = selectedTags.sorted { $0.rawValue < $1.rawValue }
        onConfirm(rating, trimmed.isEmpty ? nil : trimmed, tags)
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
