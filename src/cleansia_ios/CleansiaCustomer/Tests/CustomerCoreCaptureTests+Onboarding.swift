import CleansiaCore
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The onboarding in each of the mock's states, in the mock's frame (54 over the screen, 34 under it), so each frame
/// sits beside the mock's shot of the same state.
extension CustomerCoreCaptureTests {
    func testOnboarding() async throws {
        let sizes = [ProfileOnboardingLayoutTests.phone, ProfileOnboardingLayoutTests.mini]
        for language in ["en", "cs"] {
            for theme in ["light", "dark"] {
                for size in sizes {
                    for state in [OnboardingSample.empty, .filled, .applePrivate] {
                        try await onboardingScreen(state, size, language, theme)
                    }
                    try await onboardingSheet(size, language, theme)
                    try await onboardingTyping(size, language, theme)
                }
                try await onboardingScreen(.longEmail, ProfileOnboardingLayoutTests.mini, language, theme)
            }
        }
    }

    private func onboardingScreen(
        _ state: OnboardingSample,
        _ size: CGSize,
        _ language: String,
        _ theme: String
    ) async throws {
        try use(language)
        let vm = await state.viewModel()
        await CustomerCapture.shoot(
            name("onboarding", state.rawValue, size, language, theme),
            size: size,
            to: self,
            hold: 0.3
        ) {
            OnboardingMockFrame(vm: vm)
                .frame(width: size.width, height: size.height)
                .environment(\.locale, Locale(identifier: language))
                .preferredColorScheme(theme == "dark" ? .dark : .light)
        }
    }

    /// The sheet as `bookingSheet` composes one: over the dimmed screen, in the bottom half, with no system grabber.
    private func onboardingSheet(_ size: CGSize, _ language: String, _ theme: String) async throws {
        try use(language)
        let vm = await OnboardingSample.sheet.viewModel()
        await CustomerCapture.shoot(
            name("onboarding", "sheet", size, language, theme),
            size: size,
            to: self,
            hold: 0.5
        ) {
            ZStack(alignment: .bottom) {
                OnboardingMockFrame(vm: vm)
                Color.black.opacity(0.4)
                BirthDateWheelSheet(
                    birthDate: .constant(nil),
                    title: L10n.Onboarding.birthDateLabel,
                    note: L10n.Onboarding.birthDateHelper
                )
                .frame(height: size.height / 2 + 20)
                .clipShape(RoundedRectangle(cornerRadius: 20))
                .offset(y: 20)
            }
            .frame(width: size.width, height: size.height)
            .environment(\.locale, Locale(identifier: language))
            .preferredColorScheme(theme == "dark" ? .dark : .light)
        }
    }

    /// `shoot` attaches straight after its own settle, so the typing frame is hosted here: the phone field takes the
    /// focus, the keyboard's notifications run the screen's scroll, and a block of the keypad's height stands in for
    /// it in the picture.
    private func onboardingTyping(_ size: CGSize, _ language: String, _ theme: String) async throws {
        try use(language)
        let vm = await OnboardingSample.typing.viewModel()
        let keypad = theme == "dark"
            ? Color(red: 44 / 255, green: 44 / 255, blue: 46 / 255)
            : Color(red: 209 / 255, green: 213 / 255, blue: 219 / 255)
        let canvas = try HostedCanvas(size: size) {
            OnboardingMockFrame(vm: vm, keypad: ProfileOnboardingLayoutTests.keypad, keypadColor: keypad)
                .environment(\.locale, Locale(identifier: language))
                .preferredColorScheme(theme == "dark" ? .dark : .light)
        }
        defer { canvas.close() }
        await canvas.settle()
        let field = try XCTUnwrap(canvas.views(of: UITextField.self).first { $0.keyboardType == .numberPad })
        field.becomeFirstResponder()
        await canvas.settle { field.isFirstResponder }
        canvas.postKeyboard(showing: true)
        try await Task.sleep(nanoseconds: 600_000_000)
        await canvas.settle()
        CustomerCapture.attach(
            canvas.window,
            name: name("onboarding", "typing", size, language, theme),
            crops: [],
            to: self
        )
    }
}
