import CleansiaCore
import CleansiaPartnerApi
import Foundation
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

// MARK: - The design's sample data (synthetic: no real customer, address or person)

enum OrdersDesignFixture {
    enum Cash {
        case held
        case noCap
        case over
    }

    static let now = at(12, 9, 20)
    static let offer = PendingOfferItem(id: "R1", respondByUtc: at(12, 12, 6), currencyCode: "CZK")

    static func at(_ day: Int, _ hour: Int, _ minute: Int, month: Int = 11) -> Date {
        Calendar.current.date(from: DateComponents(year: 2026, month: month, day: day, hour: hour, minute: minute))
            ?? Date()
    }

    private static let services: [String: [String: String]] = [
        "standard": ["en": "Standard clean", "cs": "Standardní úklid", "uk": "Стандартне прибирання"],
        "deep": ["en": "Deep clean", "cs": "Hloubkový úklid", "uk": "Генеральне прибирання"],
        "moveout": ["en": "Move-out clean", "cs": "Úklid po vystěhování", "uk": "Прибирання після виїзду"],
        "windows": ["en": "Windows", "cs": "Okna", "uk": "Вікна"],
        "complete": [
            "en": "Complete Home Clean",
            "cs": "Kompletní úklid domácnosti",
            "uk": "Повне прибирання оселі"
        ]
    ]

    private static let slugs = [
        "oven": "inside-oven", "fridge": "inside-fridge", "iwin": "interior-windows", "ironing": "laundry-ironing"
    ]

    // swiftlint:disable:next function_parameter_count
    static func row(
        _ id: String,
        _ day: Int,
        _ from: (Int, Int),
        _ minutes: Int,
        _ service: String,
        _ rooms: Int,
        _ baths: Int,
        pay: Double,
        status: OrderStatus = ._0,
        month: Int = 11
    ) -> OrderListItem {
        var row = OrderListItem.sample(
            id: id,
            status: status,
            pay: pay,
            cleaningDateTime: at(day, from.0, from.1, month: month)
        )
        row.estimatedTime = minutes
        row.currency = CurrencyListItem(code: "CZK", symbol: "Kč")
        let names = services[service] ?? [:]
        row.selectedServices = [ServiceListItem(
            id: "svc-\(service)",
            name: names["en"],
            translations: ["cs": Translation(name: names["cs"]), "uk": Translation(name: names["uk"])]
        )]
        row.rooms = rooms
        row.bathrooms = baths
        row.paymentType = Code(value: 0)
        row.requiredEmployees = 1
        row.assignedEmployeesCount = 0
        return row
    }

    static func extras(_ row: inout OrderListItem, _ names: [String]) {
        row.extras = Dictionary(uniqueKeysWithValues: names.compactMap { slugs[$0] }.map { ($0, true) })
    }

    static func cashJob(_ row: inout OrderListItem, _ amount: Double, paid: Int? = nil) {
        row.paymentType = Code(value: 1)
        row.amountDueOnCard = amount
        row.paymentStatus = paid.map { Code(value: $0) }
    }

    static func available() -> [OrderListItem] {
        var underWay = row("V1", 12, (8, 0), 240, "deep", 4, 2, pay: 762, status: ._4)
        underWay.requiredEmployees = 2
        underWay.assignedEmployeesCount = 1
        underWay.customerAddressApproximate = "Praha 5"
        underWay.homeDistanceBand = ._3
        var tomorrowCash = row("V3", 13, (8, 0), 120, "standard", 2, 1, pay: 804)
        tomorrowCash.customerAddressApproximate = "Praha 4"
        tomorrowCash.homeDistanceBand = ._1
        extras(&tomorrowCash, ["oven"])
        cashJob(&tomorrowCash, 1390)
        var tomorrowClash = row("V4", 13, (14, 0), 120, "windows", 3, 1, pay: 600)
        tomorrowClash.customerAddressApproximate = "Praha 10"
        tomorrowClash.homeDistanceBand = ._2
        var crewSaturday = row("V5", 14, (9, 0), 210, "deep", 3, 2, pay: 749.5, status: ._2)
        crewSaturday.requiredEmployees = 2
        crewSaturday.assignedEmployeesCount = 1
        crewSaturday.customerAddressApproximate = "Praha 8"
        crewSaturday.homeDistanceBand = ._3
        extras(&crewSaturday, ["fridge"])
        var reserved = row("R1", 14, (10, 0), 120, "standard", 3, 1, pay: 944)
        reserved.customerAddressApproximate = "Praha 6"
        reserved.homeDistanceBand = ._4
        extras(&reserved, ["iwin"])
        var moveOut = row("V6", 16, (11, 0), 180, "moveout", 2, 1, pay: 954.85)
        moveOut.requiredEmployees = 2
        moveOut.customerAddressApproximate = "Praha 4"
        moveOut.homeDistanceBand = ._2
        moveOut.dirtinessLevel = ._2
        extras(&moveOut, ["oven", "fridge"])
        var windows = row("V7", 16, (14, 0), 120, "windows", 2, 2, pay: 690)
        windows.customerAddressApproximate = "Praha 9"
        windows.homeDistanceBand = ._3
        windows.dirtinessLevel = ._1
        var noBand = row("V8", 17, (10, 0), 120, "standard", 1, 1, pay: 739)
        noBand.customerAddressApproximate = "Praha 1"
        extras(&noBand, ["ironing"])
        var lateMonday = row("V9", 17, (15, 0), 120, "standard", 2, 1, pay: 654)
        lateMonday.customerAddressApproximate = "Praha 6"
        lateMonday.homeDistanceBand = ._4
        return [underWay, tomorrowCash, tomorrowClash, crewSaturday, reserved, moveOut, windows, noBand, lateMonday]
    }

    /// The board as the design draws it: the designed rows with the three DEV-like ones beside them (7-minute jobs,
    /// haléře, cash with increased dirt, an area with no band), in the server's soonest-first order.
    static func availableWithDevRows() -> [OrderListItem] {
        var first = row("D1", 12, (12, 15), 7, "complete", 1, 1, pay: 746.93)
        first.customerAddressApproximate = "Praha 8"
        first.dirtinessLevel = ._1
        cashJob(&first, 1419.16)
        var second = row("D2", 12, (12, 30), 7, "complete", 1, 1, pay: 804.43)
        second.customerAddressApproximate = "Praha 8"
        second.dirtinessLevel = ._1
        extras(&second, ["iwin"])
        var third = row("D3", 13, (8, 30), 7, "complete", 1, 1, pay: 833.18)
        third.customerAddressApproximate = "Praha 8"
        extras(&third, ["fridge"])
        cashJob(&third, 1583.03)
        var rows = available()
        rows.insert(contentsOf: [first, second], at: 1)
        rows.insert(third, at: 4)
        return rows
    }

    static func active(heroStatus: OrderStatus = ._2) -> [OrderListItem] {
        var today = row("A1", 12, (14, 0), 120, "standard", 2, 1, pay: 779, status: heroStatus)
        today.displayOrderNumber = "ORD-7F3A9C21"
        today.customerAddress = "Vinohradská 46, Praha, 12000"
        today.customerAddressApproximate = "Praha 2"
        today.customerName = "Jana Nováková"
        today.customerPhone = "+420 604 218 377"
        extras(&today, ["fridge"])
        var crew = row("A2", 13, (15, 0), 180, "deep", 3, 1, pay: 612, status: ._2)
        crew.displayOrderNumber = "ORD-91E2F0AB"
        crew.requiredEmployees = 2
        crew.assignedEmployeesCount = 2
        crew.customerAddress = "Seifertova 21, Praha, 13000"
        crew.customerAddressApproximate = "Praha 3"
        crew.customerName = "Petr Dvořák"
        crew.customerPhone = "+420 736 512 090"
        var cash = row("A3", 16, (8, 0), 120, "standard", 1, 1, pay: 714, status: ._2)
        cash.displayOrderNumber = "ORD-2B8E14D7"
        cash.customerAddress = "Hornoměcholupská 12, Praha, 10900"
        cash.customerAddressApproximate = "Praha 10"
        cash.customerName = "Marie Svobodová"
        cash.customerPhone = "+420 777 301 455"
        extras(&cash, ["oven"])
        cashJob(&cash, 1240)
        return [today, crew, cash]
    }

    // swiftlint:disable:next function_parameter_count
    private static func done(
        _ id: String,
        _ day: Int,
        _ from: (Int, Int),
        _ minutes: Int,
        _ area: String,
        _ service: String,
        pay: Double,
        team: Bool = false,
        month: Int = 11
    ) -> OrderListItem {
        var job = row(id, day, from, minutes, service, 2, 1, pay: pay, status: ._5, month: month)
        job.customerAddressApproximate = area
        job.requiredEmployees = team ? 2 : 1
        return job
    }

    static func history(_ period: CompletedPeriod) -> [OrderListItem] {
        var notPaid = done("H8", 11, (8, 0), 120, "Praha 4", "standard", pay: 829)
        cashJob(&notPaid, 1440, paid: 1)
        var collected = done("H4", 5, (10, 0), 120, "Praha 10", "standard", pay: 714)
        cashJob(&collected, 1240, paid: 2)
        let november = [
            done("H9", 11, (13, 0), 180, "Praha 1", "deep", pay: 537, team: true), notPaid,
            done("H7", 10, (9, 0), 120, "Praha 9", "windows", pay: 690),
            done("H6", 9, (14, 0), 120, "Praha 6", "standard", pay: 944),
            done("H5", 7, (9, 0), 240, "Praha 8", "moveout", pay: 776.1, team: true), collected,
            done("H3", 3, (8, 0), 240, "Praha 5", "deep", pay: 762, team: true),
            done("H2", 2, (13, 0), 120, "Praha 10", "windows", pay: 600),
            done("H1", 2, (9, 0), 120, "Praha 2", "standard", pay: 654)
        ]
        switch period {
        case .thisWeek:
            return november.filter { ($0.cleaningDateTime ?? .distantPast) >= at(9, 0, 0) }
        case .thisMonth: return november
        case .lastMonth: return october
        }
    }

    static var october: [OrderListItem] {
        var octoberCash = done("O2", 2, (13, 0), 120, "Praha 10", "standard", pay: 714, month: 10)
        cashJob(&octoberCash, 1240, paid: 2)
        var octoberLater = done("O9", 21, (13, 0), 120, "Praha 4", "standard", pay: 804, month: 10)
        cashJob(&octoberLater, 1390, paid: 2)
        return [
            done("O14", 30, (14, 0), 120, "Praha 10", "windows", pay: 600, month: 10),
            done("O13", 30, (9, 0), 120, "Praha 2", "standard", pay: 779, month: 10),
            done("O12", 29, (8, 0), 240, "Praha 5", "deep", pay: 762, team: true, month: 10),
            done("O11", 27, (10, 0), 120, "Praha 1", "standard", pay: 739, month: 10),
            done("O10", 23, (8, 0), 240, "Praha 8", "moveout", pay: 776.1, team: true, month: 10), octoberLater,
            done("O8", 19, (9, 0), 120, "Praha 2", "standard", pay: 829, month: 10),
            done("O7", 15, (8, 0), 180, "Praha 8", "deep", pay: 537, team: true, month: 10),
            done("O6", 13, (14, 0), 120, "Praha 9", "windows", pay: 690, month: 10),
            done("O5", 9, (9, 0), 120, "Praha 6", "standard", pay: 944, month: 10),
            done("O4", 7, (10, 0), 120, "Praha 10", "windows", pay: 600, month: 10),
            done("O3", 5, (8, 0), 240, "Praha 5", "deep", pay: 762, team: true, month: 10), octoberCash,
            done("O1", 1, (9, 0), 120, "Praha 2", "standard", pay: 654, month: 10)
        ]
    }

    static func cash(_ kind: Cash) -> CashHeld {
        switch kind {
        case .held: CashHeld(currencyCode: "CZK", amount: 1240, floatCap: 3000, cashJobsHidden: false)
        case .noCap: CashHeld(currencyCode: "CZK", amount: 1240, floatCap: nil, cashJobsHidden: false)
        case .over: CashHeld(currencyCode: "CZK", amount: 3200.40, floatCap: 3000, cashJobsHidden: true)
        }
    }

    static func contract(for row: OrderListItem, version: String = "2026-10-27") -> WorkContract {
        var facts = WorkContractJobFacts(
            orderNumber: "CL-2026-0042",
            cleaningDateTimeUtc: row.cleaningDateTime ?? Date(),
            estimatedMinutes: row.estimatedTime ?? 0,
            reward: row.estimatedCleanerPay ?? 0,
            currencyCode: "CZK",
            locationApproximate: row.customerAddressApproximate,
            rooms: row.rooms ?? 0,
            bathrooms: row.bathrooms ?? 0,
            services: row.selectedServices?.compactMap(\.name) ?? [],
            packages: [],
            extraSlugs: OrdersBoardRules.extraSlugs(row.extras)
        )
        facts.serviceIds = row.selectedServices?.compactMap(\.id) ?? []
        return WorkContract(
            legalDocumentTextId: "text-1",
            version: version,
            language: "en",
            title: "Contract for work",
            contentHtml: "<p>This contract for work is concluded for one cleaning job between the operating company"
                + " that sold the cleaning to its customer (the client) and you, the cleaner who accepts it in the"
                + " Cleansia partner app or on the partner website (the contractor).</p><h3>The client</h3><p>[company"
                + " legal name], company ID [ID number], registered office [registered office].</p>",
            facts: facts,
            acceptance: nil
        )
    }

    /// The board from the DEV screenshot, tomorrow: two 7-minute open jobs paid to the haléř, each with cash, one
    /// also dirtier and with the fridge, both in "Praha 8" with no band; her own 6-minute job just before them.
    static func screenshot() -> (open: [OrderListItem], own: [OrderListItem]) {
        var early = row("S1", 13, (8, 30), 7, "complete", 1, 1, pay: 746.93)
        early.customerAddressApproximate = "Praha 8"
        early.dirtinessLevel = ._1
        extras(&early, ["fridge"])
        cashJob(&early, 1419.16)
        var noon = row("S2", 13, (12, 15), 7, "complete", 1, 1, pay: 833.18)
        noon.customerAddressApproximate = "Praha 8"
        cashJob(&noon, 1583.03)
        var mine = row("S0", 13, (8, 15), 6, "complete", 1, 1, pay: 712.4, status: ._2)
        mine.customerAddress = "Sokolovská 120, Praha, 18600"
        mine.customerAddressApproximate = "Praha 8"
        mine.customerName = "Jana Nováková"
        mine.customerPhone = "+420 604 218 377"
        return ([early, noon], [mine])
    }

    static func taken(_ row: OrderListItem) -> BoardTake {
        var mine = row
        mine.orderStatus = Code(value: 2)
        mine.customerAddress = "Opatovská 8, Praha, 14900"
        mine.customerAddressLatitude = 50.0303
        mine.customerAddressLongitude = 14.5095
        return BoardTake(snapshot: row, taken: mine, takenLookupDone: true)
    }
}

// MARK: - The board from the DEV screenshot

extension PartnerOrdersVisualTests {
    /// Short jobs on the day line, haléře, cash with dirt, the long scope and no band: light and dark, en and cs, on
    /// both phones; and the Active hero over her 6-minute job.
    func testScreenshotCase() async throws {
        let (open, own) = OrdersDesignFixture.screenshot()
        for language in ["en", "cs"] {
            for theme in ["light", "dark"] {
                for size in [phone, small] {
                    try await shoot("available", "screenshot", language, theme, size: size, board: Board(
                        available: open,
                        active: own
                    ))
                }
            }
        }
        for theme in ["light", "dark"] {
            let board = Board(tab: .active, available: open, active: own)
            try await shoot("active", "screenshot", "en", theme, board: board)
        }
    }
}

// MARK: - The rest of the 137-job board: the design generates 91 more rows from one seed, and so does this

extension OrdersDesignFixture {
    static func more() -> [OrderListItem] {
        var seed = 1_523_808_771.0
        func rnd() -> Double {
            seed = (seed * 1_103_515_245 + 12345).truncatingRemainder(dividingBy: 2_147_483_648)
            return seed / 2_147_483_648
        }
        var rows: [(index: Int, row: OrderListItem)] = []
        var day = 17
        var month = 11
        while rows.count < 91 {
            let perDay = 3 + Int(rnd() * 4)
            for _ in 0 ..< perDay where rows.count < 91 {
                rows.append((index: rows.count, row: moreRow("V\(10 + rows.count)", day, month, rnd)))
            }
            day += 1
            if month == 11, day > 30 {
                month = 12
                day = 1
            }
        }
        return rows.sorted {
            ($0.row.cleaningDateTime ?? .distantPast, $0.index) < ($1.row.cleaningDateTime ?? .distantPast, $1.index)
        }.map(\.row)
    }

    /// One generated row, drawing from the seed in the design's order: service, rooms, baths, length, start, area,
    /// band, extras, dirt, and the cash draw on a one-cleaner job only.
    private static func moreRow(_ id: String, _ day: Int, _ month: Int, _ rnd: () -> Double) -> OrderListItem {
        func pick<T>(_ list: [T]) -> T {
            list[Int(rnd() * Double(list.count))]
        }
        let prices: [String: [Double]] = [
            "standard": [790, 150, 150], "deep": [1290, 250, 250], "moveout": [1490, 250, 250],
            "windows": [700, 100, 100]
        ]
        let extraPrices = ["oven": 300.0, "fridge": 250, "iwin": 400, "ironing": 350]
        let service = pick(["standard", "standard", "standard", "deep", "windows", "moveout"])
        let rooms = 1 + Int(rnd() * 4)
        let baths = 1 + Int(rnd() * 2)
        let minutes = service == "deep" || service == "moveout" ? pick([180, 240]) : 120
        let start = pick([8, 9, 10, 11, 13, 14, 15])
        let crew = (minutes + 119) / 120
        let area = "Praha \(1 + Int(rnd() * 10))"
        let band = rnd() < 0.08 ? nil : HomeDistanceBand(rawValue: 1 + Int(rnd() * 5))
        let added = pick([[], [], ["oven"], ["fridge"], ["ironing"], ["iwin"], ["oven", "fridge"]])
        let dirty = rnd() < 0.1
        let price = prices[service] ?? [0, 0, 0]
        let core = price[0] + Double(rooms - 1) * price[1] + Double(baths) * price[2]
        let extrasTotal = added.reduce(0) { $0 + (extraPrices[$1] ?? 0) }
        let job = (0.6 * core + 0.5 * extrasTotal) * (dirty ? 1.15 : 1)
        let seat = (job / Double(crew) * 100 + 1e-6).rounded(.down) / 100
        var next = row(id, day, (start, 0), minutes, service, rooms, baths, pay: seat, month: month)
        next.requiredEmployees = crew
        next.customerAddressApproximate = area
        next.homeDistanceBand = band
        if dirty { next.dirtinessLevel = ._1 }
        extras(&next, added)
        if crew == 1, rnd() < 0.15 { cashJob(&next, core + extrasTotal) }
        return next
    }

    /// The checklist ids of a job, in the order the checklist draws them: services, then extras.
    static func checklistIds(_ item: OrderItem) -> [String] {
        (item.selectedServices ?? []).map { "service:\($0.id ?? $0.name ?? "")" }
            + (item.extras ?? [:]).filter(\.value).keys.sorted().map { "extra:\($0)" }
    }
}

// MARK: - What a capture records besides its pixels

@MainActor
enum CaptureProbe {
    /// The accessibility tree the window exposes, walked as VoiceOver walks it: label, value, traits, frame.
    static func semantics(of window: UIWindow) -> Data {
        var nodes: [[String: Any]] = []
        func visit(_ node: NSObject, _ depth: Int) {
            guard depth < 80 else { return }
            if node.isAccessibilityElement {
                let frame = node.accessibilityFrame
                nodes.append([
                    "label": node.accessibilityLabel ?? "",
                    "value": node.accessibilityValue ?? "",
                    "traits": node.accessibilityTraits.rawValue,
                    "frame": [frame.minX, frame.minY, frame.width, frame.height].map { ($0 * 10).rounded() / 10 }
                ])
                return
            }
            if let elements = node.accessibilityElements as? [NSObject], !elements.isEmpty {
                elements.forEach { visit($0, depth + 1) }
                return
            }
            let count = node.accessibilityElementCount()
            if count != NSNotFound, count > 0 {
                for index in 0 ..< count {
                    (node.accessibilityElement(at: index) as? NSObject).map { visit($0, depth + 1) }
                }
                return
            }
            (node as? UIView)?.subviews.forEach { visit($0, depth + 1) }
        }
        visit(window, 0)
        return (try? JSONSerialization.data(withJSONObject: nodes, options: [.prettyPrinted, .sortedKeys])) ?? Data()
    }

    /// Scrolls the longest scroll view to its end, as the design's `scroll=end` frames.
    static func scrollToEnd(in view: UIView) {
        func scrollViews(_ view: UIView) -> [UIScrollView] {
            ((view as? UIScrollView).map { [$0] } ?? []) + view.subviews.flatMap(scrollViews)
        }
        guard let longest = scrollViews(view).max(by: {
            $0.contentSize.height - $0.bounds.height < $1.contentSize.height - $1.bounds.height
        }) else { return }
        let end = longest.contentSize.height + longest.adjustedContentInset.bottom - longest.bounds.height
        longest.setContentOffset(CGPoint(x: 0, y: max(end, -longest.adjustedContentInset.top)), animated: false)
    }

    /// The window sits on the 390 × 844 iPhone 14 screen, whose insets it takes; the design's phone is 393 × 852 with
    /// a 34-pt home indicator (an SE has none) and the frames draw their own status bar. The hosted content is given
    /// those insets instead.
    static func deviceInsets(_ host: UIViewController, in window: UIWindow, homeIndicator: CGFloat) {
        let screen = window.safeAreaInsets
        host.additionalSafeAreaInsets = UIEdgeInsets(
            top: -screen.top,
            left: 0,
            bottom: homeIndicator - screen.bottom,
            right: 0
        )
    }

    static func attach(_ window: UIWindow, name: String, to test: XCTestCase) {
        let content = window.rootViewController?.view.safeAreaInsets ?? .zero
        print("[capture] \(name) window \(window.safeAreaInsets) content \(content)")
        // The render server draws nothing for a window much past 8 000 px tall, so a longer page is drawn from its
        // layers at a lower scale.
        let tall = window.bounds.height * window.screen.scale > 8000
        let format = UIGraphicsImageRendererFormat(for: window.traitCollection)
        if tall { format.scale = 8000 / window.bounds.height }
        let image = UIGraphicsImageRenderer(bounds: window.bounds, format: format).image { context in
            if tall {
                window.layer.render(in: context.cgContext)
            } else {
                window.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
            }
        }
        let picture = XCTAttachment(image: image)
        picture.name = name
        picture.lifetime = .keepAlways
        test.add(picture)
        let tree = XCTAttachment(data: semantics(of: window), uniformTypeIdentifier: "public.json")
        tree.name = name + ".semantics"
        tree.lifetime = .keepAlways
        test.add(tree)
        guard ProcessInfo.processInfo.environment["PARTNER_ORDERS_CAPTURE_RENDER"] == "1" else { return }
        let drawn = XCTAttachment(data: CaptureRenderTree.json(of: window), uniformTypeIdentifier: "public.json")
        drawn.name = name + ".render.tree"
        drawn.lifetime = .keepAlways
        test.add(drawn)
    }
}

// MARK: - A view as VoiceOver reads it

/// A view hosted in a window of a phone's width and read the way VoiceOver walks it, element by element, with
/// non-breaking spaces read as spaces; an element can be activated as a double tap would.
@MainActor
final class HostedSpeech {
    private let window: UIWindow
    private let host: UIViewController

    init(_ view: some View, size: CGSize = CGSize(width: 393, height: 1600)) {
        host = UIHostingController(
            rootView: view.frame(width: size.width).environment(\.locale, Locale(identifier: "en"))
        )
        let scene = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }.first
        window = scene.map(UIWindow.init(windowScene:)) ?? UIWindow()
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
    }

    static func labels(of view: some View, size: CGSize = CGSize(width: 393, height: 1600)) async throws -> [String] {
        let hosted = HostedSpeech(view, size: size)
        defer { hosted.close() }
        try await hosted.settle()
        return hosted.labels
    }

    static func plain(_ text: String) -> String {
        text.replacingOccurrences(of: "\u{00A0}", with: " ").replacingOccurrences(of: "\u{202F}", with: " ")
    }

    func settle(_ rounds: Int = 6) async throws {
        for _ in 0 ..< rounds {
            try await Task.sleep(nanoseconds: 60_000_000)
            host.view.layoutIfNeeded()
        }
    }

    var labels: [String] {
        elements.map { Self.plain($0.accessibilityLabel ?? "") }
    }

    @discardableResult
    func activate(_ label: String) -> Bool {
        let wanted = Self.plain(label)
        return elements.first { Self.plain($0.accessibilityLabel ?? "") == wanted }?.accessibilityActivate() ?? false
    }

    func close() {
        host.presentedViewController?.dismiss(animated: false)
        window.isHidden = true
        window.rootViewController = nil
    }

    private var elements: [NSObject] {
        var found: [NSObject] = []
        func visit(_ node: NSObject, _ depth: Int) {
            guard depth < 80 else { return }
            if node.isAccessibilityElement {
                found.append(node)
                return
            }
            if let elements = node.accessibilityElements as? [NSObject], !elements.isEmpty {
                elements.forEach { visit($0, depth + 1) }
                return
            }
            let count = node.accessibilityElementCount()
            if count != NSNotFound, count > 0 {
                for index in 0 ..< count {
                    (node.accessibilityElement(at: index) as? NSObject).map { visit($0, depth + 1) }
                }
                return
            }
            (node as? UIView)?.subviews.forEach { visit($0, depth + 1) }
        }
        visit(window, 0)
        return found
    }
}

/// The Before column, its Add tapped: the camera, or on a phone with none, why not.
struct PhotoAddHost: View {
    @State private var asked = false

    var body: some View {
        VStack(alignment: .leading) {
            PhotoColumn(
                title: L10n.Orders.photosBefore,
                type: ._1,
                photos: [],
                canUpload: true,
                mutation: PhotoMutationState(),
                imageCache: RemoteImageCache(),
                request: $asked,
                onPick: { _, _ in },
                onDelete: { _ in }
            )
            .padding(Spacing.m)
            Spacer()
        }
        .background(CleansiaColors.surface)
        .onAppear { DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { asked = true } }
    }
}
