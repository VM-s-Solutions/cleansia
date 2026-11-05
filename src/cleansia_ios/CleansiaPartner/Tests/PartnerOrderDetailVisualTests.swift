import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// Native captures of the job detail in every state the design draws, from its own sample data (Thursday 12
/// November 2026): the real screen over a fake client, never the app against an API. Each attachment is named as
/// the design's frame, `detail-<state>-ios-<w>x<h>-<lang>-<theme>[-ax1][-full]`; the lane exports them.
@MainActor
final class PartnerOrderDetailVisualTests: XCTestCase {
    private let phone = CGSize(width: 393, height: 852)
    private let small = CGSize(width: 375, height: 667)
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

    /// `PARTNER_ORDERS_CAPTURE_STATES` (comma-separated state names) shoots only those states' four frames.
    func testDetail() async throws {
        let asked = ProcessInfo.processInfo.environment["PARTNER_ORDERS_CAPTURE_STATES"]?
            .split(separator: ",").map(String.init)
        for state in DetailState.allCases where asked?.contains(state.rawValue) ?? true {
            for (language, theme) in [("en", "light"), ("en", "dark"), ("uk", "light")] {
                try await shoot(state, language, theme)
            }
            try await shoot(state, "en", "light", full: true)
        }
        guard asked == nil else { return }
        for state in [DetailState.today, .setoff, .progress] {
            try await shoot(state, "cs", "light")
            try await shoot(state, "en", "light", size: small)
            try await shoot(state, "en", "light", size: small, end: true)
            try await shoot(state, "en", "light", large: true)
            try await shoot(state, "en", "light", large: true, end: true)
            try await shoot(state, "en", "light", large: true, full: true)
        }
        try await shoot(.progress, "uk", "light", large: true)
    }

    func testMockAlsoOpenRows() async throws {
        let also = MockCapture.Scroll.toolbar(L10n.Orders.alsoOpenThatDay)
        for theme in ["light", "dark"] {
            try await mockShot(.browse, "en", theme, scroll: also, suffix: "-also-open")
        }
    }

    // MARK: - Capture

    private func shoot(
        _ state: DetailState,
        _ language: String,
        _ theme: String,
        size: CGSize? = nil,
        large: Bool = false,
        full: Bool = false,
        end: Bool = false
    ) async throws {
        try use(language)
        let canvas = size ?? phone
        let height = full ? (large ? 4400 : 2600) : canvas.height
        let fixture = DetailFixture(state)
        let name = "detail-\(state.rawValue)-ios-\(Int(canvas.width))x\(Int(canvas.height))-\(language)-\(theme)"
            + (large ? "-ax1" : "") + (end ? "-end" : "") + (full ? "-full" : "")
        let statusBar: CGFloat = canvas.height == small.height ? 20 : 54
        await capture(name: name, size: CGSize(width: canvas.width, height: height), statusBar: statusBar, end: end) {
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

    private func use(_ language: String) throws {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
        CoreL10n.apply(languageTag: language)
    }

    /// The page runs under the status bar, as on a phone: its band is drawn behind a `statusBar` of safe area.
    private func capture(
        name: String,
        size: CGSize,
        statusBar: CGFloat,
        end: Bool,
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
        CaptureProbe.deviceInsets(host, in: window, homeIndicator: statusBar < 54 ? 0 : 34)
        host.additionalSafeAreaInsets.top += statusBar
        defer {
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        for _ in 0 ..< 16 {
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
        CaptureProbe.attach(window, name: name, to: self)
    }
}

/// The detail's states, by the design's own names; then states the design does not draw, kept as evidence.
enum DetailState: String, CaseIterable {
    case loading, error, browse, reserved, latejoin, far, contract, today, setoff, late, onway, lockout, reported
    case progress, progressDev = "progress-dev", ready, crew, crewWait = "crew-wait", crewDone = "crew-done", cash
    case completed, past, cancelled
    case cancelledLockout = "cancelled-lockout", norowLoading = "norow-loading", norowError = "norow-error"
    case norowBrowse = "norow-browse", removed
    case crewOpen = "crew-open", crewUnpaid = "crew-unpaid", cashCollected = "cash-collected"
    case setoffLoading = "setoff-loading", gone
}

/// One state's world: the job the server answers, her photos and rows, the board's store, the clock.
@MainActor
struct DetailFixture {
    let orderId: String
    let now: Date
    let client = FakePartnerOrderClient()
    let store = OrdersBoardStore()
    let offers: PendingOffersStore
    let checklist: CleaningChecklistStore
    var removal = false

    // swiftlint:disable:next function_body_length cyclomatic_complexity
    init(_ state: DetailState) {
        let fixture = OrdersDesignFixture.self
        let numbers = ["A1": "ORD-7F3A9C21", "A2": "ORD-91E2F0AB", "A3": "ORD-2B8E14D7"]
        let own = fixture.active().map { row in
            var row = row
            row.displayOrderNumber = row.id.flatMap { numbers[$0] }
            return row
        }
        let open = fixture.available()
        var orderId = "A1"
        var now = fixture.now
        var item = DetailJob.a1(status: 2)
        var photos: [OrderPhoto] = []
        var ticks = 0
        var withStore = true
        switch state {
        case .loading:
            client.suspendGetById = true
        case .setoffLoading:
            now = fixture.at(12, 13, 10)
            client.suspendGetById = true
        case .error:
            client.byIdResult = .failure(ApiError(code: "network.unreachable"))
        case .browse:
            orderId = "V4"
            item = DetailJob.notTaken(open[2], order: "ORD-4C7D8812")
        case .reserved:
            orderId = "R1"
            item = DetailJob.notTaken(open[4], order: "ORD-C41A0E93")
            client.pendingOffersResult = .success([fixture.offer])
        case .latejoin:
            orderId = "V1"
            item = DetailJob.notTaken(open[0], order: "ORD-E6B3D215", mates: ["Marta"])
        case .far:
            orderId = "A3"
            item = DetailJob.a3(status: 2)
        case .contract:
            orderId = "A3"
            item = DetailJob.a3(status: 2)
            item.workContractAcceptances = []
        case .today:
            break
        case .setoff:
            now = fixture.at(12, 13, 10)
        case .late:
            now = fixture.at(12, 14, 10)
        case .onway:
            now = fixture.at(12, 13, 40)
            item = DetailJob.a1(status: 3, history: [(3, fixture.at(12, 13, 22))])
        case .lockout, .reported:
            now = state == .lockout ? fixture.at(12, 14, 17) : fixture.at(12, 14, 25)
            item = DetailJob.a1(status: 3, history: [(3, fixture.at(12, 13, 22))])
            photos = [DetailJob.photo(._3, fixture.at(12, 14, 16))]
            if state == .reported {
                item.lockoutReportedAt = fixture.at(12, 14, 19)
                item.lockoutCallAttempts = DetailJob.calls
            }
        case .progress, .ready:
            now = state == .progress ? fixture.at(12, 15, 12) : fixture.at(12, 15, 49)
            item = DetailJob.a1(status: 4, history: DetailJob.a1Run)
            item.orderNotes = [DetailJob.note]
            item.orderIssues = [DetailJob.issue]
            photos = [DetailJob.photo(._1, fixture.at(12, 14, 1)), DetailJob.photo(._1, fixture.at(12, 14, 2))]
            ticks = 1
            if state == .ready {
                item.hasAfterPhotos = true
                photos += [DetailJob.photo(._2, fixture.at(12, 15, 46)), DetailJob.photo(._2, fixture.at(12, 15, 47))]
                ticks = 2
            }
        case .progressDev:
            orderId = "DV1"
            now = fixture.at(2, 9, 55)
            item = DetailJob.dev()
        case .crew, .crewWait, .crewDone, .crewUnpaid:
            orderId = "A2"
            now = fixture.at(13, 17, state == .crewDone || state == .crewUnpaid ? 50 : 46)
            let mate = state == .crewWait ? nil : fixture.at(13, 17, 41)
            let mine = state == .crew ? nil : fixture.at(13, 17, 44)
            item = DetailJob.a2(mine: mine, mate: mate)
            if state == .crewUnpaid { item.paymentStatus = Code(value: 1) }
            photos = [DetailJob.photo(._1, fixture.at(13, 15, 6)), DetailJob.photo(._2, fixture.at(13, 17, 38))]
            ticks = 1
        case .crewOpen:
            orderId = "A2"
            now = fixture.at(13, 17, 30)
            item = DetailJob.a2(mine: nil, mate: nil)
            item.hasAfterPhotos = false
            photos = [DetailJob.photo(._1, fixture.at(13, 15, 6))]
            ticks = 1
        case .cash, .cashCollected:
            orderId = "A3"
            now = fixture.at(16, 9, 48)
            item = DetailJob.a3(status: 4)
            item.hasAfterPhotos = true
            item.statusHistory = DetailJob.history([(3, fixture.at(16, 7, 25)), (4, fixture.at(16, 7, 57))])
            if state == .cashCollected { item.paymentStatus = Code(value: 2) }
            photos = [DetailJob.photo(._1, fixture.at(16, 8, 1)), DetailJob.photo(._2, fixture.at(16, 9, 41))]
            ticks = 2
        case .completed:
            now = fixture.at(12, 16, 20)
            ticks = 2
            item = DetailJob.a1(status: 5, history: DetailJob.a1Run + [(5, fixture.at(12, 15, 51))])
            item.completedAt = fixture.at(12, 15, 51)
            item.hasAfterPhotos = true
            item.orderNotes = [DetailJob.note]
            item.orderIssues = [DetailJob.issue]
            photos = [
                DetailJob.photo(._1, fixture.at(12, 14, 1)), DetailJob.photo(._1, fixture.at(12, 14, 2)),
                DetailJob.photo(._2, fixture.at(12, 15, 46)), DetailJob.photo(._2, fixture.at(12, 15, 47))
            ]
        case .past:
            orderId = "H7"
            item = DetailJob.h7()
            ticks = 1
            photos = [DetailJob.photo(._1, fixture.at(10, 9, 1)), DetailJob.photo(._2, fixture.at(10, 10, 44))]
        case .cancelled:
            orderId = "X1"
            item = DetailJob.x1()
        case .cancelledLockout:
            now = fixture.at(12, 14, 45)
            item = DetailJob.a1(status: 6, history: [(3, fixture.at(12, 13, 22)), (6, fixture.at(12, 14, 41))])
            item.systemCancellationReason = "order.cancelled.customer_lockout"
            item.address = nil
            item.customerName = ""
            item.customerPhone = ""
            item.lockoutReportedAt = fixture.at(12, 14, 19)
        case .norowLoading:
            orderId = "V4"
            withStore = false
            client.suspendGetById = true
        case .norowError, .gone:
            orderId = "V99"
            client.byIdResult = .failure(state == .gone
                ? ApiError(code: "order.not_found", httpStatus: 404)
                : ApiError(code: "network.unreachable"))
        case .norowBrowse:
            orderId = "V4"
            withStore = false
            item = DetailJob.notTaken(open[2], order: "ORD-4C7D8812")
        case .removed:
            orderId = "A2"
            now = fixture.at(12, 10, 5)
            removal = true
            client.byIdResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))
            client.removalReasonResult = .success(DetailJob.removedReason)
        }
        if case .success = client.byIdResult { client.byIdResult = .success(item) }
        client.getPhotosResult = .success(photos)
        client.pageResponder = { query in
            let rows = query.hasAvailableSpots == true ? open : own
            return .success(OrderPage(rows: rows, total: rows.count))
        }
        if withStore {
            store.publish(own: state == .cancelledLockout ? own.filter { $0.id != "A1" } : own)
            store.publish(available: open)
        }
        let defaults = UserDefaults(suiteName: "detail-capture-\(state.rawValue)") ?? .standard
        defaults.removePersistentDomain(forName: "detail-capture-\(state.rawValue)")
        let checklist = UserDefaultsCleaningChecklistStore(defaults: defaults)
        for id in OrdersDesignFixture.checklistIds(item).prefix(ticks) {
            checklist.setChecked(orderId: orderId, itemId: id, checked: true)
        }
        self.checklist = checklist
        offers = PendingOffersStore(client: client, ordersStaleness: OrdersStaleness())
        self.orderId = orderId
        self.now = now
    }
}

/// The design's jobs as `GetById` answers them for her (synthetic: no real customer, address or person).
enum DetailJob {
    static let calls = "Volala jsem v 14:02, 14:09 a 14:15, nikdo to nebere. SMS v 14:05 bez odpovědi."
    static let removedReason = "Zákazník přesunul úklid na sobotu ráno a v tu dobu nepracujete. Děkujeme za pochopení."
    static let note = OrderNoteDto(
        id: "note-1",
        employeeId: "emp-self",
        content: "The cat is shut in the bedroom, the window is closed.",
        createdOn: OrdersDesignFixture.at(12, 14, 40)
    )
    static let issue = OrderIssueDto(
        id: "issue-1",
        reportedByEmployeeId: "emp-self",
        description: "The socket by the bathroom mirror doesn’t work.",
        isResolved: false,
        createdOn: OrdersDesignFixture.at(12, 14, 52)
    )
    static var a1Run: [(Int, Date)] {
        [(3, OrdersDesignFixture.at(12, 13, 22)), (4, OrdersDesignFixture.at(12, 13, 58))]
    }

    static func history(_ stamps: [(Int, Date)]) -> [OrderStatusTrackDto] {
        stamps.map { OrderStatusTrackDto(status: Code(value: $0.0), createdOn: $0.1) }
    }

    static func photo(_ type: PhotoType, _ captured: Date) -> OrderPhoto {
        OrderPhoto(
            id: "\(type.rawValue)-\(captured.timeIntervalSince1970)",
            photoType: type,
            blobUrl: nil,
            capturedAt: captured
        )
    }

    static func from(_ row: OrderListItem, order: String, mine: Bool) -> OrderItem {
        var item = OrderItem.wireComplete()
        item.id = row.id
        item.displayOrderNumber = order
        item.orderStatus = row.orderStatus
        item.cleaningDateTime = row.cleaningDateTime
        item.estimatedTime = row.estimatedTime
        item.estimatedCleanerPay = row.estimatedCleanerPay
        item.currency = CurrencyDetailDto(code: "CZK", symbol: "Kč")
        item.customerAddressApproximate = row.customerAddressApproximate
        if mine, let street = OrdersBoardRules.streetLine(row.customerAddress) {
            item.address = OrderAddress(
                street: street,
                city: "Praha",
                zipCode: row.customerAddress?
                    .components(separatedBy: ", ").last,
                latitude: row.customerAddressLatitude ?? 50.0757,
                longitude: row.customerAddressLongitude ?? 14.4434
            )
            item.customerName = row.customerName
            item.customerPhone = row.customerPhone
        }
        item.rooms = row.rooms
        item.bathrooms = row.bathrooms
        item.extras = row.extras
        item.selectedServices = row.selectedServices?.map {
            ServiceDetails(id: $0.id, name: $0.name, translations: $0.translations)
        }
        item.paymentType = row.paymentType?.value == 1 ? row.paymentType : Code(value: 2)
        item.paymentStatus = row.paymentType?.value == 1 ? Code(value: 1) : Code(value: 2)
        item.amountDueOnCard = row.amountDueOnCard
        item.totalPrice = row.amountDueOnCard
        item.creditAppliedAmount = 0
        item.requiredEmployees = row.requiredEmployees
        item.availableSpots = max((row.requiredEmployees ?? 1) - (row.assignedEmployeesCount ?? 0), 0)
        item.hasAvailableSpots = (item.availableSpots ?? 0) > 0
        item.dirtinessLevel = row.dirtinessLevel
        item.isAssignedToCurrentUser = mine
        return item
    }

    static func notTaken(_ row: OrderListItem, order: String, mates: [String] = []) -> OrderItem {
        var item = from(row, order: order, mine: false)
        item.assignedEmployees = mates.enumerated().map {
            AssignedEmployeeDto(id: "seat-\($0.offset)", employeeId: "emp-\($0.element)", fullName: $0.element)
        }
        return item
    }

    private static func mine(
        _ row: OrderListItem,
        order: String,
        status: Int,
        accepted: Int,
        history: [(Int, Date)]
    ) -> OrderItem {
        var item = from(row, order: order, mine: true)
        item.orderStatus = Code(value: status)
        item.statusHistory = Self.history(history)
        item.assignedEmployees = [
            AssignedEmployeeDto(id: "seat-me", employeeId: "emp-self", fullName: "Oksana Melnyk")
        ]
        item.workContractAcceptances = [WorkContractAcceptanceDto(
            id: "acc-\(order)",
            orderEmployeeId: "seat-me",
            employeeId: "emp-self",
            acceptedOn: OrdersDesignFixture.at(accepted, 18, 0),
            documentVersion: "2026-10-27"
        )]
        return item
    }

    static func a1(status: Int, history: [(Int, Date)] = []) -> OrderItem {
        var item = mine(
            OrdersDesignFixture.active()[0],
            order: "ORD-7F3A9C21",
            status: status,
            accepted: 10,
            history: history
        )
        item.customerFloor = "3"
        item.customerApartment = "12"
        item.accessMode = "at_home"
        item.notes = "Máme kočku Micku, je přátelská. Prosím nenechávejte otevřené okno v ložnici."
        item.specialInstructions = "Čisticí prostředky jsou pod dřezem, vysavač ve skříni v předsíni. "
            + "Parkety jen vlhkým hadrem."
        item.accessInstructions = "Budu doma. Zvonek NOVÁKOVÁ, výtahem do 3. patra, byt 12 vlevo."
        return item
    }

    static func a2(mine: Date?, mate: Date?) -> OrderItem {
        var item = Self.mine(
            OrdersDesignFixture.active()[1],
            order: "ORD-91E2F0AB",
            status: 4,
            accepted: 8,
            history: [(3, OrdersDesignFixture.at(13, 14, 12)), (4, OrdersDesignFixture.at(13, 15, 2))]
        )
        item.hasAfterPhotos = true
        item.availableSpots = 0
        item.hasAvailableSpots = false
        item.customerFloor = "4"
        item.customerApartment = "9"
        item.accessMode = "door_code"
        item.notes = "Po rekonstrukci je všude jemný prach, hlavně na skříňkách v kuchyni."
        item.accessInstructions = "Kód ke vchodu 2580#, 4. patro bez výtahu, byt 9. Klíč od bytu je v boxu u dveří, "
            + "kód 1147."
        item.assignedEmployees = [
            AssignedEmployeeDto(
                id: "seat-lenka",
                employeeId: "emp-lenka",
                fullName: "Lenka Procházková",
                phoneNumber: "+420 605 318 772",
                completionConfirmedAt: mate
            ),
            AssignedEmployeeDto(
                id: "seat-me",
                employeeId: "emp-self",
                fullName: "Oksana Melnyk",
                completionConfirmedAt: mine
            )
        ]
        return item
    }

    static func a3(status: Int) -> OrderItem {
        var item = mine(
            OrdersDesignFixture.active()[2],
            order: "ORD-2B8E14D7",
            status: status,
            accepted: 11,
            history: []
        )
        item.customerFloor = "2"
        item.customerApartment = "5"
        item.accessMode = "keys_handover"
        item.notes = "Prosím vyčistěte i za lednicí, minule tam zůstal prach."
        item.specialInstructions = "Platím hotově, peníze nechám na stole v kuchyni."
        item.accessInstructions = "Klíče má sousedka paní Králová, 1. patro, byt 2 (zvonek KRÁLOVÁ). "
            + "Po úklidu jí je prosím vraťte."
        return item
    }

    /// Mike's DEV job as his screen showed it: a six-minute booking long past its end, nothing ticked, no photos, no
    /// notes and no way in, paid by card. The street, name and phone are stand-ins.
    static func dev() -> OrderItem {
        var row = OrdersDesignFixture.row("DV1", 2, (8, 15), 6, "complete", 1, 1, pay: 746.93, status: ._4)
        row.customerAddress = "Sokolovská 120, Praha, 18600"
        row.customerAddressApproximate = "Praha 8"
        row.customerName = "Jan Novák"
        row.customerPhone = "+420 600 123 456"
        return mine(
            row,
            order: "ORD-A1D496EB",
            status: 4,
            accepted: 1,
            history: [(3, OrdersDesignFixture.at(2, 9, 51)), (4, OrdersDesignFixture.at(2, 9, 52))]
        )
    }

    static func x1() -> OrderItem {
        var row = OrdersDesignFixture.row("X1", 12, (11, 0), 120, "standard", 2, 1, pay: 654, status: ._6)
        row.customerAddressApproximate = "Praha 2"
        var item = from(row, order: "ORD-3D5B9A64", mine: false)
        item.isAssignedToCurrentUser = true
        item.statusHistory = history([(6, OrdersDesignFixture.at(12, 8, 10))])
        return item
    }

    static func h7() -> OrderItem {
        var row = OrdersDesignFixture.row("H7", 10, (9, 0), 120, "windows", 2, 2, pay: 690, status: ._5)
        row.customerAddressApproximate = "Praha 9"
        row.dirtinessLevel = ._1
        var item = from(row, order: "ORD-8A1F2C40", mine: false)
        item.isAssignedToCurrentUser = true
        item.hasAfterPhotos = true
        item.completedAt = OrdersDesignFixture.at(10, 10, 52)
        item.statusHistory = history([
            (3, OrdersDesignFixture.at(10, 8, 14)), (4, OrdersDesignFixture.at(10, 8, 57)),
            (5, OrdersDesignFixture.at(10, 10, 52))
        ])
        item.assignedEmployees = [AssignedEmployeeDto(id: "seat-me", employeeId: "emp-self", fullName: "Oksana")]
        item.workContractAcceptances = [WorkContractAcceptanceDto(
            id: "acc-h7",
            orderEmployeeId: "seat-me",
            employeeId: "emp-self",
            acceptedOn: OrdersDesignFixture.at(9, 18, 0),
            documentVersion: "2026-10-27"
        )]
        return item
    }
}

extension PartnerOrdersVisualTests {
    /// The Job taken panel after the detail's take: its facts are the order's, read back as hers.
    func testMockDetailTakeEndsOnThePanel() async throws {
        let browsed = OrdersDesignFixture.available()[2]
        var hers = DetailJob.from(
            OrdersDesignFixture.taken(browsed).taken ?? browsed,
            order: "ORD-4C7D8812",
            mine: true
        )
        hers.orderStatus = Code(value: 2)
        detailTake = try TakenJob(detail: OrderDetail(hers), readBack: true)
        let contract = OrdersDesignFixture.contract(for: browsed)
        mapSettle = 5
        for theme in ["light", "dark"] {
            try await sheet("taken-detail", "en", theme, state: .taken(contract), row: browsed)
        }
        mapSettle = 0
        detailTake = nil
    }
}
