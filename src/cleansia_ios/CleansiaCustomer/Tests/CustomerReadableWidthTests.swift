import XCTest
@testable import CleansiaCustomer

/// The order detail's footers keep their buttons at most a readable width over a surface that still spans the
/// footer, and the heroes inside the fading scroll, whose content is held to that width on a wide canvas, paint
/// sideways past their frames as they already paint upwards past them, so they stay full-bleed at any width.
final class CustomerReadableWidthTests: XCTestCase {
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
