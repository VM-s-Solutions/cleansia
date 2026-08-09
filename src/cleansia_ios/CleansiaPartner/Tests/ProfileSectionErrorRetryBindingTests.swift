import XCTest
@testable import CleansiaPartner

/// Every section view model already answered a failed load with `.error`, and four of the five views
/// ignored it and drew the empty form anyway — a cleaner whose network blipped saw a blank profile and
/// could save over it. The view models stayed green throughout, because the defect is entirely in what
/// the view hands the scaffold, and SwiftUI offers no seam to assert a rendered branch through.
final class ProfileSectionErrorRetryBindingTests: XCTestCase {
    private static let sectionViews = [
        "CleansiaPartner/Sources/Features/Profile/Personal/PersonalSectionView.swift",
        "CleansiaPartner/Sources/Features/Profile/Address/AddressSectionView.swift",
        "CleansiaPartner/Sources/Features/Profile/Identification/IdentificationSectionView.swift",
        "CleansiaPartner/Sources/Features/Profile/Bank/BankSectionView.swift",
        "CleansiaPartner/Sources/Features/Profile/Emergency/EmergencySectionView.swift",
        "CleansiaPartner/Sources/Features/Profile/JobRadius/JobRadiusSectionView.swift"
    ]

    func testEverySectionHandsTheScaffoldItsErrorState() throws {
        for path in Self.sectionViews {
            let call = try scaffoldCall(in: read(path))
            XCTAssertTrue(
                call.contains("isError: isError"),
                "\(path) falls through to an editable form on a failed load"
            )
            XCTAssertTrue(
                call.contains("onRetry: { Task { await vm.load() } }"),
                "\(path) leaves the cleaner with no way back"
            )
        }
    }

    func testTheErrorFlagIsDerivedFromTheViewModelState() throws {
        for path in Self.sectionViews {
            XCTAssertTrue(
                try read(path).contains("if case .error = vm.state { return true }"),
                "\(path) no longer derives its error flag from the loaded state"
            )
        }
    }

    /// Scoped to the body's own call: `BankSectionView` also builds scaffolds in its previews, one of
    /// which passes `isError: true` and would satisfy a whole-file search on its own.
    private func scaffoldCall(in source: String) throws -> Substring {
        let start = try XCTUnwrap(source.range(of: "SectionScaffold("), "no scaffold in this section")
        let end = try XCTUnwrap(
            source.range(of: ".task { await vm.load() }", range: start.upperBound ..< source.endIndex),
            "the scaffold no longer loads on appear"
        )
        return source[start.lowerBound ..< end.lowerBound]
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
