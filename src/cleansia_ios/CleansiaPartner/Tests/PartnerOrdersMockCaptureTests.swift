import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// The design's iOS frames as native captures, each also in the theme the design did not draw, Ukrainian at the
/// small width, and the design's three spec sheets drawn with the components they show. They run on request, as the
/// rest of the harness; the swipe-back host runs only when it is given a file to wait for.
@MainActor
enum MockCapture {
    enum Scroll {
        case rest
        case end
        case offset(CGFloat)
        /// Until the text starting with this sits on a toolbar whose top is 8 under the pinned header.
        case toolbar(String)
    }

    static func use(_ language: String, near test: AnyClass) throws {
        let path = [Bundle.main, Bundle(for: test)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
        CoreL10n.apply(languageTag: language)
    }

    // swiftlint:disable:next function_parameter_count
    static func shoot(
        _ test: XCTestCase,
        name: String,
        size: CGSize,
        statusBar: CGFloat?,
        homeIndicator: CGFloat,
        scroll: Scroll,
        settle seconds: Double = 0,
        @ViewBuilder content: () -> some View
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
        CaptureProbe.deviceInsets(host, in: window, homeIndicator: homeIndicator)
        if let statusBar { host.additionalSafeAreaInsets.top += statusBar }
        defer {
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        await settle(host, rounds: 16 + Int(seconds / 0.06))
        if case .rest = scroll {} else {
            await settleContent(host, in: window)
        }
        if let moved = move(scroll, in: window) {
            print("[capture] \(name) scrolled to \(moved)")
            await settle(host, rounds: 8)
        }
        CaptureProbe.attach(window, name: name, to: test)
    }

    private static func settle(_ host: UIViewController, rounds: Int) async {
        for _ in 0 ..< rounds {
            try? await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
    }

    /// Until the longest scroll's content has kept its height for five rounds (a contract's text loads late), at
    /// most six seconds.
    private static func settleContent(_ host: UIViewController, in window: UIWindow) async {
        var last: CGFloat = -1, steady = 0
        for _ in 0 ..< 50 where steady < 5 {
            try? await Task.sleep(nanoseconds: 120_000_000)
            host.view.layoutIfNeeded()
            let height = longest(in: window)?.contentSize.height ?? 0
            steady = height == last ? steady + 1 : 0
            last = height
        }
    }

    private static func longest(in window: UIWindow) -> UIScrollView? {
        func scrollViews(_ view: UIView) -> [UIScrollView] {
            ((view as? UIScrollView).map { [$0] } ?? []) + view.subviews.flatMap(scrollViews)
        }
        return scrollViews(window).max(by: {
            $0.contentSize.height - $0.bounds.height < $1.contentSize.height - $1.bounds.height
        })
    }

    private static func move(_ scroll: Scroll, in window: UIWindow) -> CGFloat? {
        guard let longest = longest(in: window) else { return nil }
        let top = -longest.adjustedContentInset.top
        let end = longest.contentSize.height + longest.adjustedContentInset.bottom - longest.bounds.height
        let target: CGFloat
        switch scroll {
        case .rest:
            return nil
        case .end:
            target = end
        case let .offset(distance):
            target = min(longest.contentOffset.y + distance, end)
        case let .toolbar(start):
            let plain = { (text: String) in
                text.replacingOccurrences(of: "\u{00A0}", with: " ").replacingOccurrences(of: "\u{202F}", with: " ")
            }
            let wanted = String(plain(start).prefix(14))
            guard !wanted.isEmpty, let finding = CaptureRenderTree.texts(in: window)
                .first(where: { plain($0.text).hasPrefix(wanted) })?.frame else { return nil }
            let toolbarTop = finding.minY - Spacing.xxs - max(0, (24 - finding.height) / 2)
            let headerFoot = longest.convert(longest.bounds, to: window).minY
            target = min(longest.contentOffset.y + toolbarTop - (headerFoot + 8), end)
        }
        longest.setContentOffset(CGPoint(x: 0, y: max(target, top)), animated: false)
        window.layoutIfNeeded()
        return longest.contentOffset.y - top
    }
}

extension PartnerOrdersVisualTests {
    func testMockTabs() async throws {
        let populated = Board(available: OrdersDesignFixture.availableWithDevRows())
        let row = OrdersDesignFixture.available()[1]
        let contract = OrdersDesignFixture.contract(for: row)
        for theme in ["light", "dark"] {
            try await shoot("available", "populated", "en", theme, board: populated)
            try await shoot("available", "populated", "en", theme, size: small, board: populated)
            try await shoot("available", "populated", "en", theme, large: true, board: populated)
            try await shoot(
                "available",
                "populated",
                "en",
                theme,
                large: true,
                board: populated,
                full: true,
                fullHeight: 5200
            )
            try await shoot("available", "populated", "en", theme, board: populated, full: true, fullHeight: 3000)
            try await scrolledBoard(theme, board: populated)
            try await shoot("active", "next", "en", theme, board: Board(tab: .active))
            try await shoot("active", "next", "en", theme, large: true, board: Board(tab: .active))
            try await shoot(
                "active",
                "next",
                "en",
                theme,
                large: true,
                board: Board(tab: .active),
                full: true,
                fullHeight: 2600
            )
            try await shoot("history", "november", "en", theme, board: Board(tab: .history))
            try await sheet("ready", "en", theme, state: .loaded(contract), row: row)
            await sheetEnd(theme, contract: contract, row: row)
        }
        try await shoot("available", "populated", "uk", "light", size: small, board: populated)
        try await sheet("ready", "uk", "light", state: .loaded(contract), row: row, size: small)
    }

    /// The open rows with their chevron column, the Job taken panel with its map strip, and Add: the camera alone.
    func testMockRowsTakenAndPhotoAdd() async throws {
        let populated = Board(available: OrdersDesignFixture.availableWithDevRows())
        let row = OrdersDesignFixture.available()[1]
        let contract = OrdersDesignFixture.contract(for: row)
        for theme in ["light", "dark"] {
            try await shoot("available", "populated", "en", theme, board: populated)
            mapSettle = 5
            try await sheet("taken", "en", theme, state: .taken(contract), row: row)
            mapSettle = 0
            try use("en")
            await MockCapture.shoot(
                self,
                name: "photo-add-ios-393x852-en-\(theme)",
                size: phone,
                statusBar: 54,
                homeIndicator: 34,
                scroll: .rest,
                settle: 1.5
            ) {
                PhotoAddHost()
                    .frame(width: phone.width, height: phone.height)
                    .environment(\.locale, Locale(identifier: "en"))
                    .preferredColorScheme(theme == "dark" ? .dark : .light)
                    .dynamicTypeSize(.large)
            }
        }
    }

    /// The take sheet scrolled to its end, over the dimming alone, once the contract's text has loaded.
    private func sheetEnd(_ theme: String, contract: WorkContract, row: OrderListItem) async {
        try? use("en")
        await MockCapture.shoot(
            self,
            name: "take-ready-ios-393x852-en-\(theme)-end",
            size: phone,
            statusBar: nil,
            homeIndicator: 34,
            scroll: .end
        ) {
            ZStack(alignment: .bottom) {
                Color.black.opacity(0.4)
                sheetHost(.take(orderId: "order-1"), state: .loaded(contract), board: BoardTake(snapshot: row))
                    .frame(width: phone.width, height: phone.height - 62)
                    .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
            }
            .frame(width: phone.width, height: phone.height)
            .environment(\.locale, Locale(identifier: "en"))
            .preferredColorScheme(theme == "dark" ? .dark : .light)
            .dynamicTypeSize(.large)
        }
    }

    private func scrolledBoard(_ theme: String, board: Board) async throws {
        let vm = try await viewModel(board, language: "en")
        let finding = vm.overlapFinding ?? ""
        await MockCapture.shoot(
            self,
            name: "available-populated-ios-393x852-en-\(theme)-scrolled",
            size: phone,
            statusBar: nil,
            homeIndicator: 34,
            scroll: finding.isEmpty ? .offset(222) : .toolbar(finding)
        ) {
            boardFrame(vm, canvas: phone, height: phone.height, theme: theme)
                .environment(\.locale, Locale(identifier: "en"))
                .preferredColorScheme(theme == "dark" ? .dark : .light)
                .dynamicTypeSize(.large)
        }
        client.resumeGetPaged()
    }

    func testMockSpecSheets() async throws {
        try use("en")
        let rows = OrdersDesignFixture.availableWithDevRows()
        let sheets: [(String, AnyView)] = [
            ("compare-leaf-ios-393x852", AnyView(LeafSheet())),
            ("compare-tile-ios-393x852", AnyView(TileSheet(rows: rows))),
            ("slide-options-ios-393x852", AnyView(SlideSheet()))
        ]
        for (name, sheet) in sheets {
            let height: CGFloat = name.hasPrefix("compare-leaf") ? 1010 : 852
            await MockCapture.shoot(
                self,
                name: name,
                size: CGSize(width: 393, height: height),
                statusBar: 0,
                homeIndicator: 34,
                scroll: .rest
            ) {
                sheet
                    .frame(width: 393, height: height, alignment: .top)
                    .environment(\.locale, Locale(identifier: "en"))
                    .preferredColorScheme(.light)
                    .dynamicTypeSize(.large)
            }
        }
    }

    /// The real Orders stack over the design's fake client, full screen and key, for an outside driver (AXe) to tap
    /// and swipe. It reports the stack's depth whenever it changes and ends once `PARTNER_ORDERS_SWIPE_DONE` exists.
    func testSwipeBackHost() async throws {
        let done = ProcessInfo.processInfo.environment["PARTNER_ORDERS_SWIPE_DONE"] ?? ""
        try XCTSkipIf(done.isEmpty, "the swipe host runs only with a file to wait for")
        try use("en")
        let fixture = DetailFixture(.today)
        let scene = try XCTUnwrap(UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first)
        let previous = scene.windows.first { $0.isKeyWindow }
        let host = UIHostingController(rootView: OrdersRootView(
            client: fixture.client,
            profileClient: FakePartnerProfileClient(),
            staleness: OrdersStaleness(),
            checklistStore: fixture.checklist,
            snackbar: SnackbarController(),
            pendingOffers: fixture.offers,
            boardStore: fixture.store
        )
        .tint(CleansiaColors.primaryText)
        .environment(\.locale, Locale(identifier: "en")))
        let window = UIWindow(windowScene: scene)
        window.rootViewController = host
        window.makeKeyAndVisible()
        defer {
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        print("[swipe] ready \(window.bounds.size) \(UIDevice.current.systemVersion)")
        let log = PopGestureLog()
        var last = -1
        var settles: Date?
        let deadline = Date().addingTimeInterval(900)
        while !FileManager.default.fileExists(atPath: done), Date() < deadline {
            try? await Task.sleep(nanoseconds: 150_000_000)
            let stack = Self.navigation(in: host.view)
            let depth = stack?.viewControllers.count ?? 0
            if depth != last {
                print(Self.depthLine(stack, depth: depth, log: log))
                last = depth
                settles = Date().addingTimeInterval(1.2)
            }
            if let due = settles, Date() >= due {
                settles = nil
                let gesture = stack?.interactivePopGestureRecognizer
                let allows = gesture.flatMap { $0.delegate?.gestureRecognizerShouldBegin?($0) }
                print("[swipe] settled depth \(depth) shouldBegin \(allows.map(String.init) ?? "n/a") "
                    + "inTransition \(stack?.transitionCoordinator != nil)")
            }
        }
    }

    /// The stack's depth, its top screen and the state of its pop recognisers, watching each for the log.
    private static func depthLine(_ stack: UINavigationController?, depth: Int, log: PopGestureLog) -> String {
        let top = stack?.topViewController.map { String(describing: type(of: $0)).prefix(60) } ?? ""
        let gesture = stack?.interactivePopGestureRecognizer
        gesture.map { log.watch($0, as: "popGesture") }
        let allows = gesture.flatMap { $0.delegate?.gestureRecognizerShouldBegin?($0) }
        var contentPop = ""
        if #available(iOS 26.0, *), let content = stack?.interactiveContentPopGestureRecognizer {
            log.watch(content, as: "contentPopGesture")
            contentPop = " contentPop enabled \(content.isEnabled) delegate "
                + (content.delegate.map { String(describing: type(of: $0)) } ?? "nil")
        }
        return "[swipe] depth \(depth) at \(Date().timeIntervalSince1970) top \(top) "
            + "popGesture enabled \(gesture?.isEnabled ?? false) delegate "
            + (gesture?.delegate.map { String(describing: type(of: $0)) } ?? "nil")
            + " shouldBegin \(allows.map(String.init) ?? "n/a") barHidden "
            + "\(stack?.isNavigationBarHidden ?? false) hidesBack "
            + "\(stack?.topViewController?.navigationItem.hidesBackButton ?? false)" + contentPop
    }

    /// Prints each state the stack's pop recognisers pass through (the edge swipe, and on iOS 26 the content swipe),
    /// so a swipe that did not pop says whether a recogniser never began (the touch or its delegate) or began and was
    /// cancelled.
    private final class PopGestureLog: NSObject {
        private var watched: [ObjectIdentifier: String] = [:]

        func watch(_ gesture: UIGestureRecognizer, as name: String) {
            guard watched[ObjectIdentifier(gesture)] == nil else { return }
            watched[ObjectIdentifier(gesture)] = name
            gesture.addTarget(self, action: #selector(changed(_:)))
        }

        @objc private func changed(_ gesture: UIGestureRecognizer) {
            let names: [UIGestureRecognizer.State: String] = [
                .began: "began", .changed: "changed", .ended: "ended", .cancelled: "cancelled", .failed: "failed"
            ]
            guard gesture.state != .changed else { return }
            print("[swipe] \(watched[ObjectIdentifier(gesture)] ?? "gesture") "
                + "\(names[gesture.state] ?? "\(gesture.state.rawValue)") at \(Date().timeIntervalSince1970)")
        }
    }

    private static func navigation(in view: UIView) -> UINavigationController? {
        var responder: UIResponder? = view
        while let current = responder {
            if let stack = current as? UINavigationController { return stack }
            responder = current.next
        }
        for subview in view.subviews {
            if let found = navigation(in: subview) { return found }
        }
        return nil
    }
}

extension PartnerOrderDetailVisualTests {
    func testMockDetail() async throws {
        for state in [DetailState.today, .setoff, .browse, .completed, .crewWait, .cancelled, .error] {
            for theme in ["light", "dark"] {
                try await mockShot(state, "en", theme)
            }
        }
        for theme in ["light", "dark"] {
            try await mockShot(.completed, "en", theme, full: true)
            try await mockShot(.browse, "uk", theme, small: true)
            try await mockShot(.browse, "en", theme, large: true, full: true)
        }
        for theme in ["light", "dark"] {
            try await mockShot(.progressDev, "en", theme)
            try await mockShot(.progressDev, "en", theme, scroll: .offset(380), suffix: "-scrolled")
            try await mockShot(.progress, "en", theme)
            try await mockShot(.progress, "en", theme, scroll: .end, suffix: "-end")
            try await mockShot(.progress, "en", theme, full: true)
            try await mockShot(.progress, "en", theme, large: true)
            try await mockShot(.progress, "en", theme, large: true, full: true)
            try await mockShot(.progress, "en", theme, small: true)
            try await mockShot(.progress, "uk", theme, small: true)
        }
    }

    /// The band's map before the job is taken and while she cleans, the cleaning figure and the Note and Issue row,
    /// once the map's tiles have come.
    func testMockBandMaps() async throws {
        let notes = MockCapture.Scroll.toolbar(L10n.Orders.photosSectionTitle)
        for theme in ["light", "dark"] {
            try await mockShot(.browse, "en", theme, suffix: "-map", settle: 6)
            try await mockShot(.progress, "en", theme, suffix: "-map", settle: 6)
            try await mockShot(.progress, "en", theme, scroll: notes, suffix: "-map-photos-notes", settle: 3)
        }
    }

    func mockShot(
        _ state: DetailState,
        _ language: String,
        _ theme: String,
        small: Bool = false,
        large: Bool = false,
        full: Bool = false,
        scroll: MockCapture.Scroll = .rest,
        suffix: String = "",
        settle: Double = 0
    ) async throws {
        try MockCapture.use(language, near: Self.self)
        let canvas = small ? CGSize(width: 375, height: 667) : CGSize(width: 393, height: 852)
        let height = full ? (large ? 4400 : 2600) : canvas.height
        let fixture = DetailFixture(state)
        let name = "detail-\(state.rawValue)-ios-\(Int(canvas.width))x\(Int(canvas.height))-\(language)-\(theme)"
            + (large ? "-ax1" : "") + suffix + (full ? "-full" : "")
        await MockCapture.shoot(
            self,
            name: name,
            size: CGSize(width: canvas.width, height: height),
            statusBar: small ? 20 : 54,
            homeIndicator: small ? 0 : 34,
            scroll: scroll,
            settle: settle
        ) {
            NavigationStack(path: .constant([fixture.orderId])) {
                Color.clear
                    .navigationTitle(L10n.Orders.title)
                    .navigationDestination(for: String.self) { orderId in
                        OrderDetailView(
                            orderId: orderId,
                            client: fixture.client,
                            staleness: OrdersStaleness(),
                            checklistStore: fixture.checklist,
                            snackbar: SnackbarController(),
                            pendingOffers: fixture.offers,
                            boardStore: fixture.store,
                            showRemovalReason: fixture.removal,
                            onOpenLegalDocuments: {},
                            onOpenJob: { _ in },
                            onBrowseJobs: {},
                            onOpenEarnings: {},
                            now: { fixture.now }
                        )
                        .environment(\.jobPanelOpensCollapsed, suffix.hasSuffix("-collapsed"))
                    }
            }
            .frame(width: canvas.width, height: height)
            .tint(CleansiaColors.primaryText)
            .environment(\.locale, Locale(identifier: language))
            .preferredColorScheme(theme == "dark" ? .dark : .light)
            .dynamicTypeSize(large ? .accessibility1 : .large)
        }
        fixture.client.resumeGetById()
    }
}

// MARK: - The spec sheets

/// The leaf at both sizes in its three fills, in Ukrainian, and at an accessibility size (capped at xxxLarge).
private struct LeafSheet: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            SheetTitle(text: "The calendar leaf, native")
            panel("Header leaf: upcoming, today, closed") {
                leaves(.header, days: [14, 12, 10], language: "en")
            }
            panel("Row leaf: upcoming, today, closed") {
                leaves(.row, days: [13, 12, 10], language: "en")
            }
            panel("Ukrainian at rest") {
                leaves(.header, days: [14], language: "uk")
                leaves(.row, days: [13], language: "uk")
            }
            panel("AX1 (capped at xxxLarge)") {
                HStack(alignment: .top, spacing: 16) {
                    leaves(.header, days: [14], language: "en")
                    leaves(.row, days: [13], language: "en")
                }
                .dynamicTypeSize(.accessibility1)
            }
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.background)
    }

    private func leaves(_ size: OrdersDateTile.Size, days: [Int], language: String) -> some View {
        HStack(alignment: .top, spacing: 16) {
            ForEach(days, id: \.self) { day in
                OrdersDateTile(
                    date: OrdersDesignFixture.at(day, 9, 0),
                    size: size,
                    now: OrdersDesignFixture.now,
                    closed: day < 12
                )
            }
        }
        .environment(\.locale, Locale(identifier: language))
    }

    private func panel(_ title: String, @ViewBuilder content: () -> some View) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(verbatim: title).font(CleansiaTypography.titleMedium).foregroundColor(CleansiaColors.onSurface)
            content()
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: 16))
    }
}

/// The reserved card's pay tile: Confirm, and Confirm on the DEV job's haléře.
private struct TileSheet: View {
    let rows: [OrderListItem]

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            SheetTitle(text: "The pay tile, native")
            ForEach(Array(faces.enumerated()), id: \.offset) { _, entry in
                VStack(alignment: .leading, spacing: 8) {
                    Text(verbatim: entry.0).font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    OrdersPayTile(face: entry.1) {}
                        .frame(width: 132)
                }
            }
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.background)
    }

    private var faces: [(String, PayTileFace)] {
        let byId = Dictionary(uniqueKeysWithValues: rows.compactMap { row in row.id.map { ($0, row) } })
        var out: [(String, PayTileFace)] = []
        if let reserved = byId["R1"] {
            out.append(("Confirm, the reserved card", PayTileFace(row: reserved, isTaking: false)))
        }
        if let dev = byId["D1"] {
            out.append(("Confirm, DEV-like haléře", PayTileFace(row: dev, isTaking: false)))
        }
        return out
    }
}

/// The slide in the four states the pages use, light over dark.
private struct SlideSheet: View {
    var body: some View {
        VStack(spacing: 0) {
            panel.background(CleansiaColors.background).environment(\.colorScheme, .light)
            panel.background(CleansiaColors.background).environment(\.colorScheme, .dark)
        }
    }

    private var panel: some View {
        VStack(spacing: 12) {
            SlideToConfirm(
                idleLabel: L10n.tie(L10n.Orders.swipeToNotifyOnTheWay),
                busyLabel: L10n.tie(L10n.Orders.customerNotifiedOnTheWay),
                isBusy: false,
                maxLabelLines: 2,
                thumbLanes: true,
                onConfirm: {}
            )
            SlideToConfirm(
                idleLabel: L10n.tie(L10n.WorkContract.swipeToAccept),
                busyLabel: L10n.tie(L10n.WorkContract.accepting),
                isBusy: false,
                maxLabelLines: 2,
                thumbLanes: true,
                onConfirm: {}
            )
            SlideToConfirm(lockedReason: L10n.tie(L10n.Orders.setOffFrom("13:00")))
            SlideToConfirm(lockedReason: L10n.tie(L10n.Orders.paymentNotConfirmed))
        }
        .padding(16)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private struct SheetTitle: View {
    let text: String

    var body: some View {
        Text(verbatim: text)
            .cleansiaFont(CleansiaTypography.headlineSmall)
            .foregroundColor(CleansiaColors.onSurface)
    }
}
