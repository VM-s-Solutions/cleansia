import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import WebKit
import XCTest
@testable import CleansiaPartner

/// Native captures of the Partner Orders tabs and the take sheet, from the design's own sample data (Thursday
/// 12 November 2026, 09:20), never the app against an API. Each attachment is named as the design's frame:
/// `<screen>-<state>-ios-<w>x<h>-<lang>-<theme>[-ax1][-full]`; the lane exports them from the result bundle.
@MainActor
final class PartnerOrdersVisualTests: XCTestCase {
    let phone = CGSize(width: 393, height: 852)
    let small = CGSize(width: 375, height: 667)
    var mapSettle: Double = 0
    var detailTake: TakenJob?
    private var restoreBundle: Bundle!
    private var restoreTag: String!

    /// The captures are evidence, not proofs, so they run only when asked for (`TEST_RUNNER_PARTNER_ORDERS_CAPTURE=1`);
    /// the contract-page check always runs.
    override func setUpWithError() throws {
        try super.setUpWithError()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
        let asked = ProcessInfo.processInfo.environment["PARTNER_ORDERS_CAPTURE"] == "1"
        try XCTSkipUnless(asked || name.contains("testTheContractScrollsWithThePage"), "captures run on request")
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    // MARK: - Available

    func testAvailable() async throws {
        let populated = Board(available: OrdersDesignFixture.availableWithDevRows())
        for (language, theme, large) in [
            ("en", "light", false), ("en", "dark", false), ("cs", "light", false), ("uk", "light", false),
            ("en", "light", true), ("cs", "light", true), ("uk", "light", true)
        ] {
            try await shoot("available", "populated", language, theme, large: large, board: populated)
        }
        try await shoot("available", "populated", "en", "light", board: populated, full: true)
        try await shoot("available", "populated", "en", "light", large: true, board: populated, full: true)
        try await shoot("available", "populated", "en", "light", size: small, board: populated)
        for (theme, large) in [("light", false), ("dark", false), ("light", true), ("dark", true)] {
            try await shoot("available", "nofinding", "en", theme, large: large, board: Board(active: []))
        }
        for theme in ["light", "dark"] {
            try await shoot("available", "empty", "en", theme, board: Board(available: []))
            try await shoot("available", "empty-nocap", "en", theme, board: Board(available: [], cash: .noCap))
            try await shoot("available", "empty-cash", "en", theme, board: Board(available: [], cash: .over))
            try await shoot("available", "cash", "en", theme, board: Board(cash: .over))
            try await shoot("available", "error", "en", theme, board: Board(failAvailable: true))
            try await shoot("available", "stale", "en", theme, board: Board(stale: true))
            let page = OrdersDesignFixture.available() + OrdersDesignFixture.more()
            for (state, fails) in [("more", false), ("more-failed", true)] {
                let board = Board(available: page, availableTotal: 137, moreFails: fails)
                try await shoot("available", state, "en", theme, board: board, end: true)
            }
            try await shoot("available", "loading", "en", theme, board: Board(loading: true))
        }
        try await shoot("available", "empty", "uk", "light", board: Board(available: []))
        try await shoot("available", "empty", "en", "light", board: Board(available: []), full: true)
    }

    // MARK: - Active

    func testActive() async throws {
        for (language, theme, large) in [
            ("en", "light", false), ("en", "dark", false), ("cs", "light", false), ("uk", "light", false),
            ("en", "light", true), ("uk", "light", true)
        ] {
            try await shoot("active", "next", language, theme, large: large, board: Board(tab: .active))
        }
        try await shoot("active", "next", "en", "light", board: Board(tab: .active), full: true)
        try await shoot("active", "next", "en", "light", size: small, board: Board(tab: .active))
        let onWay = Board(
            tab: .active,
            now: OrdersDesignFixture.at(12, 13, 40),
            active: OrdersDesignFixture.active(heroStatus: ._3)
        )
        let one = Board(
            tab: .active,
            now: OrdersDesignFixture.at(12, 14, 30),
            available: OrdersDesignFixture.available().filter { $0.id != "V1" },
            active: [OrdersDesignFixture.active(heroStatus: ._4)[0]]
        )
        for theme in ["light", "dark"] {
            try await shoot("active", "onway", "en", theme, board: onWay)
            try await shoot("active", "one", "en", theme, board: one)
            try await shoot("active", "empty", "en", theme, board: Board(tab: .active, active: []))
            try await shoot("active", "loading", "en", theme, board: Board(tab: .active, loading: true))
            try await shoot("active", "error", "en", theme, board: Board(tab: .active, failActive: true))
            try await shoot(
                "active",
                "error-cash",
                "en",
                theme,
                board: Board(tab: .active, cash: .over, failActive: true)
            )
        }
        for language in ["cs", "uk"] {
            try await shoot("active", "one", language, "light", board: one)
        }
        try await shoot("active", "one", "en", "light", board: one, full: true)
        let empty = Board(tab: .active, active: [])
        for (language, size, type) in [
            ("uk", phone, DynamicTypeSize.large), ("uk", small, .large), ("ru", phone, .large), ("ru", small, .large),
            ("ru", small, .xLarge), ("uk", phone, .xxxLarge), ("ru", small, .xxxLarge), ("cs", phone, .accessibility1),
            ("uk", phone, .accessibility1), ("uk", phone, .accessibility3), ("uk", phone, .accessibility5)
        ] {
            let tall = type >= .accessibility3
            try await shoot("active", "empty", language, "light", typeSize: type, size: size, board: empty, full: tall)
        }
    }

    // MARK: - History

    func testHistory() async throws {
        for (language, theme) in [("en", "light"), ("en", "dark"), ("cs", "light"), ("uk", "light")] {
            try await shoot("history", "november", language, theme, board: Board(tab: .history))
        }
        try await shoot("history", "november", "en", "light", board: Board(tab: .history), full: true)
        let first = OrdersDesignFixture.at(1, 9, 20)
        for theme in ["light", "dark"] {
            try await shoot("history", "week", "en", theme, board: Board(tab: .history, period: .thisWeek))
            try await shoot("history", "october", "en", theme, board: Board(tab: .history, period: .lastMonth))
            try await shoot("history", "empty", "en", theme, board: Board(tab: .history, now: first, history: []))
            try await shoot("history", "error", "en", theme, board: Board(tab: .history, failHistory: true))
            try await shoot("history", "loading", "en", theme, board: Board(tab: .history, loadingHistory: true))
        }
        try await shoot("history", "empty", "uk", "light", board: Board(tab: .history, now: first, history: []))
        try await shoot(
            "history",
            "empty",
            "en",
            "light",
            board: Board(tab: .history, now: first, history: []),
            full: true
        )
    }

    // MARK: - Take sheet

    func testTakeSheet() async throws {
        let row = OrdersDesignFixture.available()[1]
        let contract = OrdersDesignFixture.contract(for: row)
        let refusal = ApiError(code: "order.no_available_spots", httpStatus: 400)
        for (language, theme) in [("en", "light"), ("en", "dark"), ("uk", "light")] {
            try await sheet("ready", language, theme, state: .loaded(contract), row: row)
            try await sheet("taken", language, theme, state: .taken(contract), row: row)
        }
        try await sheet("ready", "en", "light", state: .loaded(contract), row: row, full: true)
        try await sheet("ready", "en", "light", state: .loaded(contract), row: row, size: small)
        try await sheet("ready", "en", "light", state: .loaded(contract), row: row, end: true)
        try await sheet("ready", "en", "light", state: .loaded(contract), row: row, size: small, end: true)
        try await sheet("taken", "en", "light", state: .taken(contract), row: row, size: small)
        try await sheet("refused", "en", "light", state: .refused(contract, refusal), row: row)
    }

    /// One scroll: the contract is as tall as its text and scrolls with the page, never inside it, the page ends where
    /// the contract does, and the swipe stays pinned on screen, on a 375 × 667 phone and at AX3, in both modes.
    func testTheContractScrollsWithThePageAndTheSwipeStaysOnScreen() async throws {
        try use("en")
        let cash = OrdersDesignFixture.available()[1]
        for (size, large) in [(phone, false), (small, false), (phone, true), (small, true)] {
            try await contractPage(
                .take(orderId: "order-1"),
                board: BoardTake(snapshot: cash),
                size: size,
                large: large
            )
        }
        try await contractPage(.read(acceptanceId: "acceptance-1"), board: nil, size: phone, large: true)
    }

    private struct ContractPage {
        let scrollsItself: Bool
        let text: CGRect
        let documentHeight: CGFloat
        let page: CGRect
        let textEndToPageEnd: CGFloat
    }

    private func contractPage(
        _ request: WorkContractRequest,
        board: BoardTake?,
        size: CGSize,
        large: Bool
    ) async throws {
        var measured: ContractPage?
        await hosted(size: size, content: {
            sheetHost(
                request,
                state: .loaded(OrdersDesignFixture.contract(for: OrdersDesignFixture.available()[1])),
                board: board
            )
            .frame(width: size.width, height: size.height)
            .styled(language: "en", scheme: "light", large: large ? .accessibility3 : nil)
        }, inspect: { window in
            guard let web = Self.webView(in: window), let page = Self.pageScroll(around: web) else { return }
            let end = page.contentSize.height + page.adjustedContentInset.bottom - page.bounds.height
            page.setContentOffset(CGPoint(x: 0, y: max(end, -page.adjustedContentInset.top)), animated: false)
            window.layoutIfNeeded()
            let text = web.convert(web.bounds, to: window)
            let visible = page.convert(page.bounds, to: window)
            measured = ContractPage(
                scrollsItself: web.scrollView.isScrollEnabled,
                text: text,
                documentHeight: web.scrollView.contentSize.height,
                page: visible,
                textEndToPageEnd: visible.maxY - text.maxY
            )
        })
        let found = try XCTUnwrap(measured, "no contract text at \(size), large: \(large)")
        let label = "\(request) at \(size), large: \(large)"
        XCTAssertFalse(found.scrollsItself, "the contract scrolls inside the page: \(label)")
        XCTAssertGreaterThan(found.text.height, 100, label)
        XCTAssertEqual(found.text.height, found.documentHeight, accuracy: 1, "cut or padded: \(label)")
        XCTAssertEqual(found.textEndToPageEnd, 0, accuracy: 1, "space of the page's own under the contract: \(label)")
        XCTAssertGreaterThanOrEqual(found.page.height, 160, label)
        XCTAssertLessThanOrEqual(found.page.maxY + 52, size.height, "the contract pushed the swipe away: \(label)")
    }

    // MARK: - The board's fixture

    struct Board {
        var tab: OrdersTab = .available
        var now = OrdersDesignFixture.now
        var available: [OrderListItem]?
        var availableTotal: Int?
        var active: [OrderListItem]?
        var history: [OrderListItem]?
        var cash: OrdersDesignFixture.Cash = .held
        var period: CompletedPeriod = .thisMonth
        var failAvailable = false
        var failActive = false
        var failHistory = false
        var stale = false
        var moreFails = false
        var loading = false
        var loadingHistory = false
    }

    var client: FakePartnerOrderClient!

    func use(_ language: String) throws {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
        CoreL10n.apply(languageTag: language)
    }

    // swiftlint:disable:next function_body_length
    func viewModel(_ board: Board, language: String) async throws -> OrdersListViewModel {
        try use(language)
        let rows = (board.available ?? OrdersDesignFixture.available())
            .filter { board.cash != .over || $0.paymentType?.value != 1 }
        let own = board.active ?? OrdersDesignFixture.active()
        let done = board.history ?? OrdersDesignFixture.history(board.period)
        client = FakePartnerOrderClient()
        let offer = OrdersDesignFixture.offer
        let holds = rows.contains { $0.id == offer.id } && board.now < (offer.respondByUtc ?? .distantPast)
        client.pendingOffersResult = .success(holds ? [offer] : [])
        var availableFails = board.failAvailable
        client.pageResponder = { query in
            if query.hasAvailableSpots == true {
                if availableFails { return .failure(ApiError(code: "network.unreachable")) }
                return .success(OrderPage(rows: rows, total: board.availableTotal ?? rows.count))
            }
            if query.statuses == [._5] {
                if board.failHistory { return .failure(ApiError(code: "network.unreachable")) }
                let rows = query.cleaningDateTo
                    .map { $0 <= OrdersDesignFixture.at(1, 0, 0) } == true ? OrdersDesignFixture.october : done
                return .success(OrderPage(rows: rows, total: rows.count))
            }
            return board.failActive
                ? .failure(ApiError(code: "network.unreachable"))
                : .success(OrderPage(rows: own, total: own.count))
        }
        let payroll = FakePayrollClient()
        payroll.cashHeldResult = .success([OrdersDesignFixture.cash(board.cash)])
        let staleness = OrdersStaleness()
        var clock = board.stale ? OrdersDesignFixture.at(12, 9, 14) : board.now
        let vm = OrdersListViewModel(
            client: client,
            staleness: staleness,
            snackbar: SnackbarController(),
            payroll: payroll,
            pendingOffers: PendingOffersStore(client: client, ordersStaleness: staleness),
            boardStore: OrdersBoardStore(),
            now: { clock }
        )
        if board.loading {
            client.suspendGetPaged = true
            Task { await vm.selectTab(board.tab) }
            await Task.yield()
            return vm
        }
        await vm.onAppear()
        if board.period != .thisMonth { await vm.setCompletedPeriod(board.period) }
        if board.loadingHistory {
            client.suspendGetPaged = true
            Task { await vm.selectTab(board.tab) }
            await Task.yield()
            return vm
        }
        if board.tab != .available { await vm.selectTab(board.tab) }
        if board.stale {
            clock = board.now
            availableFails = true
            await vm.userRefresh()
        }
        if board.availableTotal != nil, board.moreFails {
            client.pageResponder = { _ in .failure(ApiError(code: "network.unreachable")) }
            await vm.loadMore()
        }
        return vm
    }

    // MARK: - Capture

    func shoot(
        _ screen: String,
        _ state: String,
        _ language: String,
        _ theme: String,
        large: Bool = false,
        typeSize: DynamicTypeSize? = nil,
        size: CGSize? = nil,
        board: Board,
        full: Bool = false,
        fullHeight: CGFloat? = nil,
        end: Bool = false
    ) async throws {
        let vm = try await viewModel(board, language: language)
        let canvas = size ?? phone
        let type = typeSize ?? (large ? .accessibility1 : nil)
        let height = full ? fullHeight ?? (type != nil ? 4400 : 2600) : canvas.height
        let name = label(screen, state, language, theme, type, canvas, full: full)
        await capture(name: name, size: CGSize(width: canvas.width, height: height), end: end) {
            boardFrame(vm, canvas: canvas, height: height, theme: theme)
                .styled(language: language, scheme: theme, large: type)
        }
        client.resumeGetPaged()
    }

    // swiftlint:disable:next function_parameter_count
    private func label(
        _ screen: String,
        _ state: String,
        _ language: String,
        _ theme: String,
        _ type: DynamicTypeSize?,
        _ size: CGSize,
        full: Bool
    ) -> String {
        let names = ["-xs", "-s", "-m", "", "-xl", "-xxl", "-xxxl", "-ax1", "-ax2", "-ax3", "-ax4", "-ax5"]
        let step = type.flatMap { DynamicTypeSize.allCases.firstIndex(of: $0) }.map { names[$0] } ?? ""
        return "\(screen)-\(state)-ios-\(Int(size.width))x\(Int(size.height))-\(language)-\(theme)"
            + step + (full ? "-full" : "")
    }

    private func capture(
        name: String,
        size: CGSize,
        end: Bool = false,
        @ViewBuilder content: () -> some View
    ) async {
        await hosted(size: size, end: end, content: content) { window in
            CaptureProbe.attach(window, name: name, to: self)
        }
    }

    /// Lays the view out in a window of `size` and hands the window over once the layout has settled.
    private func hosted(
        size: CGSize,
        end: Bool = false,
        @ViewBuilder content: () -> some View,
        inspect: (UIWindow) -> Void
    ) async {
        let previous = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        let host = UIHostingController(rootView: content())
        if #available(iOS 16.4, *) { host.safeAreaRegions = [] }
        let window = previous?.windowScene.map(UIWindow.init(windowScene:)) ?? UIWindow(frame: CGRect(
            origin: .zero,
            size: size
        ))
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
        CaptureProbe.deviceInsets(host, in: window, homeIndicator: size.height == small.height ? 0 : 34)
        defer {
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        for _ in 0 ..< 14 + Int(mapSettle / 0.06) {
            try? await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
        let fitted = Date().addingTimeInterval(4)
        while !Self.webViewsFitTheirText(in: host.view), Date() < fitted {
            try? await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
        if end {
            CaptureProbe.scrollToEnd(in: host.view)
            for _ in 0 ..< 6 {
                try? await Task.sleep(nanoseconds: 60_000_000)
                host.view.layoutIfNeeded()
            }
        }
        inspect(window)
    }

    private static func webView(in view: UIView) -> WKWebView? {
        if let web = view as? WKWebView { return web }
        return view.subviews.lazy.compactMap(webView(in:)).first
    }

    private static func pageScroll(around view: UIView) -> UIScrollView? {
        var ancestor = view.superview
        while let current = ancestor {
            if let scroll = current as? UIScrollView { return scroll }
            ancestor = current.superview
        }
        return nil
    }

    /// A text sized to its document has loaded and been laid out at its height; one that scrolls itself is ready.
    private static func webViewsFitTheirText(in view: UIView) -> Bool {
        guard let web = webView(in: view), !web.scrollView.isScrollEnabled else { return true }
        return web.scrollView.contentSize.height > 1
            && abs(web.bounds.height - web.scrollView.contentSize.height) <= 1
    }
}

extension PartnerOrdersVisualTests {
    /// The board as the tab shows it, under the status-bar and tab-bar stand-ins.
    func boardFrame(_ vm: OrdersListViewModel, canvas: CGSize, height: CGFloat, theme _: String) -> some View {
        VStack(spacing: 0) {
            Rectangle()
                .fill(CleansiaColors.background)
                .frame(height: canvas.height == small.height ? 20 : 54)
            OrdersListView(vm: vm)
            Rectangle()
                .fill(CleansiaColors.surface)
                .frame(height: canvas.height == small.height ? 50 : 83)
                .overlay(alignment: .top) { Divider().overlay(CleansiaColors.outline) }
        }
        .frame(width: canvas.width, height: height)
    }

    /// The sheet as presented: at the bottom, over the dimmed board, as tall as its detent. The large detent leaves
    /// 62 pt above it; the taken sheet is as tall as its content reports, plus the home-indicator inset the system adds
    /// under a custom detent. The grabber, the dimming and the 10-pt corners stand in for the system's presentation. An
    /// `end` frame is the sheet scrolled to its end, over the dimming alone, the board being the longer scroll.
    func sheet(
        _ state: String,
        _ language: String,
        _ theme: String,
        state sheetState: WorkContractSheetState,
        row: OrderListItem,
        size: CGSize? = nil,
        full: Bool = false,
        end: Bool = false
    ) async throws {
        try use(language)
        let canvas = size ?? phone
        let board = sheetState.isTaken ? OrdersDesignFixture.taken(row) : BoardTake(snapshot: row)
        let name = label("take", state, language, theme, nil, canvas, full: full)
        if end {
            return await capture(name: name + "-end", size: canvas, end: true) {
                ZStack(alignment: .bottom) {
                    Color.black.opacity(0.4)
                    sheetHost(.take(orderId: "order-1"), state: sheetState, board: board)
                        .frame(width: canvas.width, height: canvas.height - 62)
                        .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
                .frame(width: canvas.width, height: canvas.height)
                .styled(language: language, scheme: theme, large: nil)
            }
        }
        if full {
            return await capture(name: name, size: CGSize(width: canvas.width, height: 2000)) {
                sheetHost(.take(orderId: "order-1"), state: sheetState, board: board)
                    .frame(width: canvas.width, height: 2000)
                    .styled(language: language, scheme: theme, large: nil)
            }
        }
        var hug: CGFloat = 0
        await hosted(size: canvas, content: {
            sheetHost(.take(orderId: "order-1"), state: sheetState, board: board) { hug = $0 }
                .frame(width: canvas.width, height: canvas.height)
                .styled(language: language, scheme: theme, large: nil)
        }, inspect: { _ in })
        let height = sheetState.isTaken ? hug + (canvas.height == small.height ? 0 : 34) : canvas.height - 62
        let behind = try await viewModel(Board(), language: language)
        await capture(name: name, size: canvas) {
            ZStack(alignment: .bottom) {
                boardFrame(behind, canvas: canvas, height: canvas.height, theme: theme)
                Color.black.opacity(0.4)
                sheetHost(.take(orderId: "order-1"), state: sheetState, board: board)
                    .frame(width: canvas.width, height: height)
                    .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                    .overlay(alignment: .top) {
                        Capsule().fill(Color(uiColor: .systemGray3)).frame(width: 36, height: 5).padding(.top, 5)
                    }
            }
            .frame(width: canvas.width, height: canvas.height)
            .styled(language: language, scheme: theme, large: nil)
        }
        client.resumeGetPaged()
    }

    func sheetHost(
        _ request: WorkContractRequest,
        state: WorkContractSheetState,
        board: BoardTake?,
        onTakenHeight: @escaping (CGFloat) -> Void = { _ in }
    ) -> some View {
        WorkContractSheetContent(
            request: request,
            state: state,
            board: board,
            taken: detailTake,
            onTakenHeight: onTakenHeight,
            now: OrdersDesignFixture.now
        )
    }

    /// The generated page is the design's: its data.js makes 91 rows, V10 first and V100 last, 76 013.29 Kč in all.
    func testTheGeneratedPageIsTheDesigns() {
        let more = OrdersDesignFixture.more()
        XCTAssertEqual(more.count, 91)
        XCTAssertEqual(more.prefix(4).map(\.id), ["V10", "V13", "V11", "V12"])
        XCTAssertEqual(more.first?.estimatedCleanerPay, 750)
        XCTAssertEqual(more.last?.id, "V100")
        XCTAssertEqual(more.last?.amountDueOnCard, 1390)
        XCTAssertEqual(more.compactMap(\.estimatedCleanerPay).reduce(0) { $0 + ($1 * 100).rounded() }, 7_601_329)
        XCTAssertEqual(more.filter { $0.amountDueOnCard != nil }.count, 10)
        XCTAssertEqual(more.filter { $0.homeDistanceBand == nil }.count, 8)
        XCTAssertEqual(more.filter { $0.dirtinessLevel != nil }.count, 11)
    }

    /// Three frames each with one known defect (a truncated scope, a one-word last line, a 60-pt blank band in a
    /// panel), so the checks run over the captures are shown to catch what they look for. The system's line breaker
    /// keeps an English last word company; the Czech removal reason at its well's width does end on one word.
    func testCheckerControls() async {
        let panel = { (content: AnyView) in
            content
                .padding(16)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: 24))
                .padding(16)
                .frame(width: 393, height: 300, alignment: .top)
                .background(CleansiaColors.surfaceVariant)
        }
        let scope = "Standard clean · 3 rooms · 1 bath · + interior windows"
        let controls: [(String, AnyView)] = [
            ("control-truncated", AnyView(Text(scope).cleansiaFont(.nunito(.regular, size: 15)).lineLimit(1)
                    .frame(width: 200, alignment: .leading))),
            ("control-orphan", AnyView(Text(DetailJob.removedReason).cleansiaFont(.nunito(.regular, size: 16))
                    .frame(width: 301, alignment: .leading))),
            ("control-band", AnyView(VStack(alignment: .leading, spacing: 0) {
                Text("Cash you hold").cleansiaFont(.nunito(.bold, size: 16))
                Color.clear.frame(height: 60)
                Text("1 240 Kč").cleansiaFont(.nunito(.bold, size: 20))
            }))
        ]
        for (name, content) in controls {
            await capture(name: name, size: CGSize(width: 393, height: 300)) {
                panel(content).styled(language: "en", scheme: "light", large: nil)
            }
        }
    }
}

private extension WorkContractSheetState {
    var isTaken: Bool {
        if case .taken = self { return true }
        return false
    }
}

private extension View {
    func styled(language: String, scheme: String, large: DynamicTypeSize?) -> some View {
        environment(\.locale, Locale(identifier: language))
            .preferredColorScheme(scheme == "dark" ? .dark : .light)
            .dynamicTypeSize(large ?? .large)
    }
}
