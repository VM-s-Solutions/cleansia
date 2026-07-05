import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct EarningsView: View {
    @StateObject private var vm: EarningsViewModel
    @State private var path = NavigationPath()

    private let payrollClient: PartnerPayrollClient
    private let invoicesStaleness: InvoicesStaleness
    private let snackbar: SnackbarController

    init(
        dashboardClient: PartnerDashboardClient,
        payrollClient: PartnerPayrollClient,
        invoicesStaleness: InvoicesStaleness,
        snackbar: SnackbarController
    ) {
        _vm = StateObject(wrappedValue: EarningsViewModel(client: dashboardClient))
        self.payrollClient = payrollClient
        self.invoicesStaleness = invoicesStaleness
        self.snackbar = snackbar
    }

    var body: some View {
        NavigationStack(path: $path) {
            content
                .navigationTitle(L10n.Earnings.title)
                .navigationBarTitleDisplayMode(.inline)
                .background(CleansiaColors.background.ignoresSafeArea())
                .navigationDestination(for: EarningsRoute.self, destination: destination)
        }
        .task { await vm.load() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background.ignoresSafeArea())
        case let .error(error):
            EarningsErrorView(error: error) { Task { await vm.load() } }
        case let .loaded(stats):
            EarningsContent(stats: stats, onOpenInvoices: { path.append(EarningsRoute.invoices) })
                .background(CleansiaColors.background.ignoresSafeArea())
        }
    }

    @ViewBuilder
    private func destination(_ route: EarningsRoute) -> some View {
        switch route {
        case .invoices:
            InvoicesListView(
                client: payrollClient,
                staleness: invoicesStaleness,
                snackbar: snackbar,
                onOpenInvoice: { path.append(EarningsRoute.invoiceDetail(id: $0)) }
            )
        case let .invoiceDetail(id):
            InvoiceDetailView(
                invoiceId: id,
                client: payrollClient,
                snackbar: snackbar,
                onOpenPeriodPay: { payPeriodId, currencyCode in
                    path.append(EarningsRoute.periodPay(payPeriodId: payPeriodId, currencyCode: currencyCode))
                }
            )
        case let .periodPay(payPeriodId, currencyCode):
            PeriodPayView(
                payPeriodId: payPeriodId,
                currencyCode: currencyCode,
                client: payrollClient,
                snackbar: snackbar
            )
        }
    }
}

private struct EarningsErrorView: View {
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
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct EarningsView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                EarningsContent(stats: sample, onOpenInvoices: {})
                    .background(CleansiaColors.background)
                    .previewDisplayName("Loaded")
                EarningsContent(stats: DashboardStatsDto(), onOpenInvoices: {})
                    .background(CleansiaColors.background)
                    .previewDisplayName("Loaded · empty")
            }
        }

        private static var sample: DashboardStatsDto {
            DashboardStatsDto(
                thisMonthCompletedOrders: 26,
                lastMonthCompletedOrders: 22,
                todayEarnings: 1238,
                todayCompletedCount: 1,
                weekEarnings: 6262,
                weekCompletedCount: 4,
                lastMonthEarnings: 18450,
                currentPeriodEarnings: 9500,
                currentPayPeriodStart: Date(timeIntervalSinceNow: -6 * 86400),
                currentPayPeriodEnd: Date(timeIntervalSinceNow: 8 * 86400),
                nextPayoutDate: Date(timeIntervalSinceNow: 8 * 86400),
                latestInvoiceStatus: nil,
                currencyCode: "CZK"
            )
        }
    }
#endif
