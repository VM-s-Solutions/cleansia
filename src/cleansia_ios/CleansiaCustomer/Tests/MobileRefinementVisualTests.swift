import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

@MainActor
final class MobileRefinementVisualTests: XCTestCase {
    func testRegistrationCredentialsRenderAsSeparateNativeFieldsInBothThemes() async throws {
        let model = CustomerAuthViewModel(
            loginClient: FakeLoginClient(), registrationClient: FakeRegistrationClient(),
            emailConfirmationClient: FakeEmailConfirmationClient(), passwordResetClient: FakePasswordResetClient(),
            socialAuthClient: FakeSocialAuthClient(), socialProvider: FakeSocialSignInProvider(),
            settings: FakeAppSettingsStore(), snackbar: SnackbarController()
        )
        model.onFirstNameChange("Jana")
        model.onLastNameChange("Nováková")
        model.onSignUpEmailChange("jana@example.test")
        model.onSignUpPasswordChange("Example-password-7")
        model.onConfirmPasswordChange("Example-password-7")
        for scheme in [ColorScheme.light, .dark] {
            let canvas = try HostedCanvas(size: CGSize(width: 390, height: 1000)) {
                SignUpView(makeViewModel: { model }, onSignIn: {}, onOutcome: { _ in })
                    .preferredColorScheme(scheme)
            }
            await canvas.settle()
            let fields = canvas.views(of: UITextField.self)
            XCTAssertEqual(fields.filter { $0.textContentType == .username }.map(\.text), ["jana@example.test"])
            XCTAssertEqual(
                fields.filter { $0.textContentType == .newPassword }.map(\.text),
                ["Example-password-7", "Example-password-7"]
            )
            attach(canvas, name: "Refinement registration filled synthetic credentials \(scheme)")
            canvas.close()
        }
    }

    func testAssignedCleanerPhotoAndVisibleNameInitialsRenderInBothThemes() async throws {
        let bytes = try XCTUnwrap(ProfileFixtures.image().jpegData(compressionQuality: 0.8))
        let cache = RemoteImageCache(loader: { _ in bytes })
        let employees = [
            AssignedEmployeeDto(fullName: "Jana Nováková", profilePhoto: BlobFileDto(
                fileName: "synthetic-cleaner-portrait.jpg", blobUrl: "https://example.invalid/synthetic-avatar"
            )),
            AssignedEmployeeDto(fullName: "Alex Novák"),
            AssignedEmployeeDto(fullName: "J*** N***")
        ]
        for scheme in [ColorScheme.light, .dark] {
            let canvas = try HostedCanvas(size: CGSize(width: 390, height: 200)) {
                AssignedCleanersCard(employees: employees, imageCache: cache)
                    .padding(Spacing.m)
                    .frame(maxHeight: .infinity, alignment: .top)
                    .background(CleansiaColors.background)
                    .preferredColorScheme(scheme)
            }
            await canvas.settle()
            try await Task.sleep(nanoseconds: 150_000_000)
            attach(canvas, name: "Refinement assigned cleaners synthetic photo and initials \(scheme)")
            canvas.close()
        }
    }

    /// Drives the native pager's scroll position; this exercises its selection callbacks, not an OS
    /// touch event. The selected marker must remain visible after that manual-control path pauses it.
    func testCarouselProgressStaysVisibleAfterNativePaging() async throws {
        let canvas = try HostedCanvas(size: CGSize(width: 390, height: 844)) {
            UpsellCarousel(inputs: UpsellSlide.Inputs(), onAction: { _ in })
                // Home's address header places the pager below the status safe area.
                .padding(.top, 80)
                .frame(maxHeight: .infinity, alignment: .top)
                .background(CleansiaColors.background)
                .environment(\.scenePhase, .active)
        }
        defer { canvas.close() }
        await canvas.settle()
        try await Task.sleep(nanoseconds: 1_500_000_000)
        attach(canvas, name: "Refinement carousel timed progress before native paging")
        let pager = try XCTUnwrap(canvas.views(of: UIScrollView.self).first { $0.isPagingEnabled })
        let next = min(pager.contentSize.width - pager.bounds.width, pager.contentOffset.x + pager.bounds.width)
        XCTAssertGreaterThan(next, pager.contentOffset.x)
        pager.setContentOffset(CGPoint(x: next, y: pager.contentOffset.y), animated: true)
        try await Task.sleep(nanoseconds: 600_000_000)
        await canvas.settle()
        attach(canvas, name: "Refinement carousel selected progress after native paging")
    }

    private func attach(_ canvas: HostedCanvas, name: String) {
        let image = UIGraphicsImageRenderer(bounds: canvas.window.bounds).image { _ in
            canvas.window.drawHierarchy(in: canvas.window.bounds, afterScreenUpdates: true)
        }
        let attachment = XCTAttachment(image: image)
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }
}
