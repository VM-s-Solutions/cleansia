import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// Earnings, the invoices, one invoice and the dashboard on a failed first load: the four screens that answer a
/// failure with the same message and Retry.
extension PartnerScreenCaptureTests {
    func testErrorRetry() async throws {
        let suite = "PartnerScreenCaptureTests.errorRetry"
        defer { UserDefaults().removePersistentDomain(forName: suite) }
        let failure = ApiError(httpStatus: 500)
        for theme in ["light", "dark"] {
            try MockCapture.use("en", near: Self.self)
            let dashboardClient = DashboardViewModelTests.FakeDashboardClient()
            dashboardClient.statsResult = .failure(failure)
            let payroll = FakePayrollClient()
            payroll.invoicesResult = .failure(failure)
            payroll.invoiceResult = .failure(failure)
            let feed = FakeNotificationFeedClient()
            let staleness = OrdersStaleness()
            let pages: [(String, AnyView)] = [
                ("earnings", AnyView(EarningsView(
                    dashboardClient: dashboardClient,
                    payrollClient: payroll,
                    invoicesStaleness: InvoicesStaleness(),
                    snackbar: SnackbarController()
                ))),
                ("invoices", AnyView(pushed {
                    InvoicesListView(
                        client: payroll,
                        staleness: InvoicesStaleness(),
                        snackbar: SnackbarController(),
                        onOpenInvoice: { _ in }
                    )
                })),
                ("invoice", AnyView(pushed {
                    InvoiceDetailView(
                        invoiceId: "inv-1",
                        client: payroll,
                        snackbar: SnackbarController(),
                        onOpenPeriodPay: { _, _, _ in }
                    )
                })),
                ("dashboard", AnyView(DashboardView(
                    client: dashboardClient,
                    notificationBadge: NotificationBadgeModel(client: feed, feedEventKeys: PartnerFeedEventKeys.all),
                    notificationFeedClient: feed,
                    profileClient: FakePartnerProfileClient(),
                    settings: UserDefaultsAppSettingsStore(defaults: UserDefaults(suiteName: suite) ?? .standard),
                    staleness: staleness,
                    snackbar: SnackbarController(),
                    pendingOffers: PendingOffersStore(client: FakePartnerOrderClient(), ordersStaleness: staleness)
                )))
            ]
            for (screen, page) in pages {
                await MockCapture.shoot(
                    self,
                    name: name(screen, "error", theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    page.styled(theme)
                }
            }
        }
    }
}
