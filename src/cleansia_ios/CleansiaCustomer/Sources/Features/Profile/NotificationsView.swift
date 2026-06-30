import CleansiaCore
import SwiftUI

struct NotificationsView: View {
    @StateObject private var vm: NotificationPreferencesViewModel

    init(client: NotificationPreferencesClient) {
        _vm = StateObject(wrappedValue: NotificationPreferencesViewModel(client: client))
    }

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            content
        }
        .navigationTitle(L10n.Notifications.title)
        .navigationBarTitleDisplayMode(.inline)
        .task { await vm.load() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            VStack(spacing: Spacing.m) {
                Text(L10n.Notifications.errorMessage)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                CleansiaOutlinedButton(L10n.retry, size: .medium) { Task { await vm.load() } }
                    .fixedSize()
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .padding(Spacing.xl)
        case let .loaded(preferences):
            ScrollView {
                VStack(spacing: Spacing.l) {
                    group(L10n.Notifications.sectionOrders, rows: orderRows, preferences: preferences)
                    group(L10n.Notifications.sectionMembership, rows: membershipRows, preferences: preferences)
                    group(L10n.Notifications.sectionAccount, rows: accountRows, preferences: preferences)
                }
                .padding(Spacing.m)
            }
        }
    }

    private var orderRows: [NotificationRowItem] {
        [
            NotificationRowItem(.orderUpdates, L10n.Notifications.orderUpdates, L10n.Notifications.orderUpdatesDesc),
            NotificationRowItem(
                .cleanerOnTheWay,
                L10n.Notifications.cleanerOnTheWay,
                L10n.Notifications.cleanerOnTheWayDesc
            ),
            NotificationRowItem(
                .orderCompleted,
                L10n.Notifications.orderCompleted,
                L10n.Notifications.orderCompletedDesc
            ),
            NotificationRowItem(
                .orderCancelled,
                L10n.Notifications.orderCancelled,
                L10n.Notifications.orderCancelledDesc
            ),
            NotificationRowItem(
                .recurringScheduled,
                L10n.Notifications.recurringScheduled,
                L10n.Notifications.recurringScheduledDesc
            )
        ]
    }

    private var membershipRows: [NotificationRowItem] {
        [
            NotificationRowItem(
                .membershipExpiring,
                L10n.Notifications.membershipExpiring,
                L10n.Notifications.membershipExpiringDesc
            ),
            NotificationRowItem(
                .membershipCancelled,
                L10n.Notifications.membershipCancelled,
                L10n.Notifications.membershipCancelledDesc
            ),
            NotificationRowItem(.tierUpgrade, L10n.Notifications.tierUpgrade, L10n.Notifications.tierUpgradeDesc),
            NotificationRowItem(.promo, L10n.Notifications.promo, L10n.Notifications.promoDesc)
        ]
    }

    private var accountRows: [NotificationRowItem] {
        [
            NotificationRowItem(.refundIssued, L10n.Notifications.refundIssued, L10n.Notifications.refundIssuedDesc),
            NotificationRowItem(.disputeReply, L10n.Notifications.disputeReply, L10n.Notifications.disputeReplyDesc)
        ]
    }

    private func group(
        _ title: String,
        rows: [NotificationRowItem],
        preferences: NotificationPreferences
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(title.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .padding(.horizontal, Spacing.xs)
            VStack(spacing: 0) {
                ForEach(rows.indices, id: \.self) { index in
                    NotificationToggleRow(
                        item: rows[index],
                        isOn: preferences.isEnabled(rows[index].category),
                        onChange: { vm.setCategory(rows[index].category, enabled: $0) }
                    )
                    if index < rows.count - 1 {
                        Divider().padding(.leading, Spacing.m)
                    }
                }
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
    }
}

struct NotificationRowItem {
    let category: NotificationCategory
    let label: String
    let description: String

    init(_ category: NotificationCategory, _ label: String, _ description: String) {
        self.category = category
        self.label = label
        self.description = description
    }
}

private struct NotificationToggleRow: View {
    let item: NotificationRowItem
    let isOn: Bool
    let onChange: (Bool) -> Void

    var body: some View {
        Toggle(isOn: Binding(get: { isOn }, set: onChange)) {
            VStack(alignment: .leading, spacing: 2) {
                Text(item.label)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(item.description)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .tint(CleansiaColors.primary)
        .padding(Spacing.m)
    }
}
