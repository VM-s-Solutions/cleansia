import XCTest
@testable import CleansiaPartner

final class ContentSafeAreaBindingTests: XCTestCase {
    func testProfileScrollKeepsTheSafeViewport() throws {
        var content = try read("CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift")
        XCTAssertTrue(content.contains("ScrollView{"), "No scrolling surface was inspected")
        for background in [
            "CleansiaColors.background.ignoresSafeArea()",
            "LinearGradient(colors:BrandGradient.blue.colors,startPoint:.top,endPoint:.bottom)"
                + ".ignoresSafeArea(.container,edges:.top)"
        ] {
            content = content.replacingOccurrences(of: background, with: "")
        }
        XCTAssertFalse(content.contains(".ignoresSafeArea("), "Profile rows can escape the safe viewport")
        XCTAssertFalse(content.contains("topInset"), "Manual content insets no longer match the safe viewport")
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
