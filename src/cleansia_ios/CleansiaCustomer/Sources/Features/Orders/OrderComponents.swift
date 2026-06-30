import CleansiaCore
import SwiftUI

struct OrderCardSurface<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

struct OrderSectionHeaderRow: View {
    let title: String
    var systemImage: String?

    var body: some View {
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
