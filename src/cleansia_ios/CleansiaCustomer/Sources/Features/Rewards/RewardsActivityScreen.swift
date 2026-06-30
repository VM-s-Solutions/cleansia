import CleansiaCore
import SwiftUI

struct RewardsActivityScreen: View {
    @StateObject private var vm: RewardsActivityViewModel

    init(loyaltyRepository: LoyaltyRepository, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: RewardsActivityViewModel(
            loyaltyRepository: loyaltyRepository,
            snackbar: snackbar
        ))
    }

    var body: some View {
        content
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
            .background(CleansiaColors.background.ignoresSafeArea())
            .navigationTitle(L10n.Rewards.activityTitle)
            .navigationBarTitleDisplayMode(.inline)
            .task { await vm.onAppear() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            ScrollView {
                RewardsStateMessage(
                    systemImage: "wifi.slash",
                    message: L10n.Rewards.errorLoad
                ) { Task { await vm.retry() } }
                    .frame(maxWidth: .infinity, minHeight: 360)
            }
            .refreshable { await vm.pullToRefresh() }
        case let .loaded(items) where items.isEmpty:
            ScrollView {
                RewardsStateMessage(systemImage: "sparkles", message: L10n.Rewards.emptyActivity)
                    .frame(maxWidth: .infinity, minHeight: 360)
            }
            .refreshable { await vm.pullToRefresh() }
        case let .loaded(items):
            RewardsActivityList(vm: vm, items: items)
        }
    }
}

private struct RewardsActivityList: View {
    @ObservedObject var vm: RewardsActivityViewModel
    let items: [LoyaltyActivityItem]

    var body: some View {
        ScrollView {
            LazyVStack(spacing: Spacing.s) {
                ForEach(items) { item in
                    RewardsActivityRow(item: item)
                        .padding(Spacing.m)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(
                            CleansiaColors.surface,
                            in: RoundedRectangle(cornerRadius: CornerRadius.small)
                        )
                        .overlay(
                            RoundedRectangle(cornerRadius: CornerRadius.small)
                                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
                        )
                        .onAppear {
                            if item.id == items.last?.id {
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
        .refreshable { await vm.pullToRefresh() }
    }
}

#if DEBUG
    struct RewardsActivityRow_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: 12) {
                RewardsActivityRow(item: LoyaltyActivityItem(
                    type: 1, points: 100, source: 1,
                    orderId: "o1", orderDisplayNumber: "1042",
                    occurredOn: Date()
                ))
                RewardsActivityRow(item: LoyaltyActivityItem(
                    type: 2, points: -50, source: 2,
                    orderId: "o2", orderDisplayNumber: "1043",
                    occurredOn: Date()
                ))
            }
            .padding()
            .background(CleansiaColors.background)
        }
    }
#endif
