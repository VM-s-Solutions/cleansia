import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

/// The sheet-edge puck's status → art rule, against Android's `FloatingMascot.mascotForStatus`.
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
        for textSize in [DynamicTypeSize.large, .accessibility3] {
            for anchor in [SnapAnchor.peek, .expanded] {
                for scheme in [ColorScheme.light, .dark] {
                    let host = UIHostingController(rootView:
                        SnapSheet(anchor: .constant(anchor)) {
                            CleansiaColors.primaryContainer
                        } ornament: {
                            OrderDetailOrnament(order: .preview)
                        } content: {
                            OrderDetailContent(
                                order: .preview,
                                checklistVM: .preview,
                                notesVM: .preview,
                                photosVM: .preview
                            )
                        }
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
                    attachment.name = "Partner header 375pt uk \(anchor) \(scheme) \(textSize)"
                    attachment.lifetime = .keepAlways
                    add(attachment)
                    window.isHidden = true
                    window.rootViewController = nil
                    previous?.makeKeyAndVisible()
                }
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

    func testOnTheWayCarriesTheKit() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._3), .still(.sprayAndCloth))
    }

    func testCompletedGivesAThumbsUp() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: ._5), .still(.thumbsUp))
    }

    func testAnUnknownStatusFallsBackToTheThumbsUp() {
        XCTAssertEqual(OrderDetailMascotArt.art(for: nil), .still(.thumbsUp))
    }

    /// The rule the whole affordance rests on, and the one a silent static fallback would hide:
    /// InProgress is the ONLY status that animates.
    func testOnlyInProgressAnimates() {
        XCTAssertEqual(
            OrderDetailMascotArt.art(for: ._4),
            .animated(.cleaningInProgress, loop: true, fallback: .cleaning)
        )
        for status in OrderStatus.allCases where status != ._4 {
            guard let art = OrderDetailMascotArt.art(for: status) else { continue }
            switch art {
            case .animated: XCTFail("\(status) must not animate")
            case .still: break
            }
        }
    }

    func testCancelledShowsNoMascot() {
        XCTAssertNil(OrderDetailMascotArt.art(for: ._6))
    }

    func testEveryOtherStatusShowsOne() {
        for status in OrderStatus.allCases where status != ._6 {
            XCTAssertNotNil(OrderDetailMascotArt.art(for: status), "\(status) has no mascot")
        }
    }
}
