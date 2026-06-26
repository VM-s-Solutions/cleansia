import CleansiaCore
import SwiftUI

struct DashboardView: View {
    @StateObject private var vm: DashboardViewModel
    let onOpenEarnings: () -> Void
    let onOpenOrders: () -> Void

    init(
        client: PartnerDashboardClient,
        onOpenEarnings: @escaping () -> Void = {},
        onOpenOrders: @escaping () -> Void = {}
    ) {
        _vm = StateObject(wrappedValue: DashboardViewModel(client: client))
        self.onOpenEarnings = onOpenEarnings
        self.onOpenOrders = onOpenOrders
    }

    var body: some View {
        content
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .error(error):
            DashboardErrorView(error: error) { Task { await vm.load() } }
        case let .loaded(data):
            DashboardContent(data: data, onOpenEarnings: onOpenEarnings, onOpenOrders: onOpenOrders)
        }
    }
}

private struct DashboardErrorView: View {
    let error: ApiError
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "exclamationmark.triangle")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.error)
            Text(ApiErrorLocalizer().message(for: error))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

struct DashboardContent: View {
    let data: DashboardData
    let onOpenEarnings: () -> Void
    let onOpenOrders: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                GreetingBar(firstName: data.firstName)
                HeroCard(hero: data.hero, currencyCode: data.currencyCode, onOpenOrders: onOpenOrders)
                WeeklyEarningsCard(data: data, onClick: onOpenEarnings)
                if let period = data.payPeriod {
                    PayPeriodCard(period: period, currencyCode: data.currencyCode, onClick: onOpenEarnings)
                }
                LastMonthCard(data: data)
            }
            .padding(.vertical, Spacing.m)
        }
    }
}

private struct GreetingBar: View {
    let firstName: String?

    var body: some View {
        HStack(spacing: Spacing.s) {
            Mascot.waving.image
                .resizable()
                .scaledToFit()
                .frame(width: 40, height: 40)
            VStack(alignment: .leading, spacing: 2) {
                Text(DashboardGreeting.text(firstName: firstName))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.onBackground)
                Text(DashboardGreeting.dateLine())
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
        }
        .padding(.horizontal, Spacing.m)
    }
}

private struct HeroCard: View {
    let hero: DashboardHero
    let currencyCode: String?
    let onOpenOrders: () -> Void

    var body: some View {
        switch hero {
        case let .nextJob(title, subtitle):
            HeroRowCard(
                label: nil,
                title: title,
                subtitle: subtitle,
                mascot: .cleaning,
                onClick: onOpenOrders
            )
        case let .availableWork(jobCount, potentialEarnings):
            HeroRowCard(
                label: L10n.Dashboard.availableWorkLabel,
                title: L10n.Dashboard.availableNowCount(jobCount),
                subtitle: potentialEarnings > 0
                    ? L10n.Dashboard.earnUpTo(DashboardFormat.money(potentialEarnings, currencyCode: currencyCode))
                    : nil,
                mascot: .ready,
                onClick: onOpenOrders
            )
        case .empty:
            Button(action: onOpenOrders) {
                HStack(spacing: Spacing.m) {
                    Mascot.leaning.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 64, height: 64)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(L10n.Dashboard.noJobsYetTitle)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        Text(L10n.Dashboard.noJobsYetSubtitle)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .multilineTextAlignment(.leading)
                    }
                    Spacer()
                    Image(systemName: "arrow.right")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .cardPadding()
            }
            .buttonStyle(.plain)
            .padding(.horizontal, Spacing.m)
        }
    }
}

private struct HeroRowCard: View {
    let label: String?
    let title: String
    let subtitle: String?
    let mascot: Mascot
    let onClick: () -> Void

    var body: some View {
        Button(action: onClick) {
            HStack(spacing: Spacing.s) {
                mascot.image
                    .resizable()
                    .scaledToFit()
                    .frame(width: 56, height: 56)
                VStack(alignment: .leading, spacing: 2) {
                    if let label {
                        Text(label)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.primary)
                    }
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                    if let subtitle {
                        Text(subtitle)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .lineLimit(1)
                    }
                }
                Spacer()
                Image(systemName: "arrow.right")
                    .font(.system(size: 18))
                    .foregroundColor(CleansiaColors.primary)
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Spacing.m)
    }
}

#if DEBUG
    struct DashboardView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                stateView(.loading).previewDisplayName("Loading")
                stateView(.error(ApiError(httpStatus: 500))).previewDisplayName("Error")
                DashboardContent(data: sample(hero: .empty), onOpenEarnings: {}, onOpenOrders: {})
                    .previewDisplayName("Loaded · empty hero")
                DashboardContent(
                    data: sample(hero: .nextJob(title: "Today 14:00", subtitle: "Jana · Praha 5")),
                    onOpenEarnings: {},
                    onOpenOrders: {}
                )
                .previewDisplayName("Loaded · next-job hero")
                DashboardContent(
                    data: sample(hero: .availableWork(jobCount: 2, potentialEarnings: 650)),
                    onOpenEarnings: {},
                    onOpenOrders: {}
                )
                .previewDisplayName("Loaded · available-work hero")
            }
            .background(CleansiaColors.background)
        }

        @ViewBuilder
        private static func stateView(_ state: UiState<DashboardData>) -> some View {
            switch state {
            case .loading:
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            case let .error(error):
                DashboardErrorView(error: error, onRetry: {})
            case let .loaded(data):
                DashboardContent(data: data, onOpenEarnings: {}, onOpenOrders: {})
            }
        }

        private static func sample(hero: DashboardHero) -> DashboardData {
            DashboardData(
                firstName: "Jana",
                currencyCode: "CZK",
                weekEarnings: 6262,
                weekCompletedCount: 4,
                todayEarnings: 1238,
                payPeriod: DashboardData.PayPeriod(
                    start: Date(timeIntervalSinceNow: -6 * 86400),
                    end: Date(timeIntervalSinceNow: 8 * 86400),
                    earnings: 9500,
                    nextPayoutDate: Date(timeIntervalSinceNow: 8 * 86400)
                ),
                lastMonthEarnings: 18450,
                lastMonthCompletedOrders: 22,
                thisMonthCompletedOrders: 26,
                averageRating: 4.8,
                ratingCount: 31,
                hero: hero
            )
        }
    }
#endif
