import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import XCTest
@testable import CleansiaPartner

/// Native captures of the partner screens the Orders captures do not reach: the bank step of the onboarding chain,
/// My documents, the dashboard's last-month card on a rise and on a fall, the take sheet's slide while its contract
/// loads and while the take is sent, and the Language and Theme pickers. Named
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
