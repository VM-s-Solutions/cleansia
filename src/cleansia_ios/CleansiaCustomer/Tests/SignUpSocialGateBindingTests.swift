import XCTest
@testable import CleansiaCustomer

/// The one hop no view-model suite can see: which entry point each screen's provider buttons are
/// wired to. Both screens own an identical `CustomerAuthViewModel` and identical buttons, so a
/// signup screen pointed back at the sign-in methods compiles, passes every test, and quietly
/// stops provisioning anyone.
final class SignUpSocialGateBindingTests: XCTestCase {
    private static let signUp = "CleansiaCustomer/Sources/Features/Auth/SignUpView.swift"
    private static let signIn = "CleansiaCustomer/Sources/Features/Auth/SignInView.swift"

    func testTheSignUpScreenTapsTheConsentAssertingEntryPoints() throws {
        let source = try read(Self.signUp)
        XCTAssertTrue(source.contains("vm.signUpWithApple()"), "Apple signup is wired to sign-in")
        XCTAssertTrue(source.contains("vm.signUpWithGoogle()"), "Google signup is wired to sign-in")
        XCTAssertFalse(source.contains("vm.signInWith"), "a signup button still asserts nothing")
    }

    func testTheSignInScreenTapsTheEntryPointsThatAssertNothing() throws {
        let source = try read(Self.signIn)
        XCTAssertTrue(source.contains("vm.signInWithApple()"))
        XCTAssertTrue(source.contains("vm.signInWithGoogle()"))
        XCTAssertFalse(source.contains("vm.signUpWith"), "sign-in would provision an unconsented account")
    }

    /// A dead control with no explanation is a dead end, so the lock note is not optional garnish:
    /// it is the only thing on screen that names the box the buttons are waiting on.
    func testTheSignUpScreenExplainsTheLockWhileTheBoxIsUnticked() throws {
        let source = try read(Self.signUp)
        XCTAssertTrue(
            source.contains("lockNote: form.acceptTerms ? nil : L10n.Auth.socialTermsRequired"),
            "the locked social section says nothing about why"
        )
    }

    func testTheSignInScreenCarriesNoLock() throws {
        XCTAssertTrue(try read(Self.signIn).contains("lockNote: nil"))
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
