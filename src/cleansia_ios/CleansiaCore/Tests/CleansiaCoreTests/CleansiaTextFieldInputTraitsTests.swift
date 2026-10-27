import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// An e-mail address opens in lower case and is typed as written, as Android's `KeyboardType.Email` field is:
/// the field with the e-mail keyboard turns auto-capitalisation and autocorrection off, and every other field
/// keeps UIKit's defaults.
@MainActor
final class CleansiaTextFieldInputTraitsTests: XCTestCase {
    private var window: UIWindow?

    override func tearDown() {
        window?.isHidden = true
        window = nil
        super.tearDown()
    }

    private func hostedField(keyboardType: UIKeyboardType) async throws -> UITextField {
        window?.isHidden = true
        let host = UIHostingController(
            rootView: CleansiaTextField(value: .constant(""), label: "Field", keyboardType: keyboardType)
                .padding(.horizontal, Spacing.l)
        )
        let window = UIWindow(frame: CGRect(x: 0, y: 0, width: 402, height: 874))
        window.rootViewController = host
        window.makeKeyAndVisible()
        self.window = window
        for _ in 0 ..< 40 {
            host.view.layoutIfNeeded()
            if let field = Self.textFields(under: host.view).first {
                return field
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }
        return try XCTUnwrap(Self.textFields(under: host.view).first)
    }

    private static func textFields(under view: UIView) -> [UITextField] {
        let own = (view as? UITextField).map { [$0] } ?? []
        return own + view.subviews.flatMap { textFields(under: $0) }
    }

    func testTheEmailFieldOpensInLowerCaseAndIsNotAutocorrected() async throws {
        let field = try await hostedField(keyboardType: .emailAddress)

        XCTAssertEqual(field.keyboardType, .emailAddress)
        XCTAssertEqual(field.autocapitalizationType, .none)
        XCTAssertEqual(field.autocorrectionType, .no)
    }

    func testEveryOtherFieldKeepsUIKitsDefaults() async throws {
        for keyboard in [UIKeyboardType.default, .phonePad, .numberPad, .decimalPad] {
            let field = try await hostedField(keyboardType: keyboard)

            XCTAssertEqual(field.keyboardType, keyboard)
            XCTAssertEqual(field.autocapitalizationType, .sentences, "keyboard \(keyboard.rawValue)")
            XCTAssertEqual(field.autocorrectionType, .default, "keyboard \(keyboard.rawValue)")
        }
    }
}
