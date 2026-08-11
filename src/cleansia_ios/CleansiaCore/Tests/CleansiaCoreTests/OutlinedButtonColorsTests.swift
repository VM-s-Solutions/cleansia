import SwiftUI
import XCTest
@testable import CleansiaCore

/// IOS-1 — `CleansiaOutlinedButton` used to hard-code `onSurface` + `outline`, so
/// the `.tint(...)` the order-detail footer applied to Cancel and Report issue was
/// dead and all three actions shipped the same neutral grey. These pin the two
/// invariants that fix depends on: an untouched call site is still neutral, and a
/// tinted one colours BOTH slots.
///
/// Identity comparison is safe here in a way the `BrandGradientTests` note warns
/// about elsewhere — the resolver returns the very `CleansiaColors` value it was
/// handed, so nothing round-trips through `UIColor` and no dynamic provider is
/// flattened.
final class OutlinedButtonColorsTests: XCTestCase {
    func testDefaultContentIsTheNeutralOnSurface() {
        XCTAssertEqual(CleansiaOutlinedButtonColors.content(nil), CleansiaColors.onSurface)
    }

    func testDefaultBorderIsTheNeutralOutline() {
        XCTAssertEqual(
            CleansiaOutlinedButtonColors.border(content: nil, border: nil),
            CleansiaColors.outline
        )
    }

    func testContentColorOverridesTheNeutralDefault() {
        XCTAssertEqual(
            CleansiaOutlinedButtonColors.content(CleansiaColors.error),
            CleansiaColors.error
        )
    }

    func testBorderFollowsContentWhenOnlyContentIsGiven() {
        XCTAssertEqual(
            CleansiaOutlinedButtonColors.border(content: CleansiaColors.error, border: nil),
            CleansiaColors.error
        )
    }

    func testExplicitBorderWinsOverContent() {
        XCTAssertEqual(
            CleansiaOutlinedButtonColors.border(
                content: CleansiaColors.error,
                border: CleansiaColors.outline
            ),
            CleansiaColors.outline
        )
    }

    /// Tinting only the border must NOT drag the label along with it — the two
    /// slots stay independently addressable in that direction.
    func testBorderOnlyLeavesContentNeutral() {
        XCTAssertEqual(CleansiaOutlinedButtonColors.content(nil), CleansiaColors.onSurface)
        XCTAssertEqual(
            CleansiaOutlinedButtonColors.border(content: nil, border: CleansiaColors.primary),
            CleansiaColors.primary
        )
    }

    /// The two role colours the order-detail footer tints with — matching
    /// Android's `ActionsFooter`, which sets `contentColor` and `BorderStroke` to
    /// the same role colour on each button.
    ///
    /// This asserts the resolver only, so it cannot see WHICH action gets WHICH
    /// role and stays green through any reassignment of them: it survived Report
    /// issue moving off `primary` onto `error` without a word. The assignment is
    /// pinned at the call site instead, in the customer app's
    /// `OrderDetailFooterStyleTests`.
    func testOrderDetailFooterPairsMatchAndroidRoles() {
        for role in [CleansiaColors.error, CleansiaColors.primary] {
            XCTAssertEqual(CleansiaOutlinedButtonColors.content(role), role)
            XCTAssertEqual(CleansiaOutlinedButtonColors.border(content: role, border: nil), role)
        }
    }
}
