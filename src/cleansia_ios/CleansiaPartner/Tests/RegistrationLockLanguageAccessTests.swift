import XCTest
@testable import CleansiaPartner

/// A cleaner who skipped or never saw the one-shot pre-auth intro is stuck on the registration lock
/// until an admin approves them, so the lock is their only language control. There is no seam to assert
/// a SwiftUI destination through, so pin the wiring at its source: the `.language` route resolved to
/// `EmptyView()` for a full release and nothing went red.
final class RegistrationLockLanguageAccessTests: XCTestCase {
    private static let lockView = "CleansiaPartner/Sources/Features/RegistrationLock/RegistrationLockView.swift"
    private static let rootView = "CleansiaPartner/Sources/PartnerRootView.swift"

    func testTheLanguageRouteResolvesToTheRealPicker() throws {
        let source = try read(Self.lockView)
        XCTAssertTrue(
            source.contains("case .language:\n            LanguagePickerView(preferences: preferences"),
            "the language route no longer pushes the picker"
        )
    }

    /// **Anchored on the `EmptyView()` fallback, not on the list of routes that reach it.** The switch
    /// has no `default` by design, so every new `ProfileRoute` is classified into one arm or the other —
    /// which means a verbatim `case .emergency, .jobRadius, …:` anchor goes stale on each addition and
    /// fails as a bare unwrap that names nothing. It did: `.deleteAccount` joined the arm in #215 and
    /// this test had been red ever since, unseen because the iOS suite was not running. What the test
    /// actually means is "`.language` is not in whichever arm renders nothing", so it now reads that arm.
    func testTheLanguageRouteIsNotAmongTheUnbuiltOnes() throws {
        let source = try read(Self.lockView)
        let fallback = try XCTUnwrap(
            source.range(of: "EmptyView()"),
            "no EmptyView arm left in the lock's route switch — this guard would assert nothing"
        )
        let arm = try XCTUnwrap(
            source.range(of: "case ", options: .backwards, range: source.startIndex ..< fallback.lowerBound),
            "found EmptyView() outside a case arm; the route switch has been restructured"
        )
        XCTAssertFalse(
            source[arm.lowerBound ..< fallback.lowerBound].contains(".language"),
            "the language route fell back to EmptyView"
        )
    }

    func testTheLockOffersSomethingToReachThePickerFrom() throws {
        let source = try read(Self.lockView)
        XCTAssertTrue(source.contains("LanguageRow(summary: languageSummary"), "no affordance to push from")
        XCTAssertTrue(source.contains("onLanguage: { path.append(ProfileRoute.language) }"), "row pushes nothing")
    }

    func testTheRootHandsTheLockTheLivePreferences() throws {
        let source = try read(Self.rootView)
        let call = try XCTUnwrap(source.range(of: "RegistrationLockView(")).lowerBound
        let closing = try XCTUnwrap(source.range(
            of: "onSignedOut: { route = .login }",
            range: call ..< source.endIndex
        ))
        XCTAssertTrue(
            source[call ..< closing.upperBound].contains("preferences: preferences"),
            "the lock cannot render or change the language without the live model"
        )
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
