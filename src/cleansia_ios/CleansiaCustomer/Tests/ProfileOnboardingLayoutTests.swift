import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The onboarding in the mock's frame, measured from what the hosted tree exposes: whether a state fits, the rows and
/// the touch targets, what VoiceOver reads, the focus chain from Last name, and the typing state.
@MainActor
final class ProfileOnboardingLayoutTests: XCTestCase {
    static let phone = CGSize(width: 390, height: 844)
    static let mini = CGSize(width: 375, height: 812)
    static let keypad: CGFloat = 291

    private var restoreBundle: Bundle!
    private var restoreTag: String!

    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

    override func setUpWithError() throws {
        try super.setUpWithError()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
        try use("en")
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    /// "Fits" is the card ending at or above the footer, with nothing to scroll. Czech in dark mode carries the
    /// longest words: the header, "Jméno a příjmení", "Navigovat" and the dial label.
    func testEveryRequiredStateFitsWithoutScrolling() async throws {
        let states: [(OnboardingSample, CGSize)] = [
            (.empty, Self.phone), (.filled, Self.phone), (.sheet, Self.phone), (.applePrivate, Self.phone),
            (.empty, Self.mini), (.longEmail, Self.mini)
        ]
        for (state, size) in states {
            let canvas = try await host(state, size: size)
            defer { canvas.close() }
            await canvas.settle()
            try assertFits(canvas, "\(state.rawValue) at \(Int(size.width))")
        }
        try use("cs")
        let czech = try await host(.csDark, size: Self.phone, language: "cs", dark: true)
        defer { czech.close() }
        await czech.settle()
        try assertFits(czech, "cs-dark")
    }

    func testTheRowsAndTheTouchTargetsKeepTheirSizes() async throws {
        let canvas = try await host(.empty, size: Self.phone)
        defer { canvas.close() }
        await canvas.settle()

        for label in ["Name", "Email"] {
            let row = try XCTUnwrap(row(label, in: canvas), label)
            XCTAssertEqual(row.frame.height, 44, accuracy: 0.5, label)
        }
        let code = try XCTUnwrap(Self.reads(in: canvas.window).first { $0.label == "Country code" })
        XCTAssertEqual(code.frame.height, 52, accuracy: 0.5, "the phone field's box")
        XCTAssertEqual(
            code.frame.minX,
            Spacing.ml + Spacing.m,
            accuracy: 0.5,
            "the box starts at the card's inner edge"
        )
        let skip = try XCTUnwrap(Self.reads(in: canvas.window).first { $0.label == "Skip for now" })
        XCTAssertGreaterThanOrEqual(skip.frame.height, 44)
        XCTAssertEqual(skip.frame.width, Self.phone.width - 2 * Spacing.ml, accuracy: 0.5)
    }

    func testEachRowIsReadOnceWithItsState() async throws {
        let canvas = try await host(.empty, size: Self.phone)
        defer { canvas.close() }
        await canvas.settle()
        let reads = Self.reads(in: canvas.window)
        let expected = [
            ("Name, Jana Dvořáková", "done"),
            ("Email, jana.dvorakova@gmail.com", "done"),
            ("Phone number", "current step"),
            ("Birth date, Optional. Never shown to your cleaner.", "not done")
        ]

        for (label, value) in expected {
            let read = reads.first { $0.label == label }
            XCTAssertEqual(read?.value, value, "\(label) in \(reads.map(\.label))")
        }
        let birth = reads.first { $0.label.hasPrefix("Birth date") }
        XCTAssertTrue(birth?.traits.contains(.button) == true, "the Birth date row is a button")
        XCTAssertFalse(reads.contains { $0.label == "Not set" }, "a waiting row says its state once")
    }

    func testAHiddenAppleAccountShowsNoStandInAndTheExampleFollowsHerTyping() async throws {
        let canvas = try await host(.applePrivate, size: Self.phone)
        defer { canvas.close() }
        await canvas.settle()
        let reads = Self.reads(in: canvas.window)

        XCTAssertTrue(reads.contains { $0.label == "Email, Hidden by Apple" }, "\(reads.map(\.label))")
        XCTAssertTrue(reads.contains { $0.label == "Welcome aboard!" })
        for word in ["privaterelay", "Cleansia", "Customer"] {
            XCTAssertFalse(reads.contains { $0.label.contains(word) || $0.value.contains(word) }, word)
        }
        let example = try XCTUnwrap(Self.example(in: canvas))
        XCTAssertTrue(example.contains("Not set"), example)
        XCTAssertFalse(example.contains("Jana"), example)

        let first = try XCTUnwrap(canvas.views(of: UITextField.self).first { $0.accessibilityLabel == "First name" })
        first.text = "Jana"
        await canvas.settle { Self.example(in: canvas)?.contains("Jana") == true }
        XCTAssertTrue(Self.example(in: canvas)?.contains("Jana") == true, Self.example(in: canvas) ?? "no example")
    }

    /// Both ways a step opens by hand focus its field: Next on Last name, and a tap on a waiting row.
    func testAStepOpenedByHandFocusesItsField() async throws {
        let canvas = try await host(.applePrivate, size: Self.phone)
        defer { canvas.close() }
        await canvas.settle()
        let last = try XCTUnwrap(canvas.views(of: UITextField.self).first { $0.returnKeyType == .next })
        last.becomeFirstResponder()
        await canvas.settle { last.isFirstResponder }

        _ = last.delegate?.textFieldShouldReturn?(last)
        let phoneFocused = {
            canvas.views(of: UITextField.self).contains { $0.keyboardType == .numberPad && $0.isFirstResponder }
        }
        await canvas.settle(timeout: 5, until: phoneFocused)
        XCTAssertTrue(phoneFocused(), "Next on Last name focuses the phone field")

        let waitingName = try XCTUnwrap(
            Self.elements(under: canvas.window).first {
                $0.accessibilityLabel == "Name" && $0.accessibilityTraits.contains(.button)
            },
            "the blank name waits behind a tap row"
        )
        waitingName.accessibilityActivate()
        let firstFocused = {
            canvas.views(of: UITextField.self).contains { $0.accessibilityLabel == "First name" && $0.isFirstResponder }
        }
        await canvas.settle(timeout: 5, until: firstFocused)
        XCTAssertTrue(firstFocused(), "a tap on the waiting Name row focuses First name")
    }

    /// The keypad is a bottom inset of its height, so the footer stands where the keyboard would push it.
    func testTypingKeepsTheFieldFocusedAndScrollsTheCardsEndOverTheFooter() async throws {
        let canvas = try await host(.typing, size: Self.phone, keypad: Self.keypad)
        defer { canvas.close() }
        await canvas.settle()
        let field = try XCTUnwrap(canvas.views(of: UITextField.self).first { $0.keyboardType == .numberPad })
        field.becomeFirstResponder()
        await canvas.settle { field.isFirstResponder }
        let scroll = try XCTUnwrap(Self.scrollView(in: canvas))
        let footerTop = { canvas.frame(of: scroll).maxY }

        canvas.postKeyboard(showing: true)
        await canvas.settle { (self.row("Birth date", in: canvas)?.frame.maxY ?? .infinity) <= footerTop() + 0.5 }

        XCTAssertTrue(field.isFirstResponder)
        let reads = Self.reads(in: canvas.window)
        XCTAssertFalse(reads.contains { $0.label == "Skip for now" }, "Skip hides while typing")
        let save = try XCTUnwrap(reads.first { $0.label == "Save and continue" })
        XCTAssertLessThanOrEqual(save.frame.maxY, Self.phone.height - Self.keypad + 0.5)
        XCTAssertTrue(Self.example(in: canvas)?.contains("+420 777 12") == true, Self.example(in: canvas) ?? "")
        let birth = try XCTUnwrap(row("Birth date", in: canvas))
        XCTAssertLessThanOrEqual(birth.frame.maxY, footerTop() + 0.5)
        XCTAssertGreaterThanOrEqual(birth.frame.maxY, footerTop() - 8, "the card's end sits on the footer")
    }

    // MARK: - Harness

    private func host(
        _ state: OnboardingSample,
        size: CGSize,
        keypad: CGFloat = 0,
        language: String = "en",
        dark: Bool = false
    ) async throws -> HostedCanvas {
        let vm = await state.viewModel()
        return try HostedCanvas(size: size) {
            OnboardingMockFrame(vm: vm, keypad: keypad)
                .environment(\.locale, Locale(identifier: language))
                .preferredColorScheme(dark ? .dark : .light)
        }
    }

    private func use(_ language: String) throws {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
        CoreL10n.apply(languageTag: language)
    }

    private func assertFits(_ canvas: HostedCanvas, _ name: String) throws {
        let scroll = try XCTUnwrap(Self.scrollView(in: canvas), name)
        let footerTop = canvas.frame(of: scroll).maxY
        let birth = try XCTUnwrap(
            Self.reads(in: canvas.window).first { $0.label.hasPrefix(L10n.Onboarding.birthDateLabel) },
            "\(name): no Birth date row"
        )
        XCTAssertLessThanOrEqual(birth.frame.maxY, footerTop + 0.5, "\(name): the card runs under the footer")
        XCTAssertLessThanOrEqual(scroll.contentSize.height, scroll.bounds.height + 0.5, "\(name): it scrolls")
    }

    private func row(_ label: String, in canvas: HostedCanvas) -> AccessibilityRead? {
        Self.reads(in: canvas.window).first { $0.label == label || $0.label.hasPrefix(label + ",") }
    }

    private static func example(in canvas: HostedCanvas) -> String? {
        reads(in: canvas.window).first { $0.label.hasPrefix(L10n.Onboarding.cardTitle) }?.label
    }

    private static func scrollView(in canvas: HostedCanvas) -> UIScrollView? {
        canvas.views(of: UIScrollView.self).max { $0.bounds.height < $1.bounds.height }
    }

    static func reads(in window: UIWindow) -> [AccessibilityRead] {
        elements(under: window).map {
            AccessibilityRead(
                label: $0.accessibilityLabel ?? "",
                value: $0.accessibilityValue ?? "",
                frame: $0.accessibilityFrame,
                traits: $0.accessibilityTraits
            )
        }
    }

    private static func elements(under node: NSObject, depth: Int = 0) -> [NSObject] {
        guard depth < 80 else { return [] }
        if node.isAccessibilityElement { return [node] }
        if let children = node.accessibilityElements as? [NSObject], !children.isEmpty {
            return children.flatMap { elements(under: $0, depth: depth + 1) }
        }
        let count = node.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            return (0 ..< count).compactMap { node.accessibilityElement(at: $0) as? NSObject }
                .flatMap { elements(under: $0, depth: depth + 1) }
        }
        return ((node as? UIView)?.subviews ?? []).flatMap { elements(under: $0, depth: depth + 1) }
    }
}

struct AccessibilityRead {
    let label: String
    let value: String
    let frame: CGRect
    let traits: UIAccessibilityTraits
}

/// The mock's sample states (`data.js`): Jana Dvořáková, a relay address, a long address, a full and a partial
/// phone, and 14 March 1991.
enum OnboardingSample: String, CaseIterable {
    case empty
    case typing
    case filled
    case csDark = "cs-dark"
    case sheet
    case applePrivate = "apple-private"
    case longEmail = "long-email"

    static let birthDate = CalendarDay.calendar.date(from: DateComponents(year: 1991, month: 3, day: 14))

    var user: CurrentUserProfile {
        switch self {
        case .applePrivate:
            ProfileFixtures.user(
                email: "x7k2p9q8r4@privaterelay.appleid.com",
                firstName: "Cleansia",
                lastName: "Customer",
                phoneNumber: nil,
                isNamePlaceholder: true
            )
        case .longEmail: Self.jana(email: "jana.dvorakova.vinohrady.praha@gmail.com")
        case .filled, .csDark: Self.jana(phone: "+420777123456", birthDate: Self.birthDate)
        case .sheet: Self.jana(phone: "+420777123456")
        case .typing: Self.jana(phone: "+42077712")
        case .empty: Self.jana()
        }
    }

    @MainActor
    func viewModel() async -> ProfileViewModel {
        let client = FakeUserProfileClient()
        client.currentUserResult = .success(user)
        let vm = ProfileViewModel(
            repository: UserProfileRepository(client: client),
            settings: UserDefaultsAppSettingsStore(
                defaults: UserDefaults(suiteName: "onboarding.\(UUID().uuidString)") ?? .standard
            ),
            snackbar: SnackbarController()
        )
        await vm.refresh()
        return vm
    }

    private static func jana(
        email: String = "jana.dvorakova@gmail.com",
        phone: String? = nil,
        birthDate: Date? = nil
    ) -> CurrentUserProfile {
        ProfileFixtures.user(
            email: email,
            firstName: "Jana",
            lastName: "Dvořáková",
            phoneNumber: phone,
            birthDate: birthDate
        )
    }
}

/// The screen in the mock's frame: 54 over it and 34 under it, in the ground colour. While typing, a bottom inset of
/// the keypad's height stands in for the keyboard and the 34.
struct OnboardingMockFrame: View {
    let vm: ProfileViewModel
    var keypad: CGFloat = 0
    var keypadColor = Color.clear

    var body: some View {
        ProfileOnboardingView(makeViewModel: { vm }, onDone: {})
            .padding(.top, 54)
            .padding(.bottom, keypad > 0 ? 0 : 34)
            .background(CleansiaColors.background)
            .safeAreaInset(edge: .bottom, spacing: 0) {
                keypadColor.frame(height: keypad)
            }
    }
}
