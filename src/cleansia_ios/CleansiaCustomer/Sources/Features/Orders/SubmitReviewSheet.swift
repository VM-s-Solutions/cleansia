import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct SubmitReviewSheet: View {
    let existingReview: CustomerOrderReview?
    let isSubmitting: Bool
    let errorMessage: String?
    let onConfirm: (Int, String?, [CustomerReviewTag], [OrderItemLineScore]) -> Void
    let onDismiss: () -> Void
    /// The items on the order, so the customer can score them one by one. Empty for any order
    /// whose items did not load — the section then renders nothing, which is right: the overall
    /// rating above is the required answer and this is an extra.
    var lineOptions: [OrderItemLine] = []
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
        onConfirm: @escaping (Int, String?, [CustomerReviewTag], [OrderItemLineScore]) -> Void,
        onDismiss: @escaping () -> Void,
        dismissLabel: String = L10n.OrderReview.cancel,
        titleOverride: String? = nil,
        lineOptions: [OrderItemLine] = []
    ) {
        self.existingReview = existingReview
        self.isSubmitting = isSubmitting
        self.errorMessage = errorMessage
        self.onConfirm = onConfirm
        self.onDismiss = onDismiss
        self.dismissLabel = dismissLabel
        self.titleOverride = titleOverride
        self.lineOptions = lineOptions
        _rating = State(initialValue: existingReview?.rating ?? 0)
        _comment = State(initialValue: existingReview?.comment ?? "")
        _selectedTags = State(initialValue: Set(existingReview?.tags ?? []))
        _previousRating = State(initialValue: existingReview?.rating ?? 0)
        // Seeded from the stored review on an edit, so reopening shows what they said last time.
        // Absent means NOT SCORED, which is a different answer from scored badly — hence a dictionary
        // rather than an array of zeroes.
        _lineScores = State(initialValue: (existingReview?.lines ?? []).reduce(into: [:]) { acc, line in
            acc["\(line.packageId ?? "")|\(line.serviceId)"] = line.rating
        })
    }

    @State private var lineScores: [String: Int]
    @State private var linesExpanded = false

    private var isEdit: Bool {
        existingReview != nil
    }

    private var canSubmit: Bool {
        (1 ... 5).contains(rating) && !isSubmitting
    }

    var body: some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(spacing: Spacing.l) {
                    ratingHeader
                    optionalFeedback
                }
                .padding(Spacing.l)
                .readableWidth()
            }
            .scrollDismissesKeyboard(.interactively)

            actions
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(isSubmitting)
    }

    private var ratingHeader: some View {
        VStack(spacing: Spacing.m) {
            Text(titleOverride ?? (isEdit ? L10n.OrderReview.editTitle : L10n.OrderReview.sheetTitle))
                .cleansiaFont(CleansiaTypography.headlineLarge)
                .foregroundColor(CleansiaColors.primaryText)
                .multilineTextAlignment(.center)
                .accessibilityAddTraits(.isHeader)

            StarPicker(rating: $rating, enabled: !isSubmitting)
                .onChange(of: rating) { next in
                    clearTagsOnPolarityChange(from: previousRating, to: next)
                    previousRating = next
                }

            Text(L10n.OrderReview.ratingDescription(rating))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.s)
    }

    private var optionalFeedback: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            Text(L10n.OrderReview.optionalDetails)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .accessibilityAddTraits(.isHeader)

            tagSection
            commentField
            itemRatings
        }
        .padding(Spacing.m)
        .background(CleansiaColors.background)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }

    private var commentField: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.OrderReview.commentLabel)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            ZStack(alignment: .topLeading) {
                TextEditor(text: $comment)
                    .font(CleansiaTypography.bodyMedium)
                    .frame(minHeight: 100)
                    .scrollContentBackground(.hidden)
                    .padding(Spacing.xs)
                    .accessibilityLabel(L10n.OrderReview.commentLabel)
                if comment.isEmpty {
                    Text(L10n.OrderReview.commentPlaceholder)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .padding(Spacing.m)
                        .allowsHitTesting(false)
                        .accessibilityHidden(true)
                }
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
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
    }

    @ViewBuilder
    private var itemRatings: some View {
        if !lineOptions.isEmpty {
            Divider()
            Button {
                linesExpanded.toggle()
            } label: {
                HStack(spacing: Spacing.xs) {
                    Text(L10n.OrderReview.rateItems)
                        .font(CleansiaTypography.labelLarge)
                        .lineLimit(1)
                    Spacer(minLength: 0)
                    Image(systemName: linesExpanded ? "chevron.up" : "chevron.down")
                }
                .foregroundColor(CleansiaColors.primaryText)
                .frame(minHeight: 44)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .disabled(isSubmitting)

            if linesExpanded {
                Text(L10n.OrderReview.rateItemsHint)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ForEach(lineOptions) { option in
                    itemRating(option)
                }
            }
        }
    }

    private func itemRating(_ option: OrderItemLine) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(option.label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
            if let packageLabel = option.packageLabel, !packageLabel.isEmpty {
                Text(L10n.OrderReview.itemInPackage(packageLabel))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            HStack(spacing: 0) {
                ForEach(1 ... 5, id: \.self) { star in
                    Button {
                        lineScores[option.id] = lineScores[option.id] == star ? nil : star
                    } label: {
                        Image(systemName: star <= (lineScores[option.id] ?? 0) ? "star.fill" : "star")
                            .font(.system(size: 24))
                            .foregroundColor(CleansiaColors.warningStar)
                            .frame(minWidth: 44, minHeight: 44)
                            .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .disabled(isSubmitting)
                    .accessibilityLabel(L10n.OrderReview.rateItemStar(option.label, star))
                    .accessibilityAddTraits(lineScores[option.id] == star ? .isSelected : [])
                }
            }
        }
    }

    private var actions: some View {
        VStack(spacing: Spacing.xs) {
            if let errorMessage, !errorMessage.isBlank {
                Text(errorMessage)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.error)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            CleansiaPrimaryButton(
                isEdit ? L10n.OrderReview.save : L10n.OrderReview.submit,
                loading: isSubmitting,
                enabled: canSubmit,
                action: submit
            )
            CleansiaTextLink(dismissLabel, action: onDismiss)
                .disabled(isSubmitting)
        }
        .padding(.horizontal, Spacing.l)
        .padding(.top, Spacing.m)
        .padding(.bottom, Spacing.xs)
        .readableWidth()
        .background(CleansiaColors.surface)
        .overlay(alignment: .top) {
            Divider()
        }
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
        // In list order, so two identical reviews submit identically whatever order the stars were
        // tapped in — the same reasoning as the tag sort above.
        let scores = lineOptions.compactMap { option -> OrderItemLineScore? in
            guard let rating = lineScores[option.id] else { return nil }
            return OrderItemLineScore(
                serviceId: option.serviceId,
                packageId: option.packageId,
                rating: rating
            )
        }
        onConfirm(rating, trimmed.isEmpty ? nil : trimmed, tags, scores)
    }
}

private struct StarPicker: View {
    @Binding var rating: Int
    let enabled: Bool

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            ForEach(1 ... 5, id: \.self) { star in
                Button {
                    rating = star
                } label: {
                    Image(systemName: rating >= star ? "star.fill" : "star")
                        .font(.system(size: 36))
                        .foregroundColor(rating >= star ? CleansiaColors.warningStar : CleansiaColors.onSurfaceVariant)
                        .frame(minWidth: 44, minHeight: 52)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .disabled(!enabled)
                .accessibilityLabel(Text(L10n.OrderReview.starContentDesc(star)))
                .accessibilityAddTraits(rating == star ? .isSelected : [])
            }
        }
        .frame(maxWidth: .infinity, alignment: .center)
    }
}
