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

    func testCompactOrderCountryPriceAndFullAddressInBothThemes() async throws {
        let address = OrderAddress(street: "Zenklova 6", city: "Praha 8", zipCode: "180 00", country: "Czech Republic")
        for (width, textSize) in [(CGFloat(390), DynamicTypeSize.large), (320, .accessibility3)] {
            for discounted in [false, true] {
                let order = OrderFixtures.detail(
                    statusCode: Code(value: 0), address: address, total: discounted ? 1411 : 1660,
                    originalSubtotal: 1660, promoDiscountAmount: discounted ? 249 : 0,
                    appliedDiscountSource: discounted ? ._3 : ._0, currencyCode: "CZK"
                )
                for scheme in [ColorScheme.light, .dark] {
                    let probes = LayoutProbes()
                    let canvas = try HostedCanvas(size: CGSize(width: width, height: 568)) {
                        ScrollView {
                            VStack(alignment: .leading, spacing: Spacing.xs) {
                                OrderHeroFactsStrip(order: order, marketLabel: "Czech Republic · CZK")
                                    .probed("facts", in: probes)
                                OrderAddressCard(address: address)
                                    .probed("address", in: probes)
                            }
                            .padding(Spacing.m)
                        }
                        .background(CleansiaColors.background)
                        .environment(\.locale, Locale(identifier: "en_GB"))
                        .environment(\.dynamicTypeSize, textSize)
                        .preferredColorScheme(scheme)
                    }
                    await canvas.settle(until: { probes.frames["address"] != nil })
                    for key in ["facts", "address"] {
                        let frame = try XCTUnwrap(probes.frames[key])
                        XCTAssertGreaterThanOrEqual(frame.minX, 0)
                        XCTAssertLessThanOrEqual(frame.maxX, width)
                    }
                    attach(
                        canvas,
                        name: "Compact order country price full address \(width)pt discount \(discounted) \(scheme)"
                    )
                    canvas.close()
                }
            }
        }
    }

    /// Native paging exercises selection and settling callbacks without relying on a synthetic tap.
    func testCarouselResumesWithAFreshIntervalAfterNativePaging() async throws {
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
        let originalPage = try XCTUnwrap(selectedPage(in: pager, hostedBy: canvas.host))
        let next = min(pager.contentSize.width - pager.bounds.width, pager.contentOffset.x + pager.bounds.width)
        XCTAssertGreaterThan(next, pager.contentOffset.x)
        pager.setContentOffset(CGPoint(x: next, y: pager.contentOffset.y), animated: true)
        try await Task.sleep(nanoseconds: 600_000_000)
        await canvas.settle(until: { selectedPage(in: pager, hostedBy: canvas.host) != originalPage })
        let manuallySelected = try XCTUnwrap(selectedPage(in: pager, hostedBy: canvas.host))
        XCTAssertNotEqual(manuallySelected, originalPage, "native paging must select a different offer")
        attach(canvas, name: "Refinement carousel selected progress after native paging")
        try await Task.sleep(nanoseconds: 3_000_000_000)
        XCTAssertEqual(
            selectedPage(in: pager, hostedBy: canvas.host), manuallySelected,
            "manual selection receives a fresh interval"
        )
        attach(canvas, name: "Refinement carousel live progress three seconds after manual paging")
        try await Task.sleep(nanoseconds: 3_800_000_000)
        await canvas.settle(until: {
            guard let current = selectedPage(in: pager, hostedBy: canvas.host) else { return false }
            return current != manuallySelected
        })
        XCTAssertNotEqual(
            try XCTUnwrap(selectedPage(in: pager, hostedBy: canvas.host)), manuallySelected,
            "autoplay must resume after manual paging"
        )
        attach(canvas, name: "Refinement carousel automatically advances again after manual paging")
    }

    private func selectedPage(in pager: UIScrollView, hostedBy root: UIViewController) -> AnyHashable? {
        if let collection = pager as? UICollectionView {
            return collection.indexPathForItem(at: CGPoint(x: collection.bounds.midX, y: collection.bounds.midY))
                .map(AnyHashable.init)
        }
        return pageController(in: root)?.viewControllers?.first.map { AnyHashable(ObjectIdentifier($0)) }
    }

    private func pageController(in controller: UIViewController) -> UIPageViewController? {
        if let page = controller as? UIPageViewController { return page }
        return controller.children.lazy.compactMap { self.pageController(in: $0) }.first
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
