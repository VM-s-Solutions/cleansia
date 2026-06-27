import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

extension View {
    /// Plain inset-free list row (no separators, transparent background) so the
    /// `List` reads as a stack of cards.
    func ordersRow() -> some View {
        listRowInsets(EdgeInsets(top: Spacing.xxs, leading: Spacing.m, bottom: Spacing.xxs, trailing: Spacing.m))
            .listRowSeparator(.hidden)
            .listRowBackground(Color.clear)
    }

    /// The bordered card surface used by Available/History rows.
    func ordersCard() -> some View {
        padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
    }
}

struct InProgressBanner: View {
    let order: OrderListItem
    let onClick: () -> Void

    var body: some View {
        Button(action: onClick) {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "play.circle.fill")
                    .foregroundColor(CleansiaColors.onPrimary)
                VStack(alignment: .leading, spacing: 0) {
                    Text(L10n.Orders.inProgressNow)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onPrimary.opacity(0.85))
                    Text(OrdersFormat.bannerTitle(order))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onPrimary)
                        .lineLimit(1)
                }
                Spacer()
                Image(systemName: "arrow.right").foregroundColor(CleansiaColors.onPrimary)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
            .background(CleansiaColors.primary, in: RoundedRectangle(cornerRadius: CornerRadius.small))
        }
        .buttonStyle(.plain)
    }
}

struct OrdersSearchField: View {
    @Binding var text: String

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "magnifyingglass")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            TextField(L10n.Orders.searchHint, text: $text)
                .font(CleansiaTypography.bodyMedium)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.small))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.small)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .ordersRow()
    }
}

struct ScopeChips: View {
    let order: OrderListItem

    var body: some View {
        let rooms = order.rooms ?? 0
        let baths = order.bathrooms ?? 0
        let extras = order.extras?.values.filter { $0 }.count ?? 0
        if rooms > 0 || baths > 0 || extras > 0 {
            HStack(spacing: Spacing.xxs) {
                if rooms > 0 { ScopeChip(text: OrdersFormat.rooms(rooms)) }
                if baths > 0 { ScopeChip(text: OrdersFormat.baths(baths)) }
                if extras > 0 { ScopeChip(text: OrdersFormat.extras(extras)) }
            }
        }
    }
}

private struct ScopeChip: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.labelMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, 2)
            .background(CleansiaColors.surfaceVariant, in: Capsule())
    }
}

struct DecisionBadge: View {
    let icon: String
    let label: String
    let tint: Color

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: icon).font(.system(size: 11))
            Text(label).font(CleansiaTypography.labelMedium)
        }
        .foregroundColor(tint)
        .padding(.horizontal, Spacing.xs)
        .padding(.vertical, 2)
        .background(tint.opacity(0.12), in: Capsule())
    }
}

struct CompactOrderRow: View {
    let order: OrderListItem
    let onOpen: () -> Void

    var body: some View {
        Button(action: onOpen) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(OrdersFormat.timeOnly(order.cleaningDateTime))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(OrdersFormat.compactSubtitle(order))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                }
                Spacer()
                Text(OrdersFormat.pay(order))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.primary)
                Image(systemName: "chevron.right")
                    .font(.system(size: 12))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .ordersCard()
        }
        .buttonStyle(.plain)
    }
}

struct OrdersEmptyState: View {
    let mascot: Mascot
    let text: String

    var body: some View {
        MascotEmptyState(image: mascot.image, text: text, verticallyCentered: true)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
