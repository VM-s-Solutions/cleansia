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

    private func hostedField(
        keyboardType: UIKeyboardType, contentType: UITextContentType? = nil, isPassword: Bool = false,
        labelAbove: Bool = false, onNext: (() -> Void)? = nil
    ) async throws -> UITextField {
        window?.isHidden = true
        let host = UIHostingController(
            rootView: CleansiaTextField(
                value: .constant(""), label: "Field", keyboardType: keyboardType,
                textContentType: contentType, isPassword: isPassword, labelAbove: labelAbove, onNext: onNext
            )
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

    func testNewPasswordKeepsNativeCredentialSemanticsAndNoTextCorrections() async throws {
        let field = try await hostedField(keyboardType: .default, contentType: .newPassword, isPassword: true)
        XCTAssertEqual(field.textContentType, .newPassword)
        XCTAssertTrue(field.isSecureTextEntry)
        XCTAssertEqual(field.autocapitalizationType, .none)
        XCTAssertEqual(field.autocorrectionType, .no)
        XCTAssertEqual(field.spellCheckingType, .no)
        XCTAssertEqual(field.smartQuotesType, .no)
        XCTAssertEqual(field.smartDashesType, .no)
    }

    /// Synthetic programmatic seating proves the bridge keeps the two bindings separate. Selecting
    /// a real iCloud Keychain suggestion still needs a physical-device pass.
    func testNonfocusedCredentialPairWritesStayInTheirOwnBindings() async throws {
        let model = Credentials()
        let fields = try await hostCredentials(model)
        let username = try XCTUnwrap(fields.first { $0.textContentType == .username })
        let password = try XCTUnwrap(fields.first { $0.textContentType == .newPassword })
        username.text = "mike@example.test"
        password.text = "Example-generated-password-7"
        try await Task.sleep(nanoseconds: 100_000_000)
        XCTAssertEqual(model.email, "mike@example.test")
        XCTAssertEqual(model.password, "Example-generated-password-7")
        XCTAssertEqual(username.text, "mike@example.test")
        XCTAssertEqual(password.text, "Example-generated-password-7")
    }

    /// UIKit may replace the generated-password ink/highlight. A sibling binding update must not
    /// overwrite those native presentation colours with the app's white dark-theme ink.
    func testBindingRedrawPreservesUIKitsCredentialPresentationColours() async throws {
        let model = Credentials()
        let fields = try await hostCredentials(model)
        let password = try XCTUnwrap(fields.first { $0.textContentType == .newPassword })
        password.textColor = .black
        password.backgroundColor = .systemYellow
        model.email = "updated@example.test"
        try await Task.sleep(nanoseconds: 100_000_000)
        XCTAssertEqual(password.textColor, .black)
        // iOS 16 may wrap system colours for contrast; compare the actual resolved channels.
        let actual = try rgba(password.backgroundColor, traits: password.traitCollection)
        let expected = try rgba(.systemYellow, traits: password.traitCollection)
        for (actualChannel, expectedChannel) in zip(actual, expected) {
            XCTAssertEqual(actualChannel, expectedChannel, accuracy: 0.01)
        }
        XCTAssertEqual(password.textContentType, .newPassword)
    }

    private func rgba(_ color: UIColor?, traits: UITraitCollection) throws -> [CGFloat] {
        let resolved = try XCTUnwrap(color).resolvedColor(with: traits)
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        XCTAssertTrue(resolved.getRed(&red, green: &green, blue: &blue, alpha: &alpha))
        return [red, green, blue, alpha]
    }

    private func hostCredentials(_ model: Credentials) async throws -> [UITextField] {
        let host = UIHostingController(rootView: CredentialFields(model: model).preferredColorScheme(.dark))
        let window = UIWindow(frame: CGRect(x: 0, y: 0, width: 402, height: 874))
        window.rootViewController = host
        window.makeKeyAndVisible()
        self.window = window
        for _ in 0 ..< 40 {
            host.view.layoutIfNeeded()
            let fields = Self.textFields(under: host.view)
            if fields.count == 2 { return fields }
            try await Task.sleep(nanoseconds: 50_000_000)
        }
        XCTFail("credential fields never appeared")
        return []
    }

    /// Return offers Next only to a field handed somewhere to go, and a label drawn above the box is the one VoiceOver
    /// reads on the field. A field that asks for neither keeps UIKit's return key and reads as it did.
    func testNextAndTheLabelAboveReachTheField() async throws {
        let plain = try await hostedField(keyboardType: .default)
        XCTAssertEqual(plain.returnKeyType, .default)
        XCTAssertNil(plain.accessibilityLabel)

        var nexts = 0
        let chained = try await hostedField(keyboardType: .default, onNext: { nexts += 1 })
        XCTAssertEqual(chained.returnKeyType, .next)
        XCTAssertEqual(chained.delegate?.textFieldShouldReturn?(chained), true)
        XCTAssertEqual(nexts, 1)

        let labelled = try await hostedField(keyboardType: .default, labelAbove: true)
        XCTAssertEqual(labelled.accessibilityLabel, "Field")
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

private final class Credentials: ObservableObject {
    @Published var email = ""
    @Published var password = ""
}

private struct CredentialFields: View {
    @ObservedObject var model: Credentials

    var body: some View {
        VStack {
            CleansiaTextField(
                value: $model.email, label: "Email", keyboardType: .emailAddress, textContentType: .username
            )
            CleansiaTextField(
                value: $model.password, label: "Password", textContentType: .newPassword, isPassword: true
            )
        }
    }
}
