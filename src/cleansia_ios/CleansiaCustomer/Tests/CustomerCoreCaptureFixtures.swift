import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

// MARK: - Sample data (synthetic: no real customer, address or person)

/// Fixed dates, names and amounts, so a capture taken later reproduces this one pixel for pixel. Upcoming orders
/// sit in 2027, well after any capture, and finished ones in October 2026.
enum CustomerCaptureFixture {
    static func at(_ year: Int, _ month: Int, _ day: Int, _ hour: Int, _ minute: Int) -> Date {
        Calendar.current.date(from: DateComponents(year: year, month: month, day: day, hour: hour, minute: minute))
            ?? Date(timeIntervalSince1970: 0)
    }

    static let upcoming = at(2027, 3, 18, 10, 0)
    static let finished = at(2026, 10, 13, 9, 0)
    static let longDate = at(2027, 9, 29, 10, 30)
    static let address = OrderAddress(
        street: "Zenklova 6",
        city: "Praha 8",
        zipCode: "180 00",
        country: "Czech Republic",
        latitude: 50.1067,
        longitude: 14.4724
    )

    static func order(status: Int, date: Date? = nil) -> CustomerOrderDetail {
        let finishedOrder = status >= 5
        let when = date ?? (finishedOrder ? finished : upcoming)
        let cleaner = AssignedEmployeeDto(id: "seat-1", employeeId: "emp-1", fullName: "Lenka Procházková")
        return OrderFixtures.detail(
            id: "order-1",
            countryId: "cze",
            statusCode: Code(type: "OrderStatus", name: nil, value: status),
            displayOrderNumber: "ORD-7F3A9C21",
            cleaningDateTime: when,
            completedAt: status == 5 ? when.addingTimeInterval(3 * 3600 + 600) : nil,
            receiptNumber: status == 5 ? "R-2026-0142" : nil,
            address: address,
            rooms: 3,
            bathrooms: 2,
            estimatedMinutes: 180,
            extras: ["inside-oven": true, "interior-windows": true, "inside-fridge": false],
            services: [OrderFixtures.service(id: "s-1", name: "Standard clean", estimatedMinutes: 150)],
            packages: [OrderFixtures.package(
                id: "p-1",
                name: "Kitchen refresh",
                price: 450,
                estimatedMinutes: 30,
                currencyCode: "CZK",
                includedServices: ["Oven", "Hob", "Extractor hood"]
            )],
            notes: "The cat is shy, please keep the bedroom door closed.",
            specialInstructions: "Cleaning products are under the sink.",
            accessInstructions: "Ring NOVÁKOVÁ, lift to the 3rd floor, flat 12 on the left.",
            total: 1962,
            originalSubtotal: 2180,
            promoDiscountAmount: 218,
            creditAppliedAmount: 150,
            appliedDiscountSource: ._3,
            paymentType: Code(value: 2),
            paymentStatus: Code(value: 2),
            currencyCode: "CZK",
            assignedEmployees: status == 2 || status == 5 ? [cleaner] : [],
            statusHistory: history(status, when),
            review: status == 5
                ? CustomerOrderReview(rating: 5, comment: "Spotless kitchen, thank you.", tags: [], lines: [])
                : nil,
            preferredOffer: status == 2
                ? PreferredOfferDetails(state: ._2, cleanerName: "Lenka", respondByUtc: nil, canChooseAnother: false)
                : nil,
            systemCancellationReason: status == 6 ? "order.cancelled.no_cleaner_available" : nil,
            freeCancellationHours: 24
        )
    }

    private static func history(_ status: Int, _ when: Date) -> [OrderStatusTrackDto] {
        let booked = when.addingTimeInterval(-6 * 86400)
        let steps: [(Int, TimeInterval)] = switch status {
        case 2: [(0, 0), (2, 3600)]
        case 5: [(0, 0), (2, 3600), (3, 6 * 86400 - 1500), (4, 6 * 86400 + 300), (5, 6 * 86400 + 11400)]
        case 6: [(0, 0), (6, 5 * 86400)]
        default: [(0, 0)]
        }
        return steps.map { OrderFixtures.track(statusValue: $0.0, createdOn: booked.addingTimeInterval($0.1)) }
    }

    private struct Row {
        let number: String
        let status: Int
        let date: Date
        let total: Double
        let service: String
    }

    static func summaries() -> [CustomerOrderSummary] {
        let rows = [
            Row(number: "ORD-7F3A9C21", status: 0, date: upcoming, total: 1962, service: "Standard clean"),
            Row(number: "ORD-91E2F0AB", status: 2, date: at(2027, 3, 11, 14, 0), total: 2480, service: "Deep clean"),
            Row(number: "ORD-2B8E14D7", status: 3, date: at(2027, 3, 4, 8, 30), total: 1390, service: "Windows"),
            Row(number: "ORD-4C7D8812", status: 4, date: at(2027, 2, 25, 9, 0), total: 3150, service: "Move-out clean"),
            Row(number: "ORD-8A1F2C40", status: 5, date: finished, total: 1962, service: "Standard clean"),
            Row(number: "ORD-3D5B9A64", status: 6, date: at(2026, 10, 2, 11, 0), total: 1240, service: "Standard clean")
        ]
        return rows.enumerated().map { index, row in
            OrderFixtures.summary(
                id: "order-\(index + 1)",
                countryId: "cze",
                statusCode: Code(type: "OrderStatus", name: nil, value: row.status),
                displayOrderNumber: row.number,
                cleaningDateTime: row.date,
                estimatedMinutes: 180,
                address: "Zenklova 6, Praha 8",
                total: row.total,
                currencyCode: "CZK",
                services: [CustomerOrderLineName(name: row.service, translations: nil)]
            )
        }
    }

    // MARK: - The booking at its review step

    @MainActor
    static func booking(cashCardCheckFails: Bool = false) async -> BookingViewModel {
        let category = CatalogCategory(
            id: "c", slug: "home", name: "Home", description: nil, displayOrder: 0, translations: [:]
        )
        let catalog = Catalog(
            services: [CatalogService(
                id: "s-1", name: "Standard clean", description: nil, basePrice: 500, perRoomPrice: 150,
                category: category, translations: [:]
            )],
            packages: [], currencyCode: "CZK", defaultCurrencyCode: "CZK"
        )
        let oven = CatalogExtra(
            id: "e-1", slug: "inside-oven", name: "Inside oven cleaning", description: nil, price: 200,
            displayOrder: 1, translations: [:]
        )
        let cards: ApiResult<[SavedCard]> = cashCardCheckFails
            ? .failure(ApiError(httpStatus: 500))
            : .success([PaymentsFixtures.czkCard])
        let vm = BookingViewModel(
            catalogClient: FakeCatalogClient(result: .success(catalog)),
            quoteClient: FakeQuoteClient(result: .success(BookingQuote(
                totalPrice: 1150, currencyId: "cur-czk", currencyCode: "CZK", requiredEmployees: 1,
                estimatedMinutes: 180, creditBalance: 0, creditMaxShareOfOrder: 0.7
            ))),
            extraClient: FakeExtraClient(result: .success([oven])),
            profileClient: FakeProfileClient(),
            orderCreateClient: FakeOrderCreateClient(
                result: .success(CreatedOrder(id: "o-1", confirmationCode: "130AA4"))
            ),
            paymentIntentClient: FakePaymentIntentClient(),
            countryResolver: FakeCountryResolver(),
            consentClient: FakeConsentStatusClient(granted: [.termsOfService, .privacyPolicy]),
            tokenStore: FakeTokenStore.signedIn(),
            isCardPaymentAvailable: true,
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler(),
            savedCardClient: FakeSavedCardClient(reads: [cards])
        )
        await vm.loadCatalog()
        await vm.loadExtras()
        vm.update { _ in
            var draft = BookingState()
            draft.selectedServiceIds = ["s-1"]
            draft.selectedExtraSlugs = ["inside-oven"]
            draft.rooms = 2
            draft.bathrooms = 1
            draft.dirtiness = .normal
            draft.street = "Zenklova 6"
            draft.city = "Praha 8"
            draft.zipCode = "18000"
            draft.countryIsoCode = "cz"
            draft.selectedDate = "Thursday, 18 March"
            draft.selectedTime = "10:00"
            draft.selectedInstant = upcoming
            draft.paymentMethod = cashCardCheckFails ? .cash : .card
            draft.earlyPerformanceRequested = true
            return draft
        }
        await vm.refreshQuoteForTest()
        if cashCardCheckFails { _ = await vm.refreshCashCard() }
        while vm.advance() {}
        return vm
    }
}

// MARK: - Picker stand-ins

final class FixedGeocodingService: GeocodingService, @unchecked Sendable {
    static let picked = GeocodedAddress(
        latitude: 50.1067,
        longitude: 14.4724,
        street: "Zenklova 6",
        city: "Praha 8",
        zipCode: "180 00",
        country: "Czech Republic",
        countryIsoCode: "CZ",
        formatted: "Zenklova 6, 180 00 Praha 8"
    )

    func reverseGeocode(_: Coordinate) async -> GeocodedAddress? {
        Self.picked
    }

    func forwardGeocode(query _: String, countryIsoCodes _: [String]) async -> [GeocodedAddress] {
        []
    }
}

@MainActor
final class FixedLocationProvider: LocationProvider {
    var authorizationStatus: LocationAuthorizationStatus {
        .authorized
    }

    func requestWhenInUseAuthorization() async -> LocationAuthorizationStatus {
        .authorized
    }

    func currentLocation() async -> Coordinate? {
        Coordinate(latitude: 50.1067, longitude: 14.4724)
    }
}

// MARK: - Capture

/// Hosts a view in a window of the frame's size, waits for it to settle, and attaches what it drew (PNG) and
/// what VoiceOver would read (JSON, `<name>.semantics`). The lane exports the attachments by name.
@MainActor
enum CustomerCapture {
    static func shoot(
        _ name: String,
        size: CGSize,
        to test: XCTestCase,
        hold: TimeInterval = 0,
        crops: [(suffix: String, rect: CGRect)] = [],
        end: Bool = false,
        inspect: (UIWindow) -> Void = { _ in },
        @ViewBuilder content: () -> some View
    ) async {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        let previous = scenes.flatMap(\.windows).first { $0.isKeyWindow }
        let host = UIHostingController(rootView: content())
        if #available(iOS 16.4, *) { host.safeAreaRegions = [] }
        let window = scenes.first.map(UIWindow.init(windowScene:)) ?? UIWindow(frame: CGRect(origin: .zero, size: size))
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
        defer {
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        await settle(host.view, rounds: 16)
        if hold > 0 { try? await Task.sleep(nanoseconds: UInt64(hold * 1_000_000_000)) }
        await settle(host.view, rounds: 6)
        if end {
            scrollToEnd(in: host.view)
            await settle(host.view, rounds: 6)
        }
        inspect(window)
        attach(window, name: name, crops: crops, to: test)
    }

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

    static func settle(_ view: UIView, rounds: Int) async {
        for _ in 0 ..< rounds {
            try? await Task.sleep(nanoseconds: 60_000_000)
            view.layoutIfNeeded()
        }
    }

    static func attach(_ window: UIWindow, name: String, crops: [(suffix: String, rect: CGRect)], to test: XCTestCase) {
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
        add(image, name: name, to: test)
        for crop in crops {
            let scale = image.scale
            let pixels = CGRect(
                x: crop.rect.minX * scale,
                y: crop.rect.minY * scale,
                width: crop.rect.width * scale,
                height: crop.rect.height * scale
            )
            if let cropped = image.cgImage?.cropping(to: pixels) {
                add(UIImage(cgImage: cropped, scale: scale, orientation: .up), name: name + crop.suffix, to: test)
            }
        }
        let tree = XCTAttachment(data: semantics(of: window), uniformTypeIdentifier: "public.json")
        tree.name = name + ".semantics"
        tree.lifetime = .keepAlways
        test.add(tree)
    }

    private static func add(_ image: UIImage, name: String, to test: XCTestCase) {
        let picture = XCTAttachment(image: image)
        picture.name = name
        picture.lifetime = .keepAlways
        test.add(picture)
    }

    /// Every element VoiceOver reaches, with its frame in the window's points.
    static func elements(of window: UIWindow) -> [(label: String, frame: CGRect)] {
        var found: [(String, CGRect)] = []
        func visit(_ node: NSObject, _ depth: Int) {
            guard depth < 80 else { return }
            if node.isAccessibilityElement {
                found.append((node.accessibilityLabel ?? "", node.accessibilityFrame))
                return
            }
            if let children = node.accessibilityElements as? [NSObject], !children.isEmpty {
                children.forEach { visit($0, depth + 1) }
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

    private static func semantics(of window: UIWindow) -> Data {
        let nodes = elements(of: window).map { element -> [String: Any] in
            let frame = element.frame
            return [
                "label": element.label,
                "frame": [frame.minX, frame.minY, frame.width, frame.height].map { ($0 * 10).rounded() / 10 }
            ]
        }
        return (try? JSONSerialization.data(withJSONObject: nodes, options: [.prettyPrinted, .sortedKeys])) ?? Data()
    }
}
