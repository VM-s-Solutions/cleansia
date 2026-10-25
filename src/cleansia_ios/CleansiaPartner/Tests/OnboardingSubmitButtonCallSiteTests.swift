import XCTest

/// A button held disabled until its form is valid never lets the form's field errors render: the cleaner
/// meets a dead button and no reason for it. Sign-up and the four onboarding steps disable theirs only
/// while they submit, and their view models name every problem under its field instead.
final class OnboardingSubmitButtonCallSiteTests: XCTestCase {
    func testSignUpIsDisabledOnlyWhileItRegisters() throws {
        let call = try callSite(of: "CleansiaPrimaryButton(", in: "Auth/RegisterView.swift")
        XCTAssertTrue(call.contains("loading: isLoading"))
        XCTAssertFalse(call.contains("enabled:"), "sign-up must not wait for a valid form to be tappable")
    }

    func testEveryOnboardingStepIsDisabledOnlyWhileItSaves() throws {
        for view in [
            "Profile/Personal/PersonalSectionView.swift",
            "Profile/Address/AddressSectionView.swift",
            "Profile/Identification/IdentificationSectionView.swift",
            "Profile/Bank/BankSectionView.swift"
        ] {
            let call = try callSite(of: "SaveSectionButton(", in: view)
            XCTAssertTrue(call.contains("isSubmitting: vm.action.isSubmitting"), view)
            XCTAssertFalse(call.contains("enabled:"), "\(view) must not wait for a valid form to be tappable")
        }
    }

    private func callSite(of opening: String, in view: String) throws -> Substring {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features")
        let source = try String(contentsOf: root.appendingPathComponent(view), encoding: .utf8)
        let start = try XCTUnwrap(source.range(of: opening), "\(opening) not found in \(view)")
        let end = try XCTUnwrap(
            source.range(of: "action:", range: start.upperBound ..< source.endIndex),
            "the call in \(view) no longer names its action"
        )
        return source[start.lowerBound ..< end.lowerBound]
    }
}
