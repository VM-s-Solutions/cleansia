import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// Captures of the customer screens that draw a shared component, from fixed sample data, so the same frames taken
/// after a shared component changes can be compared with these pixel for pixel. Each attachment is named
/// `<screen>-<state>-ios-<w>x<h>-<lang>-<theme>[-<text size>][-full]`; the lane exports them from the result bundle.
/// They are evidence, not proofs, so they run only when asked for (`TEST_RUNNER_CUSTOMER_CORE_CAPTURE=1`).
@MainActor
final class CustomerCoreCaptureTests: XCTestCase {
    static let small = CGSize(width: 375, height: 667)
    static let phone = CGSize(width: 393, height: 852)
    static let wide = CGSize(width: 440, height: 956)
    static let narrow = CGSize(width: 320, height: 568)

    private var restoreBundle: Bundle!
    private var restoreTag: String!

    override func setUpWithError() throws {
        try super.setUpWithError()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
        try XCTSkipUnless(
            ProcessInfo.processInfo.environment["CUSTOMER_CORE_CAPTURE"] == "1",
            "captures run on request"
        )
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    func use(_ language: String) throws {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
        CoreL10n.apply(languageTag: language)
    }

    func name(
        _ screen: String,
        _ state: String,
        _ size: CGSize,
        _ language: String,
        _ theme: String,
        text: DynamicTypeSize = .large,
        full: Bool = false
    ) -> String {
        "\(screen)-\(state)-ios-\(Int(size.width))x\(Int(size.height))-\(language)-\(theme)"
            + (text == .large ? "" : "-\(Self.textLabel(text))") + (full ? "-full" : "")
    }

    static func textLabel(_ size: DynamicTypeSize) -> String {
        switch size {
        case .xxxLarge: "xxxl"
        case .accessibility1: "ax1"
        case .accessibility3: "ax3"
        default: "\(size)"
        }
    }

    // MARK: - The booking footer and its slide

    func testBookingFooter() async throws {
        var rows: [[String: Any]] = []
        for (language, theme) in [("en", "light"), ("en", "dark"), ("uk", "light"), ("uk", "dark")] {
            for size in [Self.small, Self.phone, Self.wide] {
                try await bookingReview(language, theme, size) { rows.append($0) }
            }
        }
        for language in ["cs", "sk", "ru"] {
            for size in [Self.small, Self.phone] {
                try await bookingReview(language, "light", size) { rows.append($0) }
            }
        }
        try use("en")
        let failed = await CustomerCaptureFixture.booking(cashCardCheckFails: true)
        let tall = CGSize(width: Self.phone.width, height: 2000)
        await CustomerCapture.shoot(
            name("booking", "review-cash-cardcheck", Self.phone, "en", "light", full: true),
            size: tall,
            to: self,
            hold: 0.5
        ) {
            bookingSheet(failed, size: tall, theme: "light", language: "en", homeIndicator: 34)
        }
        try attachMeasurement(rendered: rows)
    }

    private func bookingReview(
        _ language: String,
        _ theme: String,
        _ size: CGSize,
        record: ([String: Any]) -> Void
    ) async throws {
        try use(language)
        let vm = await CustomerCaptureFixture.booking()
        let indicator: CGFloat = size == Self.small ? 0 : 34
        let footer = CGRect(x: 0, y: size.height - indicator - 96, width: size.width, height: 96 + indicator)
        let label = SlideLabelMeasure.label(language: language)
        var found: CGRect?
        await CustomerCapture.shoot(
            name("booking", "review", size, language, theme),
            size: size,
            to: self,
            hold: 0.5,
            crops: [("-footer", footer)],
            inspect: { window in
                found = CustomerCapture.elements(of: window).first { $0.label == label }?.frame
            },
            content: { bookingSheet(vm, size: size, theme: theme, language: language, homeIndicator: indicator) }
        )
        guard theme == "light" else { return }
        var row = SlideLabelMeasure.row(label: label, language: language, width: size.width)
        row["renderedLabelFrame"] = found.map { [$0.minX, $0.minY, $0.width, $0.height] } ?? NSNull()
        record(row)
    }

    /// The sheet as presented at its large detent, over the dimmed screen, with the home indicator's band under it.
    private func bookingSheet(
        _ vm: BookingViewModel,
        size: CGSize,
        theme: String,
        language: String,
        homeIndicator: CGFloat,
        text: DynamicTypeSize = .large
    ) -> some View {
        ZStack(alignment: .bottom) {
            Color.black.opacity(0.4)
            VStack(spacing: 0) {
                BookingSheetView(
                    vm: vm,
                    geocoding: FixedGeocodingService(),
                    mapProvider: PreviewMapProvider(),
                    paymentSheet: FakePaymentSheetPresenter(),
                    orderClient: FakeOrderClient(),
                    onDismiss: {}
                )
                CleansiaColors.background.frame(height: homeIndicator)
            }
            .frame(width: size.width, height: size.height - 62)
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
        .frame(width: size.width, height: size.height)
        .background(CleansiaColors.background)
        .environment(\.locale, Locale(identifier: language))
        .preferredColorScheme(theme == "dark" ? .dark : .light)
        .dynamicTypeSize(text)
    }

    /// The label's own width against the track's, in every language and on four phone widths: the label as
    /// captured (1 150 Kč), the same total in the language's region, a five-digit total and the label with no total.
    private func attachMeasurement(rendered: [[String: Any]]) throws {
        var computed: [[String: Any]] = []
        for language in SlideLabelMeasure.languages {
            try use(language)
            for (total, label) in SlideLabelMeasure.labels(language: language) {
                for width in SlideLabelMeasure.widths {
                    var row = SlideLabelMeasure.row(label: label, language: language, width: width)
                    row["total"] = total
                    computed.append(row)
                }
            }
        }
        let report: [String: Any] = [
            "rendered": rendered,
            "computed": computed,
            "geometry": SlideLabelMeasure.geometry
        ]
        let data = try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys])
        let attachment = XCTAttachment(data: data, uniformTypeIdentifier: "public.json")
        attachment.name = "slide-label-measurement"
        attachment.lifetime = .keepAlways
        add(attachment)
        for row in rendered + computed {
            print("[measure] \(row)")
        }
    }

    // MARK: - The booking's dirtiness step

    /// No level and each level picked, at the small phone in two languages and both themes, then at two larger text
    /// sizes; each with the step's scroll content against its viewport and where Continue sits.
    func testBookingDirtiness() async throws {
        let levels: [(String, Dirtiness?)] = [
            ("none", nil),
            ("normal", .normal),
            ("increased", .increased),
            ("heavy", .heavy)
        ]
        var rows: [[String: Any]] = []
        for language in ["en", "uk"] {
            for theme in ["light", "dark"] {
                for (state, level) in levels {
                    try await rows.append(dirtinessStep(language, theme, state, level, .large))
                }
            }
        }
        for text in [DynamicTypeSize.xxxLarge, .accessibility1] {
            try await rows.append(dirtinessStep("en", "light", "normal", .normal, text))
        }
        let data = try JSONSerialization.data(withJSONObject: rows, options: [.prettyPrinted, .sortedKeys])
        let attachment = XCTAttachment(data: data, uniformTypeIdentifier: "public.json")
        attachment.name = "dirtiness-step-measurement"
        attachment.lifetime = .keepAlways
        add(attachment)
        for row in rows {
            print("[measure] \(row)")
        }
    }

    private func dirtinessStep(
        _ language: String,
        _ theme: String,
        _ state: String,
        _ level: Dirtiness?,
        _ text: DynamicTypeSize
    ) async throws -> [String: Any] {
        try use(language)
        let vm = await CustomerCaptureFixture.booking()
        vm.back()
        vm.back()
        vm.update { current in
            var next = current
            next.dirtiness = level
            return next
        }
        let size = Self.small
        var row: [String: Any] = ["language": language, "theme": theme, "state": state, "text": Self.textLabel(text)]
        await CustomerCapture.shoot(
            name("booking", "dirtiness-\(state)", size, language, theme, text: text),
            size: size,
            to: self,
            hold: 0.3,
            inspect: { window in
                let scroll = Self.scrollViews(in: window).max { $0.bounds.height < $1.bounds.height }
                if let scroll {
                    let inset = scroll.adjustedContentInset
                    let viewport = scroll.bounds.height - inset.top - inset.bottom
                    let frame = scroll.convert(scroll.bounds, to: window)
                    row["viewportHeight"] = viewport
                    row["contentHeight"] = scroll.contentSize.height
                    row["scrolls"] = scroll.contentSize.height > viewport + 0.5
                    row["viewportFrame"] = [frame.minX, frame.minY, frame.width, frame.height]
                }
                let action = CustomerCapture.elements(of: window).first {
                    $0.label.hasPrefix(L10n.Booking.continueAction)
                }?.frame
                row["continueFrame"] = action.map { [$0.minX, $0.minY, $0.width, $0.height] } ?? NSNull()
                row["continueVisible"] = action.map { $0.minY >= 0 && $0.maxY <= window.bounds.height } ?? false
            },
            content: {
                bookingSheet(vm, size: size, theme: theme, language: language, homeIndicator: 0, text: text)
            }
        )
        return row
    }

    private static func scrollViews(in view: UIView) -> [UIScrollView] {
        ((view as? UIScrollView).map { [$0] } ?? []) + view.subviews.flatMap { scrollViews(in: $0) }
    }

    // MARK: - The order detail

    func testOrderDetail() async throws {
        for status in [0, 2, 5, 6] {
            for theme in ["light", "dark"] {
                try await detail(status: status, "en", theme, Self.phone)
            }
        }
        try await detail(status: 2, "uk", "light", Self.phone)
        for status in [2, 5] {
            for theme in ["light", "dark"] {
                try await detail(status: status, "en", theme, Self.phone, full: true)
                try await detail(status: status, "en", theme, Self.phone, full: true, end: true)
            }
        }
        var dates: [[String: Any]] = []
        for (language, text, theme) in [
            ("en", DynamicTypeSize.large, "light"),
            ("en", .accessibility1, "light"),
            ("en", .accessibility1, "dark"),
            ("ru", .xxxLarge, "light"),
            ("ru", .accessibility1, "light")
        ] {
            try await detail(
                status: 0,
                language,
                theme,
                Self.narrow,
                text: text,
                date: CustomerCaptureFixture.longDate,
                state: "longdate"
            )
            dates.append(CalendarLineMeasure.row(language: language, width: Self.narrow.width, text: text))
        }
        let data = try JSONSerialization.data(withJSONObject: dates, options: [.prettyPrinted, .sortedKeys])
        let attachment = XCTAttachment(data: data, uniformTypeIdentifier: "public.json")
        attachment.name = "calendar-line-measurement"
        attachment.lifetime = .keepAlways
        add(attachment)
        for row in dates {
            print("[measure-date] \(row)")
        }
    }

    private func detail(
        status: Int,
        _ language: String,
        _ theme: String,
        _ size: CGSize,
        text: DynamicTypeSize = .large,
        date: Date? = nil,
        state: String? = nil,
        full: Bool = false,
        end: Bool = false
    ) async throws {
        try use(language)
        let markets = await MarketFixtures.resolved()
        let states = [0: "new", 2: "confirmed", 5: "completed", 6: "cancelled"]
        let order = CustomerCaptureFixture.order(status: status, date: date)
        let canvas = full ? CGSize(width: size.width, height: 2600) : size
        let statusBar: CGFloat = size == Self.phone || size == Self.wide ? 54 : 20
        await CustomerCapture.shoot(
            name("detail", state ?? states[status] ?? "\(status)", size, language, theme, text: text, full: full)
                + (end ? "-end" : ""),
            size: canvas,
            to: self,
            hold: status == 2 ? 4 : 0.6,
            end: end
        ) {
            VStack(spacing: 0) {
                (theme == "dark" ? CleansiaColors.background : CleansiaColors.surfaceVariant)
                    .frame(height: statusBar)
                NavigationStack(path: .constant(["order-1"])) {
                    Color.clear
                        .toolbar(.hidden, for: .navigationBar)
                        .navigationDestination(for: String.self) { id in
                            Self.detailScreen(order, id: id, markets: markets)
                        }
                }
            }
            .frame(width: canvas.width, height: canvas.height)
            .tint(CleansiaColors.primaryText)
            .environment(\.locale, Locale(identifier: language))
            .preferredColorScheme(theme == "dark" ? .dark : .light)
            .dynamicTypeSize(text)
        }
    }

    private static func detailScreen(_ order: CustomerOrderDetail, id: String, markets: MarketStore) -> some View {
        let client = FakeOrderClient()
        client.detailResults = [.success(order)]
        client.photosResults = [.success(OrderPhotos(photos: [], beforeCount: 0, afterCount: 0))]
        return OrderDetailView(
            orderId: id,
            client: client,
            repository: OrderRepository(client: client),
            membershipRepository: MembershipRepository(client: FakeMembershipManagementClient()),
            marketStore: markets,
            snackbar: SnackbarController(),
            eventBus: OrderEventBus(),
            paymentSheet: FakePaymentSheetPresenter(),
            mapProvider: PreviewMapProvider(),
            onReportIssue: { _ in },
            onReportCleanerNoShow: { _ in },
            onRebook: { _ in },
            onCreditMoved: {},
            onMakeRecurring: { _ in }
        )
    }

    // MARK: - The address picker and its round buttons

    func testAddressPicker() async throws {
        for (language, theme, size) in [
            ("en", "light", Self.phone), ("en", "dark", Self.phone), ("uk", "light", Self.phone),
            ("en", "light", Self.small)
        ] {
            try use(language)
            await CustomerCapture.shoot(
                name("picker", "resolved", size, language, theme),
                size: size,
                to: self,
                hold: 1.2
            ) {
                NavigationStack {
                    BookingAddressPickerView(
                        geocoding: FixedGeocodingService(),
                        mapProvider: PreviewMapProvider(),
                        onConfirmed: { _ in },
                        onBack: {}
                    )
                }
                .frame(width: size.width, height: size.height)
                .tint(CleansiaColors.primaryText)
                .environment(\.locationProvider, FixedLocationProvider())
                .environment(\.locale, Locale(identifier: language))
                .preferredColorScheme(theme == "dark" ? .dark : .light)
                .dynamicTypeSize(.large)
            }
        }
    }
}

// MARK: - Measuring the slide's label

/// The review step's slide is the track the footer leaves (the width less 20 a side): a 48 thumb 4 in from its
/// leading end, and the label centred over the whole track. A centred label reaches under the thumb once it is
/// wider than the track less 104, and lanes keeping 60 clear at each end would change a render once it is wider
/// than the track less 120.
@MainActor
enum SlideLabelMeasure {
    static let languages = ["en", "cs", "sk", "uk", "ru"]
    static let widths: [CGFloat] = [360, 375, 393, 440]
    static let geometry: [String: Any] = [
        "footerPadding": Spacing.ml, "thumbInset": 4, "thumb": 48, "trackHeight": 56, "laneClearance": 60,
        "minimumScaleFactor": 0.85, "font": "labelLarge (Nunito Bold 14)"
    ]

    static func label(language _: String) -> String {
        L10n.Booking.slideToConfirmPrice(BookingPricing.formatTotal(1150, currencyCode: "CZK"))
    }

    /// The total is formatted in the handset's region, as the footer formats it; the regional row formats it in
    /// the language's own region instead.
    static func labels(language: String) -> [(String, String)] {
        let regional = OrdersFormat.price(1150, currencyCode: "CZK", locale: Locale(identifier: language))
        return [
            ("1150", L10n.Booking.slideToConfirmPrice(BookingPricing.formatTotal(1150, currencyCode: "CZK"))),
            ("1150-regional", L10n.Booking.slideToConfirmPrice(regional)),
            ("12480", L10n.Booking.slideToConfirmPrice(BookingPricing.formatTotal(12480, currencyCode: "CZK"))),
            ("none", L10n.Booking.slideToConfirm)
        ]
    }

    static func idealWidth(_ label: String, language: String) -> CGFloat {
        let host = UIHostingController(rootView: Text(label)
            .font(CleansiaTypography.labelLarge)
            .lineLimit(1)
            .fixedSize()
            .environment(\.locale, Locale(identifier: language))
            .dynamicTypeSize(.large))
        return host.sizeThatFits(in: CGSize(width: 10000, height: 1000)).width
    }

    static func row(label: String, language: String, width: CGFloat) -> [String: Any] {
        let ideal = idealWidth(label, language: language)
        let track = width - Spacing.ml * 2
        let scale = ideal > track ? max(track / ideal, 0.85) : 1
        let drawn = min(ideal * scale, track)
        let leading = (track - drawn) / 2
        return [
            "language": language,
            "width": width,
            "label": label,
            "labelWidth": (ideal * 10).rounded() / 10,
            "track": track,
            "trackLess120": track - 120,
            "trackLess104": track - 104,
            "shrunkToday": ideal > track,
            "scaleToday": (scale * 1000).rounded() / 1000,
            "truncatedToday": ideal * 0.85 > track,
            "leadingGapToday": (leading * 10).rounded() / 10,
            "underThumbToday": leading < 52,
            "lanesWouldChangeIt": ideal > track - 120
        ]
    }
}

/// The detail header's date line: the calendar glyph, the gap and the date, against the room the header leaves it
/// (the width less 20 a side and the 80 the mascot keeps).
@MainActor
enum CalendarLineMeasure {
    static func row(language: String, width: CGFloat, text: DynamicTypeSize) -> [String: Any] {
        let date = OrdersFormat.dateTime(CustomerCaptureFixture.longDate, locale: Locale(identifier: language))
        let host = UIHostingController(rootView: Label(date, systemImage: "calendar")
            .font(CleansiaTypography.bodyMedium)
            .lineLimit(1)
            .fixedSize()
            .environment(\.locale, Locale(identifier: language))
            .dynamicTypeSize(text))
        let ideal = host.sizeThatFits(in: CGSize(width: 10000, height: 1000)).width
        let room = width - Spacing.ml * 2 - 80
        return [
            "language": language,
            "width": width,
            "textSize": CustomerCoreCaptureTests.textLabel(text),
            "date": date,
            "lineWidth": (ideal * 10).rounded() / 10,
            "room": room,
            "shrunkToday": ideal > room,
            "scaleToday": ideal > room ? (max(room / ideal, 0.8) * 1000).rounded() / 1000 : 1
        ]
    }
}
