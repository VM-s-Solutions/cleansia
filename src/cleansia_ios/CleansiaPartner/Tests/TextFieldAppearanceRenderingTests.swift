import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore
@testable import CleansiaPartner

@MainActor
final class TextFieldAppearanceRenderingTests: XCTestCase {
    func testMountedNonemptyTextFieldRetainsIdentityFocusAndInkAcrossAppearanceChanges() async throws {
        try await verifyMountedField(phone: false)
    }

    func testMountedNonemptyPhoneFieldRetainsIdentityFocusAndInkAcrossAppearanceChanges() async throws {
        try await verifyMountedField(phone: true)
    }

    private func verifyMountedField(phone: Bool) async throws {
        let previous = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        let host = UIHostingController(rootView: AppearanceFieldHarness(phone: phone))
        let scene = try XCTUnwrap(previous?.windowScene)
        let window = UIWindow(windowScene: scene)
        window.frame = scene.screen.bounds
        window.rootViewController = host
        window.overrideUserInterfaceStyle = .light
        window.makeKeyAndVisible()
        defer {
            window.endEditing(true)
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        try await waitFor("nonempty field mounted") {
            guard let field = Self.field(under: host.view) else { return false }
            return field.window === window && !(field.text ?? "").isEmpty && field.bounds.width > 0
        }
        let field = try XCTUnwrap(Self.field(under: host.view))
        field.becomeFirstResponder()
        try await waitFor("mounted field focused") { field.isFirstResponder }
        XCTAssertEqual(field.text, phone ? "+420 728 089 247" : "Ink sample 123")
        XCTAssertEqual(field.keyboardType, phone ? .phonePad : .default)
        if phone { XCTAssertEqual(field.textContentType, .telephoneNumber) }

        for (step, style) in [UIUserInterfaceStyle.light, .dark, .light].enumerated() {
            window.overrideUserInterfaceStyle = style
            host.view.setNeedsLayout()
            host.view.layoutIfNeeded()
            try await waitFor("appearance traits applied") {
                field.traitCollection.userInterfaceStyle == style && field.isFirstResponder
            }
            XCTAssertTrue(Self.field(under: host.view) === field)
            XCTAssertTrue(field.window === window)
            XCTAssertTrue(window.isKeyWindow)
            XCTAssertFalse(window.isHidden)
            XCTAssertTrue(field.isFirstResponder)
            XCTAssertFalse((field.text ?? "").isEmpty)
            XCTAssertGreaterThan(field.bounds.width, 0)
            XCTAssertGreaterThan(field.bounds.height, 0)
            XCTAssertTrue(field.convert(field.bounds, to: window).intersects(window.bounds))
            let scheme: ColorScheme = style == .dark ? .dark : .light
            let expectedInk = StatusBarFade.rgb(CleansiaColors.onSurface, scheme)
            let expectedCaret = StatusBarFade.rgb(CleansiaColors.primary, scheme)
            let ink = try rgba(field.textColor, traits: field.traitCollection)
            let caret = try rgba(field.tintColor, traits: field.traitCollection)
            attachReceipt(field: field, ink: ink, caret: caret, phone: phone, step: step)
            attachScreenshot(window, name: "\(phone ? "phone" : "text")-\(step)-style\(style.rawValue)")
            assertRGB(ink, expectedInk, "mounted text ink, step \(step)")
            assertRGB(caret, expectedCaret, "mounted caret ink, step \(step)")
            XCTAssertEqual(ink.w, 1, accuracy: 0.01)
            XCTAssertEqual(caret.w, 1, accuracy: 0.01)
        }
    }

    private func waitFor(_ name: String, condition: @escaping () -> Bool) async throws {
        let ready = XCTNSPredicateExpectation(predicate: NSPredicate { _, _ in condition() }, object: nil)
        await fulfillment(of: [ready], timeout: 3)
        XCTAssertTrue(condition(), name)
    }

    private static func field(under view: UIView) -> UITextField? {
        if let field = view as? UITextField { return field }
        return view.subviews.lazy.compactMap { field(under: $0) }.first
    }

    private func rgba(_ color: UIColor?, traits: UITraitCollection) throws -> SIMD4<Double> {
        let resolved = try XCTUnwrap(color).resolvedColor(with: traits)
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        XCTAssertTrue(resolved.getRed(&red, green: &green, blue: &blue, alpha: &alpha))
        return SIMD4(Double(red), Double(green), Double(blue), Double(alpha))
    }

    private func assertRGB(_ actual: SIMD4<Double>, _ expected: SIMD3<Double>, _ message: String) {
        XCTAssertEqual(actual.x, expected.x, accuracy: 0.01, message)
        XCTAssertEqual(actual.y, expected.y, accuracy: 0.01, message)
        XCTAssertEqual(actual.z, expected.z, accuracy: 0.01, message)
    }

    private func attachReceipt(field: UITextField, ink: SIMD4<Double>, caret: SIMD4<Double>, phone: Bool, step: Int) {
        let receipt: [String: Any] = [
            "field": phone ? "phone" : "text", "step": step,
            "identity": String(describing: ObjectIdentifier(field)),
            "text": field.text ?? "", "firstResponder": field.isFirstResponder,
            "style": field.traitCollection.userInterfaceStyle.rawValue,
            "textRGBA": [ink.x, ink.y, ink.z, ink.w], "caretRGBA": [caret.x, caret.y, caret.z, caret.w],
            "os": UIDevice.current.systemVersion,
            "simulator": ProcessInfo.processInfo.environment["SIMULATOR_UDID"] ?? "unknown",
            "source": #filePath, "product": Bundle.main.bundleURL.path
        ]
        let data = (try? JSONSerialization.data(withJSONObject: receipt, options: [.sortedKeys])) ?? Data()
        let attachment = XCTAttachment(data: data, uniformTypeIdentifier: "public.json")
        attachment.name = "\(phone ? "phone" : "text")-\(step)-traits-rgba"
        attachment.lifetime = .keepAlways
        add(attachment)
        print("MOUNTED_FIELD_RECEIPT \(String(data: data, encoding: .utf8) ?? "")")
    }

    private func attachScreenshot(_ window: UIWindow, name: String) {
        let rendered = UIGraphicsImageRenderer(bounds: window.bounds).image { _ in
            window.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
        }
        let attachment = XCTAttachment(image: rendered)
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }
}

private struct AppearanceFieldHarness: View {
    let phone: Bool
    @State private var text = "Ink sample 123"
    @State private var number = "+420728089247"

    var body: some View {
        VStack {
            if phone {
                CleansiaPhoneInput(
                    value: $number, label: "Phone",
                    engine: PhoneMaskEngine(formatter: GroupedPhoneDisplayFormatter())
                )
            } else {
                CleansiaTextField(value: $text, label: "Text", autoFocus: true)
            }
            Spacer()
        }
        .padding(24)
        .padding(.top, 80)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}
