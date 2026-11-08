import SwiftUI

/// What the inbox and its bell say. Each app words them in its own catalog.
public protocol NotificationsInboxCopy {
    static var title: String { get }
    static var close: String { get }
    static var error: String { get }
    static var retry: String { get }
    static var emptyTitle: String { get }
    static var emptySubtitle: String { get }
    static func unreadA11y(_ label: String) -> String
}

/// The notifications inbox behind the bell — the server-backed feed. Newest-first
/// pages of 20 with load-more; opening fires the watermarked mark-all; the mascot
/// empty state is the zero-rows state. Tapping a row that resolves a destination
/// dismisses the sheet and hands it to the shell; a target-less row just marks
/// read and stays put. Each app supplies its words, its mascot, its row templates
/// and its destinations.
public struct NotificationsInboxSheet<Destination>: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var vm: NotificationsInboxViewModel<Destination>
    private let copy: NotificationsInboxCopy.Type
    private let emptyMascot: Mascot
    private let rows: ([UserNotification]) -> [NotificationFeedRow]
    private let onDestination: (Destination) -> Void

    public init(
        client: NotificationFeedClient,
        badge: NotificationBadgeModel,
        snackbar: SnackbarController,
        copy: NotificationsInboxCopy.Type,
        emptyMascot: Mascot,
        rows: @escaping ([UserNotification]) -> [NotificationFeedRow],
        destination: @escaping (UserNotification) -> Destination?,
        onDestination: @escaping (Destination) -> Void
    ) {
        _vm = StateObject(wrappedValue: NotificationsInboxViewModel(
            client: client,
            badge: badge,
            snackbar: snackbar,
            destination: destination
        ))
        self.copy = copy
        self.emptyMascot = emptyMascot
        self.rows = rows
        self.onDestination = onDestination
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                CleansiaColors.background.ignoresSafeArea()
                content
            }
            .navigationTitle(copy.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button(copy.close) { dismiss() }
                        .tint(CleansiaColors.primaryText)
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
            NotificationsInboxErrorView(copy: copy) { Task { await vm.retry() } }
        case let .loaded(items):
            let feedRows = rows(items)
            if feedRows.isEmpty {
                NotificationsInboxEmptyView(copy: copy, mascot: emptyMascot)
            } else {
                NotificationsInboxList(vm: vm, rows: feedRows)
            }
        }
    }
}

private struct NotificationsInboxList<Destination>: View {
    @ObservedObject var vm: NotificationsInboxViewModel<Destination>
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
    let copy: NotificationsInboxCopy.Type
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "wifi.slash")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(copy.error)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(copy.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
    }
}

private struct NotificationsInboxEmptyView: View {
    let copy: NotificationsInboxCopy.Type
    let mascot: Mascot

    var body: some View {
        MascotEmptyState(
            image: mascot.image,
            text: copy.emptyTitle,
            subtitle: copy.emptySubtitle,
            verticallyCentered: true,
            imageSize: 160,
            titleStyle: CleansiaTypography.headlineSmall
        )
    }
}

enum NotificationFeedFormat {
    /// "now" / "2 hours ago" / "yesterday" within the last week, then the weekday,
    /// day, month and time — both rendering in the app locale.
    static func timestamp(_ date: Date, relativeTo now: Date = Date(), locale: Locale) -> String {
        let clamped = min(date, now)
        guard now.timeIntervalSince(clamped) < 7 * 24 * 3600 else {
            let formatter = DateFormatter()
            formatter.locale = locale
            formatter.setLocalizedDateFormatFromTemplate("EEE d MMM HH:mm")
            return formatter.string(from: clamped)
        }
        let formatter = RelativeDateTimeFormatter()
        formatter.locale = locale
        formatter.dateTimeStyle = .named
        return formatter.localizedString(for: clamped, relativeTo: now)
    }
}

#if DEBUG
    struct NotificationFeedRowCard_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.s) {
                NotificationFeedRowCard(
                    row: NotificationFeedRow(
                        id: "n-1",
                        title: "New jobs available",
                        body: "New jobs near you: 3. Tap to view.",
                        createdOn: Date().addingTimeInterval(-3600),
                        isUnread: true
                    )
                )
                NotificationFeedRowCard(
                    row: NotificationFeedRow(
                        id: "n-2",
                        title: "New jobs available",
                        body: "New jobs near you: 1. Tap to view.",
                        createdOn: Date().addingTimeInterval(-90000),
                        isUnread: false
                    )
                )
            }
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
