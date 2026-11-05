import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// The figure standing on the detail's head, by the state the detail shows, and the head it stands
/// on captured in uk at 375 pt, light and dark, large and AX3.
final class OrderDetailMascotArtTests: XCTestCase {
    @MainActor
    func testPartnerMobilePolishVisualEvidence() async throws {
        let previous = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        let scene = try XCTUnwrap(previous?.windowScene)
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        let localization = try XCTUnwrap(Bundle.main.url(forResource: "uk", withExtension: "lproj"))
        L10n.bundle = try XCTUnwrap(Bundle(url: localization))
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 2)
        item.isAssignedToCurrentUser = true
        item.cleaningDateTime = Date(timeIntervalSinceNow: 4 * 3600)
        item.estimatedTime = 120
        item.estimatedCleanerPay = 779
        item.currency = CurrencyDetailDto(code: "CZK", symbol: "Kč")
        item.address = OrderAddress(street: "Vinohradská 46", city: "Praha", zipCode: "12000")
        item.customerAddressApproximate = "Praha 2"
        item.customerName = "Jana Nováková"
        let order = try OrderDetail(item)
        for textSize in [DynamicTypeSize.large, .accessibility3] {
            for scheme in [ColorScheme.light, .dark] {
                let host = UIHostingController(rootView:
                    OrderDetailContent(
                        order: order,
                        checklistVM: .preview,
                        notesVM: .preview,
                        photosVM: .preview
                    )
                    .padding(.top, 64)
                    .environment(\.locale, Locale(identifier: "uk"))
                    .environment(\.colorScheme, scheme)
                    .environment(\.dynamicTypeSize, textSize)
                )
                let window = UIWindow(windowScene: scene)
                window.frame = CGRect(x: 0, y: 0, width: 375, height: 812)
                window.rootViewController = host
                window.makeKeyAndVisible()
                host.view.layoutIfNeeded()
                try await Task.sleep(nanoseconds: 250_000_000)
                let image = UIGraphicsImageRenderer(bounds: window.bounds).image { _ in
                    window.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
                }
                let attachment = XCTAttachment(image: image)
                attachment.name = "Partner job head 375pt uk \(scheme) \(textSize)"
                attachment.lifetime = .keepAlways
                add(attachment)
                window.isHidden = true
                window.rootViewController = nil
                previous?.makeKeyAndVisible()
            }
        }
    }

    func testNewAndPendingLean() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._0), .still(.leaning))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._1), .still(.leaning))
    }

    func testConfirmedWaves() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._2), .still(.waving))
    }

    func testOnTheWayIsReady() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._3), .still(.ready))
    }

    func testAReportedLockoutRests() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._3, reported: true), .still(.resting))
    }

    func testInProgressCarriesTheKitAndACashJobItsSlip() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._4), .still(.sprayAndCloth))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._4, cash: true), .still(.invoice))
    }

    func testAJobNotTakenLeansAndOneAskedForHerWaves() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._0, isMine: false), .still(.leaning))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._4, isMine: false), .still(.leaning))
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._0, isMine: false, offered: true), .still(.waving))
    }

    func testCompletedGivesAThumbsUp() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._5), .still(.thumbsUp))
    }

    func testAnUnknownStatusFallsBackToTheThumbsUp() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: nil), .still(.thumbsUp))
    }

    /// The head draws still figures only: `cleaning`'s pale blob reads as a sticker on dark.
    func testNoStateAnimates() {
        for status in OrderStatus.allCases {
            guard let art = OrderDetailMascotArt.art(for: status) else { continue }
            switch art {
            case .animated: XCTFail("\(status) must not animate")
            case .still: break
            }
        }
    }

    func testCancelledRests() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._6), .still(.resting))
    }

    func testEveryStatusShowsOne() {
        for status in OrderStatus.allCases {
            XCTAssertNotNil(OrderDetailMascotArt.art(for: status), "\(status) has no mascot")
        }
    }
}
