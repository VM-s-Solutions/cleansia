import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The boxes only draw the code and the field over them is the control. When the boxes sat on top they took
/// every touch, so a long press never reached the field and its Paste never opened.
@MainActor
final class CodeInputLayoutTests: XCTestCase {
    private let length = 6
    private let boxWidth: CGFloat = 44
    private var window: UIWindow?

    override func tearDown() {
        window?.endEditing(true)
        window?.isHidden = true
        window = nil
        CoreL10n.bundle = .module
        super.tearDown()
    }

    private func hostCodeInput() async throws -> (root: UIView, field: UITextField) {
        let host = UIHostingController(
            rootView: CodeInput(code: .constant("12"), length: length).padding(.horizontal, Spacing.l)
        )
        let window = UIWindow(frame: CGRect(x: 0, y: 0, width: 402, height: 874))
        window.rootViewController = host
        window.makeKeyAndVisible()
        self.window = window
        // The paste control is sized a little after the field, so both are waited for.
        for _ in 0 ..< 40 {
            host.view.layoutIfNeeded()
            let views = Self.views(under: host.view)
            if let field = views.lazy.compactMap({ $0 as? UITextField }).first, field.bounds.height > 0,
               views.contains(where: { $0 is UIPasteControl && $0.bounds.height > 0 })
            {
                return (host.view, field)
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }
        return try (host.view, XCTUnwrap(Self.textFields(under: host.view).first))
    }

    private static func views(under view: UIView) -> [UIView] {
        [view] + view.subviews.flatMap { views(under: $0) }
    }

    private static func textFields(under view: UIView) -> [UITextField] {
        views(under: view).compactMap { $0 as? UITextField }
    }

    private var rowWidth: CGFloat {
        CGFloat(length) * boxWidth + CGFloat(length - 1) * Spacing.xs
    }

    /// The row of boxes is centred in the control.
    private func boxCentres(in root: UIView, level: CGFloat) -> [CGPoint] {
        let first = root.bounds.midX - rowWidth / 2 + boxWidth / 2
        return (0 ..< length).map { CGPoint(x: first + CGFloat($0) * (boxWidth + Spacing.xs), y: level) }
    }

    func testOneFieldCoversTheRowOfBoxes() async throws {
        let (root, field) = try await hostCodeInput()
        let frame = field.convert(field.bounds, to: root)

        XCTAssertEqual(Self.textFields(under: root).count, 1)
        XCTAssertGreaterThanOrEqual(frame.width, rowWidth, "the field is \(frame.width)pt wide")
        XCTAssertGreaterThanOrEqual(frame.height, 56, "the field is \(frame.height)pt tall")
        XCTAssertEqual(frame.midX, root.bounds.midX, accuracy: 1)
    }

    func testEveryBoxPassesItsTouchToTheField() async throws {
        let (root, field) = try await hostCodeInput()
        let frame = field.convert(field.bounds, to: root)

        for (index, point) in boxCentres(in: root, level: frame.midY).enumerated() {
            let hit = root.hitTest(point, with: nil)
            let taker = hit.map { "\(type(of: $0))" } ?? "nothing"
            XCTAssertTrue(hit?.isDescendant(of: field) == true, "box \(index) gave its touch to \(taker)")
        }
    }

    /// UIKit hands a touch at the boxes to the field even when they are drawn over it, yet on iOS 26 a gesture
    /// on boxes drawn over it still swallows the long press, so the order is read from the source.
    func testTheBoxesAreDrawnUnderTheFieldAndTakeNoTouch() throws {
        let source = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/CodeInput.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()
        let stack = try XCTUnwrap(source.range(of: "ZStack{"))
        let boxes = try XCTUnwrap(source.range(of: "box(at:index)", range: stack.upperBound ..< source.endIndex))
        let field = try XCTUnwrap(
            source.range(of: "CodeTextField(code:", range: boxes.upperBound ..< source.endIndex),
            "the boxes are drawn first, under the field"
        )

        XCTAssertTrue(source[boxes.upperBound ..< field.lowerBound].contains(".allowsHitTesting(false)"))
    }

    /// The number pad and the one-time-code suggestion, with the field's own text and caret invisible over
    /// the boxes that show the code.
    func testTheFieldIsANumericOneTimeCodeFieldThatDrawsNothing() async throws {
        let (_, field) = try await hostCodeInput()

        XCTAssertEqual(field.keyboardType, .numberPad)
        XCTAssertEqual(field.textContentType, .oneTimeCode)
        XCTAssertEqual(field.tintColor.cgColor.alpha, 0)
        XCTAssertEqual(field.textColor?.cgColor.alpha, 0)
    }

    /// The boxes are hidden from VoiceOver, so the field is what it reads, and unlabelled it said only that
    /// it was a text field.
    func testTheFieldIsLabelledAsTheVerificationCode() async throws {
        CoreL10n.bundle = CoreL10n.localizedBundle(for: "en")
        let (_, field) = try await hostCodeInput()

        XCTAssertEqual(field.accessibilityLabel, "Verification code, 6 digits")
    }

    /// Paste is the main way to the code, and the system control is drawn smaller than 44 points unless its
    /// text size is raised.
    func testThePasteButtonIsAFullTouchTargetInBothAxes() async throws {
        let (root, _) = try await hostCodeInput()
        let paste = try XCTUnwrap(Self.views(under: root).first { $0 is UIPasteControl })

        XCTAssertGreaterThanOrEqual(paste.bounds.width, 44, "the paste button is \(paste.bounds.width)pt wide")
        XCTAssertGreaterThanOrEqual(paste.bounds.height, 44, "the paste button is \(paste.bounds.height)pt tall")
    }
}
