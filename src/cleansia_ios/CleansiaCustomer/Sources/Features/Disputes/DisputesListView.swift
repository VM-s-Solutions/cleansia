import CleansiaCore
import SwiftUI

struct DisputesListView: View {
    @StateObject private var vm: DisputesListViewModel
    @Environment(\.snackbarController) private var snackbar
    let onDisputeClick: (String) -> Void
    let onCreateDispute: () -> Void
    let onBrowseOrders: () -> Void

    init(
        repository: DisputeRepository,
        snackbar: SnackbarController,
        onDisputeClick: @escaping (String) -> Void,
        onCreateDispute: @escaping () -> Void,
        onBrowseOrders: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: DisputesListViewModel(repository: repository, snackbar: snackbar))
        self.onDisputeClick = onDisputeClick
        self.onCreateDispute = onCreateDispute
        self.onBrowseOrders = onBrowseOrders
    }

    var body: some View {
        content
            .navigationTitle(L10n.Disputes.listTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button(action: startCreate) {
                        Label(L10n.Disputes.listFabNew, systemImage: "plus")
                    }
                    .tint(CleansiaColors.primary)
                }
            }
            .task { await vm.onAppear() }
    }

    private func startCreate() {
        snackbar.showInfo(L10n.Disputes.listFabNoOrder)
        onCreateDispute()
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            DisputesLoadingView()
        case .error:
            ScrollView {
                DisputesErrorView { Task { await vm.retry() } }
                    .frame(maxWidth: .infinity, minHeight: 360)
            }
            .refreshable { await vm.pullToRefresh() }
        case let .loaded(entries) where entries.isEmpty:
            ScrollView {
                DisputesEmptyView(onBrowseOrders: onBrowseOrders)
                    .frame(maxWidth: .infinity, minHeight: 420)
            }
            .refreshable { await vm.pullToRefresh() }
        case .loaded:
            DisputesListContent(vm: vm, onDisputeClick: onDisputeClick)
        }
    }
}

private struct DisputesListContent: View {
    @ObservedObject var vm: DisputesListViewModel
    let onDisputeClick: (String) -> Void

    var body: some View {
        ScrollView {
            LazyVStack(spacing: Spacing.s) {
                ForEach(vm.entries) { dispute in
                    Button {
                        onDisputeClick(dispute.id)
                    } label: {
                        DisputeRowCard(dispute: dispute)
                    }
                    .buttonStyle(.plain)
                    .onAppear {
                        if dispute.id == vm.entries.last?.id {
                            Task { await vm.loadNextPage() }
                        }
                    }
                }

                if vm.loadingMore {
                    ProgressView()
                        .tint(CleansiaColors.primary)
                        .padding(.vertical, Spacing.s)
                }

                Color.clear.frame(height: Spacing.xxl)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.xs)
        }
        .refreshable { await vm.pullToRefresh() }
    }
}

struct DisputeRowCard: View {
    let dispute: DisputeListEntry

    var body: some View {
        HStack(spacing: 0) {
            Rectangle()
                .fill(DisputeStatusPresentation.color(dispute.statusValue))
                .frame(width: 4)

            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack {
                    Text(verbatim: dispute.displayOrderNumber.map { "#\($0)" } ?? "—")
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer()
                    DisputeStatusPill(
                        label: DisputeStatusPresentation.label(dispute.statusName),
                        color: DisputeStatusPresentation.color(dispute.statusValue)
                    )
                }
                Text(verbatim: dispute.reasonName ?? "—")
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .lineLimit(1)
                Text(OrdersFormat.dateTime(dispute.createdOn))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.m)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

struct DisputeStatusPill: View {
    let label: String
    let color: Color

    var body: some View {
        Text(label)
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(color)
            .padding(.horizontal, Spacing.s)
            .padding(.vertical, Spacing.xxs)
            .background(color.opacity(0.14), in: Capsule())
    }
}

private struct DisputesLoadingView: View {
    var body: some View {
        VStack(spacing: Spacing.s) {
            ForEach(0 ..< 3, id: \.self) { _ in
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .fill(CleansiaColors.surfaceVariant)
                    .frame(height: 96)
            }
            Spacer()
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.top, Spacing.s)
    }
}

private struct DisputesErrorView: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "wifi.slash")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Disputes.listErrorTitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.Disputes.listErrorRetry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
    }
}

private struct DisputesEmptyView: View {
    let onBrowseOrders: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "checkmark.shield")
                .font(.system(size: 64))
                .foregroundColor(CleansiaColors.primary)
            Text(L10n.Disputes.listEmptyTitle)
                .font(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
            Text(L10n.Disputes.listEmptySubtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.Disputes.listEmptyCta, action: onBrowseOrders)
                .fixedSize()
        }
        .padding(Spacing.xl)
    }
}

#if DEBUG
    struct DisputeRowCard_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.s) {
                DisputeRowCard(dispute: DisputeListEntry(
                    id: "1",
                    displayOrderNumber: "1042",
                    reasonName: "Quality issue",
                    statusName: "Pending",
                    statusValue: 1,
                    createdOn: Date()
                ))
                DisputeRowCard(dispute: DisputeListEntry(
                    id: "2",
                    displayOrderNumber: "1043",
                    reasonName: "Damaged property",
                    statusName: "Resolved",
                    statusValue: 4,
                    createdOn: Date()
                ))
            }
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
