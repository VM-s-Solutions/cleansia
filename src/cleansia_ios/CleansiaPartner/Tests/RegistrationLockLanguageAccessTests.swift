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

    func testTheLanguageRouteIsNotAmongTheUnbuiltOnes() throws {
        let source = try read(Self.lockView)
        let unbuilt = try XCTUnwrap(source.range(of: "case .emergency, .jobRadius, .theme, .devices:"))
        XCTAssertFalse(source[unbuilt].contains(".language"), "the language route fell back to EmptyView")
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
