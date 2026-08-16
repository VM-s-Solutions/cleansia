import CleansiaCore
import SwiftUI

struct OrderCardSurface<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        // Vertical only. The scroll view already insets everything by Spacing.ml, the same as the
        // pinned header, so a further 16pt here started every section's text 16pt to the right of the
        // order number it belongs under. With the card flat there is no container edge for an inset to
        // hold away from anyway.
        .padding(.vertical, Spacing.m)
        // Flat, exactly like the partner sheet's `OrderSectionCard`: a surface-coloured block with no
        // border and no shadow. The 1pt outline this used to carry drew a hard edge around every
        // section, so a sheet of six cards read as six boxes stacked on a page rather than one panel.
        // The rule under each section title is what separates them now.
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
    }
}

struct OrderSectionHeaderRow: View {
    let title: String
    var systemImage: String?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(spacing: Spacing.xs) {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                Text(title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onBackground)
            }
            // The rule that separates a section from its content now that the cards carry no border —
            // the partner sheet's `OrderSectionCard` arrangement.
            Divider().overlay(CleansiaColors.outlineVariant)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct OrderInfoRow: View {
    let label: String
    let value: String

    var body: some View {
        HStack {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(value)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }
}

struct OrderStatusPill: View {
    let label: String
    let color: Color

    var body: some View {
        Text(label)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(color)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, Spacing.xxs)
            .background(color.opacity(0.14), in: Capsule())
    }
}
