import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// With a country, the field holds the national digits behind the country's calling code while the bound value stays
/// the whole international number, and a tap on the code moves to the free international form without losing a digit.
@MainActor
final class CleansiaPhoneInputPrefixTests: XCTestCase {
    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

    func testANationalNumberGoesOutBehindItsCallingCodeAndAnEmptyOneStaysEmpty() {
        XCTAssertEqual(CleansiaPhoneInput.wire(national: "", prefix: "+420"), "")
        XCTAssertEqual(CleansiaPhoneInput.wire(national: "777123456", prefix: "+420"), "+420777123456")
    }

    func testAPastedInternationalNumberIsTakenWhole() {
        XCTAssertEqual(CleansiaPhoneInput.wire(national: "+420777123456", prefix: "+420"), "+420777123456")
        XCTAssertEqual(CleansiaPhoneInput.wire(national: "+447700900123", prefix: "+420"), "+447700900123")
        XCTAssertNil(CleansiaPhoneInput.prefix(of: "+447700900123", region: "CZ"), "a foreign number leaves the prefix")
    }

    /// A stored number of another country is shown as it is stored, never rewritten under this one's code.
    func testOnlyAnEmptyValueOrOneCarryingTheCodeShowsThePrefix() {
        XCTAssertEqual(CleansiaPhoneInput.prefix(of: "", region: "CZ"), "+420")
        XCTAssertEqual(CleansiaPhoneInput.prefix(of: "+42077712", region: "CZ"), "+420")
        XCTAssertNil(CleansiaPhoneInput.prefix(of: "+421905123456", region: "CZ"))
        XCTAssertNil(CleansiaPhoneInput.prefix(of: "", region: nil))
    }

    func testThePrefixSwitchKeepsTheTypedDigits() async throws {
        let phone = PhoneBox("+42077712")
        let canvas = host(phone)
        defer { canvas.close() }
        await canvas.settle(until: { self.field(in: canvas)?.text == "777 12" && self.countryCode(in: canvas) != nil })
        let field = try XCTUnwrap(field(in: canvas))
        let code = try XCTUnwrap(countryCode(in: canvas))

        XCTAssertEqual(field.text, "777 12")
        XCTAssertEqual(code.accessibilityFrame.height, 52, accuracy: 0.5)

        code.accessibilityActivate()
        await canvas.settle(until: { field.text == "+420 777 12" })

        XCTAssertEqual(phone.value, "+42077712")
        XCTAssertEqual(field.text, "+420 777 12")
        XCTAssertNil(countryCode(in: canvas))
    }

    /// Digits typed after the code are stored behind it, while the field shows only them.
    func testTypedNationalDigitsAreStoredBehindTheCallingCode() async throws {
        let phone = PhoneBox("")
        let canvas = host(phone)
        defer { canvas.close() }
        await canvas.settle(until: { self.field(in: canvas) != nil && self.countryCode(in: canvas) != nil })
        let field = try XCTUnwrap(field(in: canvas))
        let typing = try XCTUnwrap(field.delegate as? MaskedPhoneTextField.Coordinator)

        _ = typing.textField(
            field,
            shouldChangeCharactersIn: NSRange(location: 0, length: 0),
            replacementString: "77712"
        )
        await canvas.settle(until: { phone.value == "+42077712" })

        XCTAssertEqual(phone.value, "+42077712")
        XCTAssertEqual(field.text, "777 12")
        XCTAssertNotNil(countryCode(in: canvas), "the code stays in front of the typed digits")
    }

    func testThePrefixSwitchStartsAnEmptyFieldAtThePlus() async throws {
        let phone = PhoneBox("")
        let canvas = host(phone)
        defer { canvas.close() }
        await canvas.settle(until: { self.countryCode(in: canvas) != nil })

        try XCTUnwrap(countryCode(in: canvas)).accessibilityActivate()
        await canvas.settle(until: { phone.value == "+" })

        XCTAssertEqual(phone.value, "+")
        XCTAssertNil(countryCode(in: canvas))
    }

    private func host(_ phone: PhoneBox) -> HostedCanvas {
        HostedCanvas(size: CGSize(width: 390, height: 200)) {
            PrefixedPhone(phone: phone).padding(Spacing.m)
        }
    }

    private func field(in canvas: HostedCanvas) -> UITextField? {
        canvas.views(of: UITextField.self).first
    }

    private func countryCode(in canvas: HostedCanvas) -> NSObject? {
        let label = CoreL10n.localized("phone_input.country_code")
        return ApplicationAccessibility.elements(under: canvas.window).first { $0.accessibilityLabel == label }
    }
}

private final class PhoneBox: ObservableObject {
    @Published var value: String

    init(_ value: String) {
        self.value = value
    }
}

private struct PrefixedPhone: View {
    @ObservedObject var phone: PhoneBox

    var body: some View {
        CleansiaPhoneInput(value: $phone.value, label: "Phone number", countryRegion: "CZ")
    }
}
