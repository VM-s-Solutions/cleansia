import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The list screens hold their column, and the order detail's footers and the Plus offer's bar their buttons, to at
/// most a readable width over a background that still spans the screen, and the heroes inside the fading scroll,
/// whose content is held to that width on a wide canvas, paint sideways past their frames as they already paint
/// upwards past them, so they stay full-bleed at any width. On every iPhone today (440pt at most) and on the iPhone
/// Duo's outer display the lists span the screen exactly as before.
@MainActor
final class CustomerReadableWidthTests: XCTestCase {
    private let cap: CGFloat = 640
    private let narrow = [
        CGSize(width: 393, height: 852),
        CGSize(width: 440, height: 956),
        CGSize(width: 466, height: 678)
    ]
    private let wide = [CGSize(width: 669, height: 951), CGSize(width: 951, height: 669)]

    func testTheOrdersAndDisputesListsSpanAPhoneAsBefore() async throws {
        for size in narrow {
            for list in try await [ordersListFrame(in: size), disputesListFrame(in: size)] {
                XCTAssertEqual(list.minX, 0, accuracy: 0.5, "\(size)")
                XCTAssertEqual(list.width, size.width, accuracy: 0.5, "\(size)")
            }
        }
    }

    func testTheOrdersAndDisputesListsAreCentredAtTheReadableWidthOnAWiderCanvas() async throws {
        for size in wide {
            for list in try await [ordersListFrame(in: size), disputesListFrame(in: size)] {
                XCTAssertEqual(list.width, cap, accuracy: 0.5, "\(size)")
                XCTAssertEqual(list.midX, size.width / 2, accuracy: 0.5, "\(size)")
            }
        }
    }

    /// The screens that build live clients or a map of their own, so are not hosted here, hold their column where
    /// the hosted lists hold theirs: around everything the screen draws, inside its full-bleed background.
    func testTheOtherListScreensHoldTheirColumnToTheReadableWidth() throws {
        let columns = [
            (
                "CleansiaCustomer/Sources/Features/Rewards/RewardsTab.swift",
                ".id(locale.identifier)}.readableWidth().frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.top)"
                    + ".background(CleansiaColors.background.ignoresSafeArea())"
            ),
            (
                "CleansiaCustomer/Sources/Features/Rewards/RewardsActivityScreen.swift",
                "content.readableWidth().frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.top)"
                    + ".background(CleansiaColors.background.ignoresSafeArea())"
            ),
            (
                "CleansiaCustomer/Sources/Features/Recurring/RecurringBookingsScreen.swift",
                "content.readableWidth().navigationTitle(L10n.Recurring.bookingsTitle)"
            ),
            (
                "CleansiaCustomer/Sources/Features/Recurring/RecurringBookingsScreen.swift",
                "action:onCreateNew).padding(Spacing.ml).readableWidth()}"
            ),
            (
                "CleansiaCustomer/Sources/Features/Addresses/AddressManagerView.swift",
                "addButton}.padding(.horizontal,Spacing.l).padding(.top,Spacing.s).padding(.bottom,Spacing.xxl)}}"
                    + ".readableWidth().background(CleansiaColors.background.ignoresSafeArea())"
            ),
            (
                "CleansiaCustomer/Sources/Features/Addresses/AddressManagerView.swift",
                ".padding(.horizontal,Spacing.l).padding(.bottom,Spacing.m)}"
                    + ".readableWidth().background(CleansiaColors.background.ignoresSafeArea())"
            )
        ]
        for (path, chain) in columns {
            XCTAssertTrue(try compact(path).contains(chain), "\(path): \(chain)")
        }
    }

    func testTheOrderDetailFootersHoldTheirButtonsToTheReadableWidth() throws {
        for footer in ["privatestructConfirmRecurringFooter:View{", "privatestructOrderDetailActionsFooter:View{"] {
            let body = try compactBody(of: footer, in: "CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")
            XCTAssertTrue(
                body.contains(
                    ".padding(.horizontal,Spacing.m).padding(.vertical,Spacing.s).readableWidth()"
                        + ".background(CleansiaColors.surface.ignoresSafeArea(edges:.bottom))"
                ),
                footer
            )
            XCTAssertFalse(body.contains(".frame(maxWidth:.infinity)"), footer)
        }
    }

    func testThePlusOffersBarHoldsItsButtonToTheReadableWidth() throws {
        let bar = try compactBody(
            of: "privatestructStickyCtaBar:View{",
            in: "CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift"
        )
        XCTAssertTrue(bar.contains(
            ".padding(.bottom,Spacing.s).readableWidth()"
                + ".background(CleansiaColors.surface.ignoresSafeArea(edges:.bottom))"
        ))
        XCTAssertFalse(bar.contains(".frame(maxWidth:.infinity)"))
    }

    func testTheProfileAndPlusHeroesPaintPastTheirFramesOnEverySide() throws {
        let profile = try compact("CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift")
        XCTAssertTrue(profile.contains(
            "startPoint:.top,endPoint:.bottom)}.padding(.top,-heroBleed).padding(.horizontal,-heroBleed)"
        ))
        let plus = try compact("CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift")
        XCTAssertTrue(plus.contains(
            "MembershipPalette.heroGradient}.padding(.top,-heroBleed).padding(.horizontal,-heroBleed)"
        ))
    }

    /// The Orders tab with one order: its list's scroll view, where it lays out.
    private func ordersListFrame(in size: CGSize) async throws -> CGRect {
        let client = FakeOrderClient()
        client.pages = [OrdersPage(items: [OrderFixtures.summary(id: "a", statusValue: 2)], total: 1)]
        let canvas = try HostedCanvas(size: size) {
            OrdersTab(
                repository: OrderRepository(client: client),
                marketStore: MarketFixtures.store().0,
                snackbar: SnackbarController(),
                onOrderClick: { _ in },
                onBookCleaning: {}
            )
        }
        defer { canvas.close() }
        return try await tallestScrollView(in: canvas, at: size)
    }

    private func disputesListFrame(in size: CGSize) async throws -> CGRect {
        let client = FakeDisputeClient()
        client.pages = [DisputesPage(items: [DisputeFixtures.entry(id: "a")], total: 1)]
        let canvas = try HostedCanvas(size: size) {
            NavigationStack {
                DisputesListView(
                    repository: DisputeRepository(client: client),
                    snackbar: SnackbarController(),
                    onDisputeClick: { _ in },
                    onBrowseOrders: {}
                )
            }
        }
        defer { canvas.close() }
        return try await tallestScrollView(in: canvas, at: size)
    }

    /// The list's own scroll view, which outgrows any row of chips scrolling sideways inside it.
    private func tallestScrollView(in canvas: HostedCanvas, at size: CGSize) async throws -> CGRect {
        let lists = {
            canvas.views(of: UIScrollView.self)
                .map { canvas.frame(of: $0) }
                .filter { $0.height > size.height / 2 }
        }
        await canvas.settle { !lists().isEmpty }
        return try XCTUnwrap(lists().max { $0.height < $1.height }, "no list at \(size)")
    }

    /// The braced body that follows the compacted `declaration`.
    private func compactBody(of declaration: String, in path: String) throws -> String {
        let code = try compact(path)
        let start = try XCTUnwrap(code.range(of: declaration), "\(declaration) is not in \(path)")
        var depth = 1
        for index in code[start.upperBound...].indices {
            if code[index] == "{" { depth += 1 }
            if code[index] == "}" { depth -= 1 }
            if depth == 0 { return String(code[start.upperBound ..< index]) }
        }
        XCTFail("\(declaration) never closes in \(path)")
        return ""
    }

    private func compact(_ path: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }
}
