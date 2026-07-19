import CleansiaCore
import SwiftUI

/// The notifications inbox behind the Home bell — the T-0393 server-backed
/// feed. Newest-first pages of 20 with load-more; opening fires the
/// watermarked mark-all; the interim mascot empty state is the zero-rows
/// state. Tapping a row that resolves a destination dismisses the sheet and
/// hands it to the shell; a target-less row just marks read and stays put.
struct NotificationsInboxSheet: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var vm: NotificationsInboxViewModel
    private let onDestination: (CustomerNotificationDestination) -> Void

    init(
        client: NotificationFeedClient,
        badge: NotificationBadgeModel,
        snackbar: SnackbarController,
        onDestination: @escaping (CustomerNotificationDestination) -> Void
    ) {
        _vm = StateObject(wrappedValue: NotificationsInboxViewModel(
            client: client,
            badge: badge,
            snackbar: snackbar
        ))
        self.onDestination = onDestination
    }

    var body: some View {
        NavigationStack {
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                content
            }
            .navigationTitle(L10n.NotificationsInbox.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button(L10n.NotificationsInbox.close) { dismiss() }
                        .tint(CleansiaColors.primary)
                }
            }
        }
        .task { await vm.onOpen() }
        .onReceive(vm.tapped) { destination in
            dismiss()
            onDestination(destination)
        }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            NotificationsInboxErrorView { Task { await vm.retry() } }
        case let .loaded(items):
            let rows = NotificationFeedTemplates.rows(from: items)
            if rows.isEmpty {
                NotificationsInboxEmptyView()
            } else {
                NotificationsInboxList(vm: vm, rows: rows)
            }
        }
    }
}

private struct NotificationsInboxList: View {
    @ObservedObject var vm: NotificationsInboxViewModel
    let rows: [NotificationFeedRow]

    var body: some View {
        ScrollView {
            LazyVStack(spacing: Spacing.s) {
                ForEach(rows) { row in
                    Button {
                        Task { await vm.tap(id: row.id) }
                    } label: {
                        NotificationFeedRowCard(row: row)
                    }
                    .buttonStyle(.plain)
                    .onAppear {
                        if row.id == rows.last?.id {
                            Task { await vm.loadNextPage() }
                        }
                    }
                }

                if vm.loadingMore {
                    ProgressView()
                        .tint(CleansiaColors.primary)
                        .padding(.vertical, Spacing.s)
                }

                Color.clear.frame(height: Spacing.xl)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.s)
        }
    }
}

struct NotificationFeedRowCard: View {
    @Environment(\.locale) private var locale
    let row: NotificationFeedRow

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Circle()
                .fill(row.isUnread ? CleansiaColors.primary : Color.clear)
                .frame(width: 8, height: 8)
                .padding(.top, 6)

            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(verbatim: row.title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(verbatim: row.body)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Text(verbatim: NotificationFeedFormat.timestamp(row.createdOn, locale: locale))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.top, Spacing.xxs)
            }
            Spacer(minLength: 0)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

private struct NotificationsInboxErrorView: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "wifi.slash")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.NotificationsInbox.error)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.NotificationsInbox.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
    }
}

private struct NotificationsInboxEmptyView: View {
    var body: some View {
        MascotEmptyState(
            image: Mascot.leaning.image,
            text: L10n.NotificationsInbox.emptyTitle,
            subtitle: L10n.NotificationsInbox.emptySubtitle,
            verticallyCentered: true,
            imageSize: 160,
            titleFont: CleansiaTypography.headlineSmall
        )
    }
}

#if DEBUG
    struct NotificationFeedRowCard_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.s) {
                NotificationFeedRowCard(row: NotificationFeedRow(
                    id: "n-1",
                    title: "Cleaner found! 🎉",
                    body: "Your booking #A-1042 is confirmed. Tap to see the details.",
                    createdOn: Date().addingTimeInterval(-3600),
                    isUnread: true
                ))
                NotificationFeedRowCard(row: NotificationFeedRow(
                    id: "n-2",
                    title: "All done! ✨",
                    body: "Your booking #A-1041 is complete. Tap to leave a review.",
                    createdOn: Date().addingTimeInterval(-90000),
                    isUnread: false
                ))
            }
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
