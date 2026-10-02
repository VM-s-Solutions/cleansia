import XCTest
@testable import CleansiaPartner

final class ContentSafeAreaBindingTests: XCTestCase {
    func testProfileScrollKeepsTheSafeViewport() throws {
        var content = try read("CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift")
        XCTAssertTrue(content.contains("ScrollView{"), "No scrolling surface was inspected")
        content = content.replacingOccurrences(of: "CleansiaColors.background.ignoresSafeArea()", with: "")
        XCTAssertFalse(content.contains(".ignoresSafeArea("), "Profile rows can escape the safe viewport")
        XCTAssertFalse(content.contains("topInset"), "Manual content insets no longer match the safe viewport")
    }

    /// A background inside a scroll view has no top safe area left to ignore, so the hero paints its own
    /// status-bar strip by bleeding its background above its frame — the gradient keeps the hero's bounds
    /// and a block of its first stop rises above them (the customer Profile hero's form).
    func testTheProfileHeroPaintsItsOwnStatusBarStrip() throws {
        let content = try read("CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift")
        XCTAssertTrue(content.contains(
            "VStack(spacing:0){BrandGradient.blue.colors[0].frame(height:heroBleed)"
                + "LinearGradient(colors:BrandGradient.blue.colors,startPoint:.top,endPoint:.bottom)}"
                + ".padding(.top,-heroBleed)"
        ))
    }

    func testApproximateMapLegendFitsBetweenTheSafeTopAndTheSheet() throws {
        let source = try read("CleansiaPartner/Sources/Features/Orders/OrderDetailView.swift")
        XCTAssertTrue(source.contains("@Environment(\\.snapSheetSafeTop)privatevarsafeTop"))
        XCTAssertTrue(source.contains(
            "ViewThatFits(in:.vertical){legend.fixedSize(horizontal:false,vertical:true)Color.clear}"
        ))
        XCTAssertTrue(source.contains(
            ".frame(height:max(sheetTop-safeTop,0)).clipped().padding(.top,safeTop)"
        ))
    }

    private func read(_ path: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines).joined()
    }
}
