import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct PeriodPayView: View {
    @StateObject private var vm: PeriodPayViewModel

    init(
        payPeriodId: String,
        currencyCode: String?,
        client: PartnerPayrollClient,
        snackbar: SnackbarController
    ) {
        _vm = StateObject(wrappedValue: PeriodPayViewModel(
            payPeriodId: payPeriodId,
            currencyCode: currencyCode,
            client: client,
            snackbar: snackbar
        ))
    }

    var body: some View {
        content
            .navigationTitle(L10n.PeriodPay.title)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background.ignoresSafeArea())
        case .error:
            PeriodPayErrorView { Task { await vm.load() } }
        case let .loaded(summary):
            PeriodPayContent(summary: summary, currencyCode: vm.currencyCode)
                .background(CleansiaColors.background.ignoresSafeArea())
        }
    }
}

private struct PeriodPayErrorView: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.l) {
            Text(L10n.PeriodPay.error)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct PeriodPayView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                PeriodPayContent(summary: sample, currencyCode: "CZK")
                    .background(CleansiaColors.background)
                    .previewDisplayName("Loaded")
                PeriodPayContent(summary: emptySample, currencyCode: "CZK")
                    .background(CleansiaColors.background)
                    .previewDisplayName("Loaded · empty jobs")
            }
        }

        private static var sample: PeriodPaySummary {
            PeriodPaySummary(
                payPeriodLabel: "1 – 15 Jun 2026",
                totalOrders: 3,
                totalBasePay: 3600,
                totalExtrasPay: 300,
                totalExpensesPay: 200,
                totalBonusPay: 150,
                totalDeductionPay: 50,
                grandTotal: 4200,
                orderPays: (1 ... 3).map { index in
                    OrderPayLine(
                        id: "line-\(index)",
                        orderNumber: "ORD-100\(index)",
                        totalPay: 1400,
                        createdOn: Date()
                    )
                }
            )
        }

        private static var emptySample: PeriodPaySummary {
            PeriodPaySummary(
                payPeriodLabel: nil,
                totalOrders: 0,
                totalBasePay: 0,
                totalExtrasPay: 0,
                totalExpensesPay: 0,
                totalBonusPay: 0,
                totalDeductionPay: 0,
                grandTotal: 0,
                orderPays: []
            )
        }
    }
#endif
