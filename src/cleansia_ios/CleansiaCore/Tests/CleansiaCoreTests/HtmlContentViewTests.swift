import XCTest
@testable import CleansiaCore

final class HtmlContentViewTests: XCTestCase {
    func testTheFragmentIsEmbeddedAsTheBodyOfAFullDocument() {
        let document = HtmlDocument.wrap("<p>Contract for work</p>")

        XCTAssertTrue(document.hasPrefix("<!doctype html>"))
        XCTAssertTrue(document.contains("<body><p>Contract for work</p></body>"))
        XCTAssertTrue(document.contains("<meta charset=\"utf-8\">"))
    }

    func testTheDocumentCarriesBothSchemesInk() {
        let document = HtmlDocument.wrap("<p>x</p>")

        XCTAssertTrue(document.contains("color: \(HtmlDocument.inkLightHex)"))
        XCTAssertTrue(document.contains("prefers-color-scheme: dark"))
        XCTAssertTrue(document.contains("color: \(HtmlDocument.inkDarkHex)"))
        XCTAssertTrue(document.contains(HtmlDocument.accentLightHex))
        XCTAssertTrue(document.contains(HtmlDocument.accentDarkHex))
    }

    /// A link is text, so in light mode it takes the text ink (sky-700, 5.93:1 on white), not the primary
    /// (sky-600, 4.10:1), which keeps the quote rule; dark mode draws both in sky-400 (owner decision
    /// 2026-10-05).
    func testALinkTakesTheTextInkAndTheQuoteRuleThePrimary() {
        let document = HtmlDocument.wrap("<p>x</p>")

        XCTAssertEqual(HtmlDocument.linkLightHex, "#0369A1")
        XCTAssertTrue(document.contains("a { color: \(HtmlDocument.linkLightHex); }"))
        XCTAssertTrue(document.contains("border-left: 3px solid \(HtmlDocument.accentLightHex);"))
        XCTAssertTrue(document.contains("a { color: \(HtmlDocument.accentDarkHex); }"))
        XCTAssertFalse(document.contains("a { color: \(HtmlDocument.accentLightHex); }"))
    }

    /// The text reads as the app's own: its body face, starting on the same edge as the page around it.
    func testTheTextIsSetInTheAppsBodyFaceWithNoInsetOfItsOwn() {
        let document = HtmlDocument.wrap("<p>x</p>")

        XCTAssertTrue(document.contains("font-family: Nunito, -apple-system, sans-serif;"))
        XCTAssertTrue(document.contains("body { margin: 0; padding: 0 0 16px;"))
        XCTAssertFalse(document.contains("padding: 0 4px"))
    }

    /// A web view cannot see the fonts the app registers, so the app's body face rides inside the document, one
    /// face per weight the app bundles.
    func testTheAppsBundledBodyFacesTravelInsideTheDocument() throws {
        let bundle = try fontBundle(["Nunito-Regular": [1, 2, 3], "Nunito-Bold": [4, 5, 6]])

        let faces = HtmlDocument.fontFaces(in: bundle)

        XCTAssertEqual(faces, [
            "@font-face { font-family: Nunito; font-weight: 400; src: url(data:font/ttf;base64,AQID) "
                + "format('truetype'); }",
            "@font-face { font-family: Nunito; font-weight: 700; src: url(data:font/ttf;base64,BAUG) "
                + "format('truetype'); }"
        ].joined(separator: "\n"))
        let document = HtmlDocument.wrap("<p>x</p>", fontFaces: faces)
        XCTAssertLessThan(
            try XCTUnwrap(document.range(of: faces)).upperBound,
            try XCTUnwrap(document.range(of: "</style>")).lowerBound
        )
    }

    func testWithoutBundledFacesTheTextFallsBackToTheSystemFace() throws {
        XCTAssertEqual(try HtmlDocument.fontFaces(in: fontBundle([:])), "")
    }

    func testTheWrapperAddsNoScript() {
        XCTAssertFalse(HtmlDocument.wrap("<p>x</p>").lowercased().contains("<script"))
    }

    func testOnlyTheDocumentsOwnLoadMayNavigate() {
        XCTAssertTrue(HtmlDocument.allowsNavigation(to: URL(string: "about:blank")))
        XCTAssertFalse(HtmlDocument.allowsNavigation(to: URL(string: "https://cleansia.cz/terms")))
        XCTAssertFalse(HtmlDocument.allowsNavigation(to: URL(string: "mailto:info@cleansia.cz")))
        XCTAssertFalse(HtmlDocument.allowsNavigation(to: nil))
    }

    private func fontBundle(_ files: [String: [UInt8]]) throws -> Bundle {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString + ".bundle")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: directory) }
        for (name, bytes) in files {
            try Data(bytes).write(to: directory.appendingPathComponent(name + ".ttf"))
        }
        return try XCTUnwrap(Bundle(url: directory))
    }

    #if canImport(UIKit)
        @MainActor
        func testTheWebViewIsBuiltWithJavaScriptOffAndRefusesNavigationThroughItsCoordinator() {
            let coordinator = HtmlContentView(html: "<p>x</p>").makeCoordinator()

            let webView = HtmlContentView.makeWebView(coordinator: coordinator)

            XCTAssertFalse(webView.configuration.defaultWebpagePreferences.allowsContentJavaScript)
            XCTAssertFalse(webView.isOpaque)
            XCTAssertTrue(webView.navigationDelegate === coordinator)
            XCTAssertTrue(webView.scrollView.isScrollEnabled, "by default the text scrolls itself")
        }

        /// Sized by its caller, the text scrolls with the caller's page: it does not scroll itself and says how tall
        /// its document is, with JavaScript still off and navigation still refused.
        @MainActor
        func testFittedToItsDocumentTheWebViewDoesNotScrollAndReportsTheDocumentsHeight() {
            let reported = expectation(description: "the document's height")
            reported.assertForOverFulfill = false
            var heights: [CGFloat] = []
            let coordinator = HtmlContentView(html: "<p>x</p>") { height in
                heights.append(height)
                if height == 813 { reported.fulfill() }
            }.makeCoordinator()

            let webView = HtmlContentView.makeWebView(coordinator: coordinator)
            webView.scrollView.contentSize = CGSize(width: 361, height: 812.4)

            wait(for: [reported], timeout: 2)
            XCTAssertFalse(webView.scrollView.isScrollEnabled)
            XCTAssertEqual(heights.last, 813)
            XCTAssertFalse(heights.contains { $0 <= 0 }, "an empty document reports nothing")
            XCTAssertFalse(webView.configuration.defaultWebpagePreferences.allowsContentJavaScript)
            XCTAssertTrue(webView.navigationDelegate === coordinator)
        }
    #endif
}
