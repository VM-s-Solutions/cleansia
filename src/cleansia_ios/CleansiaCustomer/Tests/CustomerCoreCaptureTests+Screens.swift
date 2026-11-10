import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The other customer screens that draw the status pill, the text link or a chip, and the disputes, which stamp
/// their dates.
extension CustomerCoreCaptureTests {
    func testOrdersList() async throws {
        for (language, theme, full) in [
            ("en", "light", false), ("en", "dark", false), ("uk", "light", false), ("en", "light", true),
            ("en", "dark", true)
        ] {
            try use(language)
            let client = FakeOrderClient()
            let rows = CustomerCaptureFixture.summaries()
            client.pages = [OrdersPage(items: rows, total: rows.count)]
            let repository = OrderRepository(client: client)
            let markets = await MarketFixtures.resolved()
            let canvas = full ? CGSize(width: Self.phone.width, height: 1700) : Self.phone
            await CustomerCapture.shoot(
                name("orders", "all", Self.phone, language, theme, full: full),
                size: canvas,
                to: self,
                hold: 0.4
            ) {
                tabFrame(theme: theme, height: canvas.height) {
                    OrdersTab(
                        repository: repository,
                        marketStore: markets,
                        snackbar: SnackbarController(),
                        onOrderClick: { _ in },
                        onBookCleaning: {}
                    )
                }
                .styled(language: language, theme: theme)
            }
        }
    }

    func testHomeSections() async throws {
        let templates = [
            RecurringFixtures.template(),
            RecurringFixtures.template(id: "tpl-2", frequency: 2, timeOfDay: "16:30")
        ]
        let rows = CustomerCaptureFixture.summaries()
        let recent = [rows[4], rows[5], rows[0]]
        for (language, theme) in [("en", "light"), ("en", "dark"), ("uk", "light")] {
            try use(language)
            await CustomerCapture.shoot(
                name("home", "sections", Self.phone, language, theme),
                size: Self.phone,
                to: self,
                hold: 0.3
            ) {
                tabFrame(theme: theme) {
                    ScrollView {
                        VStack(alignment: .leading, spacing: Spacing.l) {
                            RecentBookingsSection(
                                orders: recent,
                                onOrderTap: { _ in },
                                onSeeAll: {}
                            )
                            RecurringSchedulesSection(templates: templates, onManage: {})
                        }
                        .padding(.horizontal, Spacing.ml)
                        .padding(.top, Spacing.m)
                    }
                    .background(CleansiaColors.background)
                }
                .styled(language: language, theme: theme)
            }
        }
    }

    func testAuthScreens() async throws {
        for (screen, theme) in [("signin", "light"), ("signin", "dark"), ("signup", "light"), ("forgot", "light")] {
            try use("en")
            let model = authModel()
            await CustomerCapture.shoot(
                name(screen, "empty", Self.phone, "en", theme),
                size: Self.phone,
                to: self,
                hold: 0.3
            ) {
                VStack(spacing: 0) {
                    CleansiaColors.background.frame(height: 54)
                    switch screen {
                    case "signin":
                        SignInView(makeViewModel: { model }, onForgotPassword: {}, onSignUp: {}, onOutcome: { _ in })
                    case "signup":
                        SignUpView(makeViewModel: { model }, onSignIn: {}, onOutcome: { _ in })
                    default:
                        ForgotPasswordView(makeViewModel: { model }, onBack: {}, onOutcome: { _ in })
                    }
                }
                .frame(width: Self.phone.width, height: Self.phone.height)
                .background(CleansiaColors.background)
                .styled(language: "en", theme: theme)
            }
        }
    }

    func testOrderSheets() async throws {
        for (language, theme) in [("en", "light"), ("en", "dark"), ("uk", "light")] {
            try use(language)
            await CustomerCapture.shoot(
                name("cancel-sheet", "fee-unavailable", Self.phone, language, theme),
                size: Self.phone,
                to: self,
                hold: 0.3
            ) {
                presented {
                    CancelOrderSheet(
                        quote: .error(ApiError(httpStatus: 500)),
                        currencyCode: "CZK",
                        isSubmitting: false,
                        errorMessage: nil,
                        onReasonChanged: {},
                        onRetryQuote: {},
                        onConfirm: { _ in },
                        onDismiss: {}
                    )
                }
                .styled(language: language, theme: theme)
            }
            await CustomerCapture.shoot(
                name("review-sheet", "new", Self.phone, language, theme),
                size: Self.phone,
                to: self,
                hold: 0.3
            ) {
                presented {
                    SubmitReviewSheet(
                        existingReview: nil,
                        isSubmitting: false,
                        errorMessage: nil,
                        onConfirm: { _, _, _, _ in },
                        onDismiss: {}
                    )
                }
                .styled(language: language, theme: theme)
            }
        }
    }

    /// The text link stands in for the two buttons only when neither row of them fits: a narrow phone at a large,
    /// not yet accessibility, text size. The phone at the default size is the control. The small and the large phone
    /// carry the type scale in both themes.
    func testBookingSuccess() async throws {
        for (size, language, theme, text) in [
            (Self.narrow, "en", "light", DynamicTypeSize.xxxLarge), (Self.narrow, "uk", "light", .xxxLarge),
            (Self.narrow, "uk", "light", .large), (Self.phone, "en", "light", .large),
            (Self.small, "en", "light", .large), (Self.small, "en", "dark", .large),
            (Self.small, "uk", "light", .large), (Self.wide, "en", "light", .large),
            (Self.wide, "en", "dark", .large), (Self.wide, "uk", "light", .large)
        ] {
            try use(language)
            let order = CustomerCaptureFixture.order(status: 0)
            await CustomerCapture.shoot(
                name("booking-success", "ticket", size, language, theme, text: text),
                size: size,
                to: self,
                hold: 4
            ) {
                BookingSuccessView(
                    BookingSuccess(
                        orderId: "order-1",
                        confirmationCode: "130AA4",
                        cleaningAt: CustomerCaptureFixture.upcoming,
                        estimatedMinutes: 180,
                        street: "Zenklova 6",
                        city: "Praha 8"
                    ),
                    loadOrder: { _ in order },
                    onViewOrder: {},
                    onDone: {}
                )
                .frame(width: size.width, height: size.height)
                .background(CleansiaColors.background)
                .styled(language: language, theme: theme, text: text)
            }
        }
    }

    /// The dispute cards and one dispute's thread, each stamped with its date and time.
    func testDisputes() async throws {
        let opened = Date(timeIntervalSince1970: 1_762_340_400)
        let cards = [DisputeFixtures.entry(id: "d-1"), DisputeFixtures.entry(id: "d-2", statusValue: 3)]
        let thread = DisputeDetail(
            id: "d-1", displayOrderNumber: "1042", reasonValue: 1,
            description: "Cleaner skipped the kitchen entirely", statusValue: 1,
            createdOn: opened,
            messages: [DisputeMessage(
                id: "m-1", body: "We are looking into it.", isStaffMessage: true,
                createdOn: opened.addingTimeInterval(5400)
            )],
            evidence: [DisputeEvidence(
                id: "e-1", fileName: "receipt.pdf", blobURL: nil, uploadedOn: opened.addingTimeInterval(600)
            )]
        )
        for theme in ["light", "dark"] {
            try use("en")
            await CustomerCapture.shoot(
                name("disputes", "list", Self.phone, "en", theme),
                size: Self.phone,
                to: self
            ) {
                tabFrame(theme: theme) {
                    VStack(spacing: Spacing.s) {
                        ForEach(cards) { DisputeRowCard(dispute: $0) }
                        Spacer()
                    }
                    .padding(Spacing.m)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(CleansiaColors.background)
                }
                .styled(language: "en", theme: theme)
            }
            await CustomerCapture.shoot(
                name("dispute", "thread", Self.phone, "en", theme),
                size: Self.phone,
                to: self
            ) {
                tabFrame(theme: theme) {
                    DisputeThread(
                        detail: thread,
                        uploading: false,
                        onAddEvidence: {},
                        onImageTap: { _ in },
                        onPdfTap: { _ in },
                        onUnknownTap: { _ in }
                    )
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(CleansiaColors.background)
                }
                .styled(language: "en", theme: theme)
            }
        }
    }

    private func authModel() -> CustomerAuthViewModel {
        CustomerAuthViewModel(
            loginClient: FakeLoginClient(),
            registrationClient: FakeRegistrationClient(),
            emailConfirmationClient: FakeEmailConfirmationClient(),
            passwordResetClient: FakePasswordResetClient(),
            socialAuthClient: FakeSocialAuthClient(),
            socialProvider: FakeSocialSignInProvider(),
            settings: FakeAppSettingsStore(),
            snackbar: SnackbarController()
        )
    }

    /// A tab root as the shell shows it, under the status-bar and tab-bar stand-ins.
    private func tabFrame(
        theme: String,
        height: CGFloat = CustomerCoreCaptureTests.phone.height,
        @ViewBuilder content: () -> some View
    ) -> some View {
        VStack(spacing: 0) {
            (theme == "dark" ? CleansiaColors.background : CleansiaColors.surfaceVariant).frame(height: 54)
            content()
            CleansiaColors.surface
                .frame(height: 83)
                .overlay(alignment: .top) { Divider().overlay(CleansiaColors.outline) }
        }
        .frame(width: Self.phone.width, height: height)
    }

    /// A sheet at its large detent over the dimmed screen.
    private func presented(@ViewBuilder content: () -> some View) -> some View {
        ZStack(alignment: .bottom) {
            Color.black.opacity(0.4)
            content()
                .frame(width: Self.phone.width, height: Self.phone.height - 62)
                .background(CleansiaColors.surface)
                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
        .frame(width: Self.phone.width, height: Self.phone.height)
        .background(CleansiaColors.background)
    }
}

private extension View {
    func styled(language: String, theme: String, text: DynamicTypeSize = .large) -> some View {
        tint(CleansiaColors.primaryText)
            .environment(\.locale, Locale(identifier: language))
            .preferredColorScheme(theme == "dark" ? .dark : .light)
            .dynamicTypeSize(text)
    }
}

/// The notifications inbox the Home bell opens: a page of rows (two unread, one more than a week old), the empty inbox
/// and a failed load; and Home's top bar with the bell over three.
extension CustomerCoreCaptureTests {
    func testNotificationsInbox() async throws {
        let now = Date()
        let rows = [
            UserNotification(
                id: "n-1", eventKey: "order.cleaner_assigned", args: ["orderId": "order-1", "orderNumber": "A-1042"],
                createdOn: now.addingTimeInterval(-600), readOn: nil
            ),
            UserNotification(
                id: "n-2", eventKey: "dispute.reply", args: ["disputeId": "d-1"],
                createdOn: now.addingTimeInterval(-3 * 3600), readOn: nil
            ),
            UserNotification(
                id: "n-3", eventKey: "order.completed", args: ["orderId": "order-2", "orderNumber": "A-1041"],
                createdOn: now.addingTimeInterval(-26 * 3600), readOn: now
            ),
            UserNotification(
                id: "n-4", eventKey: "loyalty.tier_upgrade", args: ["tier": "SilverMopper"],
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
                try use("en")
                let client = FakeNotificationFeedClient()
                client.pageResults = [page]
                await CustomerCapture.shoot(
                    name("inbox", state, Self.phone, "en", theme),
                    size: Self.phone,
                    to: self,
                    hold: 0.3
                ) {
                    presented {
                        NotificationsInboxSheet(
                            client: client,
                            badge: NotificationBadgeModel(client: client, feedEventKeys: CustomerFeedEventKeys.all),
                            snackbar: SnackbarController(),
                            onDestination: { _ in }
                        )
                    }
                    .styled(language: "en", theme: theme)
                }
            }
        }
    }

    /// Home holds its skeleton until its sources land or 1.5 s pass, so the frame waits past both; the top bar is
    /// also cut out on its own, clear of the carousel's timing.
    func testHomeBell() async throws {
        for theme in ["light", "dark"] {
            try use("en")
            let feed = FakeNotificationFeedClient()
            feed.unreadCountResult = .success(3)
            let markets = await MarketFixtures.resolved()
            let booking = BookingViewModel(
                catalogClient: FakeCatalogClient(),
                market: markets.statePublisher,
                savedCardClient: FakeSavedCardClient()
            )
            await CustomerCapture.shoot(
                name("home", "bell", Self.phone, "en", theme),
                size: Self.phone,
                to: self,
                hold: 2.5,
                crops: [("-topbar", CGRect(x: 0, y: 54, width: Self.phone.width, height: 72))]
            ) {
                tabFrame(theme: theme) {
                    HomeTab(
                        orderRepository: OrderRepository(client: FakeOrderClient()),
                        recurringRepository: RecurringBookingRepository(client: FakeRecurringBookingClient()),
                        loyaltyRepository: LoyaltyRepository(client: FakeLoyaltyClient()),
                        membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
                        savedAddressRepository: SavedAddressRepository(client: FakeSavedAddressClient()),
                        referralRepository: RewardsReferralRepository(client: FakeRewardsReferralClient()),
                        marketStore: markets,
                        notificationBadge: NotificationBadgeModel(
                            client: feed,
                            feedEventKeys: CustomerFeedEventKeys.all
                        ),
                        notificationFeedClient: feed,
                        bookingVM: booking,
                        snackbar: SnackbarController(),
                        onBookCleaning: {},
                        onOpenAddressManager: {},
                        onOpenMarket: {},
                        onOrderClick: { _ in },
                        onSeeAllOrders: {},
                        onSubscribePlus: {},
                        onOpenReferral: {},
                        onBookPackage: { _ in },
                        onBookSize: { _, _ in },
                        onRebookOrder: { _ in },
                        onSetupRecurring: {},
                        onManageRecurring: {},
                        onNotificationDestination: { _ in }
                    )
                }
                .styled(language: "en", theme: theme)
            }
        }
    }
}
