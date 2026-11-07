import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// Native captures of the partner screens the Orders captures do not reach: the bank step of the onboarding chain,
/// My documents, the dashboard's last-month card on a rise and on a fall, the take sheet's slide while its contract
/// loads and while the take is sent, the Language and Theme pickers, and the invoices with one invoice. Named
/// `<screen>-<state>-ios-<w>x<h>-<lang>-<theme>`; they run with the Orders captures
/// (`TEST_RUNNER_PARTNER_ORDERS_CAPTURE=1`).
@MainActor
final class PartnerScreenCaptureTests: XCTestCase {
    private let phone = CGSize(width: 393, height: 852)
    private var restoreBundle: Bundle!
    private var restoreTag: String!

    override func setUpWithError() throws {
        try super.setUpWithError()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
        try XCTSkipUnless(
            ProcessInfo.processInfo.environment["PARTNER_ORDERS_CAPTURE"] == "1",
            "captures run on request"
        )
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    /// Personal and address done, identification still open, bank current: every state a step node draws.
    func testOnboardingBankStep() async throws {
        let client = FakePartnerProfileClient()
        client.statusResult = .success(RegistrationCompletionStatus(
            hasCompletedProfile: false,
            missingFields: (ProfileSection.identification.ownedFields.union(ProfileSection.bank.ownedFields)).sorted()
        ))
        for theme in ["light", "dark"] {
            try MockCapture.use("en", near: Self.self)
            let chain = OnboardingChainViewModel(client: client)
            await chain.load()
            await MockCapture.shoot(
                self,
                name: name("bank", "onboarding", theme),
                size: phone,
                statusBar: 54,
                homeIndicator: 34,
                scroll: .rest
            ) {
                pushed {
                    BankSectionView(
                        client: client,
                        snackbar: SnackbarController(),
                        chainVM: chain,
                        onboarding: true,
                        onSaved: {}
                    )
                }
                .styled(theme)
            }
        }
    }

    func testDocuments() async throws {
        let client = FakePartnerProfileClient()
        client.documentsResult = .success([
            GetMyDocumentsMyDocumentDto(documentId: "doc-1", fileName: "passport.pdf", documentType: ._1, status: ._2),
            GetMyDocumentsMyDocumentDto(documentId: "doc-2", fileName: "licence.pdf", documentType: ._2, status: ._1),
            GetMyDocumentsMyDocumentDto(documentId: "doc-3", fileName: "insurance.pdf", documentType: ._3, status: ._3)
        ])
        for theme in ["light", "dark"] {
            try MockCapture.use("en", near: Self.self)
            await MockCapture.shoot(
                self,
                name: name("documents", "list", theme),
                size: phone,
                statusBar: 54,
                homeIndicator: 34,
                scroll: .rest
            ) {
                pushed {
                    DocumentsSectionView(client: client, snackbar: SnackbarController())
                }
                .styled(theme)
            }
        }
    }

    func testLastMonthCard() async throws {
        for (state, thisMonth) in [("rise", 13), ("fall", 7)] {
            let data = DashboardData(
                firstName: "Jana",
                currencyCode: "CZK",
                weekEarnings: 0,
                weekCompletedCount: 0,
                todayEarnings: 0,
                payPeriod: nil,
                lastMonthEarnings: 12480,
                lastMonthCompletedOrders: 10,
                thisMonthCompletedOrders: thisMonth,
                averageRating: 4.8,
                ratingCount: 21,
                todaysJobsCount: nil,
                hero: .empty
            )
            for theme in ["light", "dark"] {
                try MockCapture.use("en", near: Self.self)
                await MockCapture.shoot(
                    self,
                    name: name("dashboard", "lastmonth-\(state)", theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    VStack {
                        LastMonthCard(data: data)
                        Spacer()
                    }
                    .padding(.top, Spacing.m)
                    .background(CleansiaColors.background)
                    .styled(theme)
                }
            }
        }
    }

    /// The take sheet's slide while the contract loads and while the take is sent, the two faces the Orders
    /// captures do not show.
    func testTakeSheetSlide() async throws {
        let row = OrdersDesignFixture.available()[1]
        let contract = OrdersDesignFixture.contract(for: row)
        for theme in ["light", "dark"] {
            try MockCapture.use("en", near: Self.self)
            let sheets: [(String, WorkContractSheetContent)] = [
                ("loading", WorkContractSheetContent(
                    request: .take(orderId: "order-1"),
                    state: .loading,
                    board: BoardTake(snapshot: row),
                    now: OrdersDesignFixture.now
                )),
                ("sending", WorkContractSheetContent(
                    request: .take(orderId: "order-1"),
                    state: .loaded(contract),
                    board: BoardTake(snapshot: row),
                    submitting: true,
                    now: OrdersDesignFixture.now
                ))
            ]
            for (state, sheet) in sheets {
                await MockCapture.shoot(
                    self,
                    name: name("take", state, theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    sheet.styled(theme)
                }
            }
        }
    }

    /// The Language and Theme pickers as Profile pushes them, the chosen row checked.
    func testPreferencePickers() async throws {
        let suite = "PartnerScreenCaptureTests.pickers"
        defer { UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite) }
        for theme in ["light", "dark"] {
            try MockCapture.use("en", near: Self.self)
            let preferences = try PreferencesModel(
                settings: UserDefaultsAppSettingsStore(
                    defaults: XCTUnwrap(UserDefaults(suiteName: suite)),
                    preferredLanguageTags: { ["en"] }
                ),
                languageSync: SilentLanguageSync(),
                appGroup: suite
            )
            let pickers: [(String, AnyView)] = [
                ("language", AnyView(LanguagePickerView(preferences: preferences, onSelected: {}))),
                ("theme", AnyView(ThemePickerView(preferences: preferences, onSelected: {})))
            ]
            for (screen, picker) in pickers {
                await MockCapture.shoot(
                    self,
                    name: name(screen, "picker", theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    pushed { picker }
                        .styled(theme)
                }
            }
        }
    }

    /// The invoices and one invoice: their figures to the haléř and their dates.
    func testInvoices() async throws {
        let issued = Date(timeIntervalSince1970: 1_762_340_400)
        let invoices = [
            Invoice.stub(
                id: "inv-2", invoiceNumber: "INV-2026-0042", payPeriodLabel: "1 – 15 Oct 2026", totalOrders: 9,
                totalAmount: 4120.3, status: ._1, generatedAt: issued
            ),
            Invoice.stub(
                id: "inv-1", invoiceNumber: "INV-2026-0041", payPeriodLabel: "16 – 30 Sep 2026", totalOrders: 12,
                totalAmount: 5312.75, status: ._2, generatedAt: issued.addingTimeInterval(-15 * 86400),
                paidAt: issued.addingTimeInterval(-10 * 86400)
            )
        ]
        for theme in ["light", "dark"] {
            try MockCapture.use("en", near: Self.self)
            let pages: [(String, AnyView)] = [
                ("invoices", AnyView(InvoicesListContent(invoices: invoices, onOpenInvoice: { _ in }))),
                ("invoice", AnyView(InvoiceDetailContent(
                    invoice: .stub(totalAmount: 4120.3),
                    canOpenPdf: true,
                    isDownloading: false,
                    onOpenPeriodPay: nil,
                    onOpenPdf: {},
                    onCopy: { _ in }
                )))
            ]
            for (screen, page) in pages {
                await MockCapture.shoot(
                    self,
                    name: name(screen, "earnings", theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    page
                        .background(CleansiaColors.background)
                        .styled(theme)
                }
            }
        }
    }

    /// The page pushed onto a stack, as the app shows it. Hosted as the stack's root, it never ran its load.
    private func pushed(@ViewBuilder page: @escaping () -> some View) -> some View {
        NavigationStack(path: .constant(["page"])) {
            Color.clear.navigationDestination(for: String.self) { _ in page() }
        }
    }

    private func name(_ screen: String, _ state: String, _ theme: String) -> String {
        "\(screen)-\(state)-ios-\(Int(phone.width))x\(Int(phone.height))-en-\(theme)"
    }
}

private extension View {
    func styled(_ theme: String) -> some View {
        tint(CleansiaColors.primaryText)
            .environment(\.locale, Locale(identifier: "en"))
            .preferredColorScheme(theme == "dark" ? .dark : .light)
            .dynamicTypeSize(.large)
    }
}

/// The notifications inbox the dashboard bell opens: a page of rows (two unread, one more than a week old), the empty
/// inbox and a failed load; and the bell on the dashboard over three, over a hundred and over none.
extension PartnerScreenCaptureTests {
    func testNotificationsInbox() async throws {
        let now = Date()
        let rows = [
            UserNotification(
                id: "n-1", eventKey: "order.new_available", args: ["count": "3"],
                createdOn: now.addingTimeInterval(-600), readOn: nil
            ),
            UserNotification(
                id: "n-2", eventKey: "order.seat_open", args: ["orderNumber": "A-2201", "orderId": "order-1"],
                createdOn: now.addingTimeInterval(-3 * 3600), readOn: nil
            ),
            UserNotification(
                id: "n-3", eventKey: "payroll.invoice_paid", args: ["invoiceId": "inv-1"],
                createdOn: now.addingTimeInterval(-26 * 3600), readOn: now
            ),
            UserNotification(
                id: "n-4", eventKey: "order.preferred_offer", args: ["orderNumber": "A-2188", "orderId": "order-2"],
                createdOn: Date(timeIntervalSince1970: 1_759_917_600), readOn: now
            )
        ]
        let pages: [(String, ApiResult<UserNotificationPage>)] = [
            ("list", .success(UserNotificationPage(items: rows, total: rows.count))),
            ("empty", .success(UserNotificationPage(items: [], total: 0))),
            ("error", .failure(ApiError(httpStatus: 500)))
        ]
        for theme in ["light", "dark"] {
            for (state, page) in pages {
                try MockCapture.use("en", near: Self.self)
                let client = FakeNotificationFeedClient()
                client.pageResults = [page]
                await MockCapture.shoot(
                    self,
                    name: name("inbox", state, theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    NotificationsInboxSheet(
                        client: client,
                        badge: NotificationBadgeModel(client: client, feedEventKeys: PartnerFeedEventKeys.all),
                        snackbar: SnackbarController(),
                        onDestination: { _ in }
                    )
                    .styled(theme)
                }
            }
        }
    }

    func testDashboardBell() async throws {
        let data = DashboardData(
            firstName: "Jana",
            currencyCode: "CZK",
            weekEarnings: 0,
            weekCompletedCount: 0,
            todayEarnings: 0,
            payPeriod: nil,
            lastMonthEarnings: 12480,
            lastMonthCompletedOrders: 10,
            thisMonthCompletedOrders: 13,
            averageRating: 4.8,
            ratingCount: 21,
            todaysJobsCount: nil,
            hero: .empty
        )
        for theme in ["light", "dark"] {
            for (state, unread) in [("unread", 3), ("capped", 120), ("read", 0)] {
                try MockCapture.use("en", near: Self.self)
                await MockCapture.shoot(
                    self,
                    name: name("dashboard", "bell-\(state)", theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    DashboardContent(data: data, unreadCount: unread, onOpenEarnings: {}, onOpenOrders: {})
                        .background(CleansiaColors.background)
                        .styled(theme)
                }
            }
        }
    }
}

/// The signed-in devices as Profile pushes them: this phone, another, a browser and one that names no platform; the
/// empty list and a failed load.
extension PartnerScreenCaptureTests {
    func testDevices() async throws {
        let devices = [
            UserDevice(
                id: "row-1", platform: "ios", deviceId: "device-current",
                lastActiveAt: Date(timeIntervalSince1970: 1_762_340_400), isCurrent: true
            ),
            UserDevice(
                id: "row-2", platform: "android", deviceId: "device-other",
                lastActiveAt: Date(timeIntervalSince1970: 1_762_000_000), isCurrent: false
            ),
            UserDevice(id: "row-3", platform: "web", deviceId: "device-web", lastActiveAt: nil, isCurrent: false),
            UserDevice(id: "row-4", platform: nil, deviceId: nil, lastActiveAt: nil, isCurrent: false)
        ]
        let results: [(String, ApiResult<[UserDevice]>)] = [
            ("list", .success(devices)),
            ("empty", .success([])),
            ("error", .failure(ApiError(httpStatus: 500)))
        ]
        for theme in ["light", "dark"] {
            for (state, result) in results {
                try MockCapture.use("en", near: Self.self)
                let client = FakePartnerDevicesClient()
                client.myDevicesResult = result
                await MockCapture.shoot(
                    self,
                    name: name("devices", state, theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest
                ) {
                    pushed {
                        DevicesView(
                            client: client,
                            authClient: CaptureAuthClient(),
                            snackbar: SnackbarController(),
                            onSignedOut: {}
                        )
                    }
                    .styled(theme)
                }
            }
        }
    }
}

private final class CaptureAuthClient: AuthClient {
    func signOutLocal() async {}
    func logout() async {}
}

/// The address picker as the address step opens it: with no location to centre on, the card asks for a drag; with
/// one, it shows the address under the pin.
extension PartnerScreenCaptureTests {
    func testAddressPicker() async throws {
        for theme in ["light", "dark"] {
            for (state, location) in [
                ("pick", CaptureLocation(granted: false)),
                ("resolved", CaptureLocation(granted: true))
            ] {
                try MockCapture.use("en", near: Self.self)
                await MockCapture.shoot(
                    self,
                    name: name("picker", state, theme),
                    size: phone,
                    statusBar: 54,
                    homeIndicator: 34,
                    scroll: .rest,
                    settle: 1.2
                ) {
                    NavigationStack {
                        AddressPickerScreen(
                            geocoding: CaptureGeocoder(),
                            mapProvider: PreviewMapProvider(),
                            serviceArea: ServiceAreaProvider(
                                dataSource: PartnerServiceAreaDataSource(client: FakePartnerProfileClient())
                            ),
                            copy: L10n.AddressPicker.self,
                            onConfirmed: { _ in },
                            onBack: {}
                        )
                    }
                    .environment(\.locationProvider, location)
                    .styled(theme)
                }
            }
        }
    }
}

private final class CaptureGeocoder: GeocodingService, @unchecked Sendable {
    func reverseGeocode(_: Coordinate) async -> GeocodedAddress? {
        GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Vinohradská 12, Praha"
        )
    }

    func forwardGeocode(query _: String, countryIsoCodes _: [String]) async -> [GeocodedAddress] {
        []
    }
}

@MainActor
private final class CaptureLocation: LocationProvider {
    private let granted: Bool

    init(granted: Bool) {
        self.granted = granted
    }

    var authorizationStatus: LocationAuthorizationStatus {
        granted ? .authorized : .denied
    }

    func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
        authorizationStatus
    }

    func currentLocation() async -> Coordinate? {
        granted ? Coordinate(latitude: 50.0755, longitude: 14.4378) : nil
    }
}
