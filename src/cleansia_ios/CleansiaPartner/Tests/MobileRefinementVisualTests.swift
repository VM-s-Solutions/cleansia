import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UIKit
import XCTest
@testable import CleansiaPartner

@MainActor
final class MobileRefinementVisualTests: XCTestCase {
    func testPartnerOvertimeUsesTheSharedCustomerTrackerSurface() async {
        var order = OrderDetail.preview
        order.estimatedMinutes = 60
        for scheme in [ColorScheme.light, .dark] {
            await capture(name: "Refinement Partner overtime \(scheme)", size: CGSize(width: 390, height: 240)) {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    OrderTimerCard(order: order, locale: Locale(identifier: "en"), showsStepCounter: true)
                    OrderTrackerHero(status: order.status, showsStepCounter: false, order: order)
                }
                .padding(Spacing.m)
                .frame(maxHeight: .infinity, alignment: .top)
                .background(CleansiaColors.surface)
                .preferredColorScheme(scheme)
                .environment(\.scenePhase, .active)
            }
        }
    }

    func testPartnerCodeFormHasNoSeparatePasteAccessory() async throws {
        let domain = "cz.cleansia.tests.refinement." + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: domain))
        defer { defaults.removePersistentDomain(forName: domain) }
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in ["en", "uk"] {
            L10n.bundle = try localeBundle(language)
            await capture(
                name: "Refinement Partner confirmation code without Paste accessory \(language)",
                size: CGSize(width: 390, height: 844)
            ) {
                ConfirmEmailView(
                    email: "cleaner@example.test", registeredPassword: "Example-password-7",
                    client: ConfirmationFixture(), settings: UserDefaultsAppSettingsStore(defaults: defaults),
                    snackbar: SnackbarController(), onBack: {}, onConfirmed: {}
                )
            }
        }
    }

    func testPartnerPendingPhotoUsesOnlyTheBottomPersonalSave() async {
        let client = FakePartnerProfileClient()
        client.employeeResult = .success(EmployeeItem(
            id: "synthetic-cleaner", email: "cleaner@example.test", firstName: "Jana", lastName: "Nováková",
            phoneNumber: "+420777111222",
            birthDate: OpenAPIDateWithoutTime(day: Date(timeIntervalSince1970: 662_688_000))
        ))
        let avatar = ProfileAvatarViewModel(client: FakePartnerUserClient(), snackbar: SnackbarController())
        await avatar.load()
        let image = UIGraphicsImageRenderer(size: CGSize(width: 64, height: 64)).image { context in
            UIColor.systemTeal.setFill()
            context.fill(CGRect(x: 0, y: 0, width: 64, height: 64))
        }
        avatar.pick(image)
        await capture(
            name: "Refinement Partner staged photo and one bottom Save",
            size: CGSize(width: 390, height: 1000)
        ) {
            PersonalSectionView(
                client: client, snackbar: SnackbarController(), chainVM: OnboardingChainViewModel(client: client),
                onboarding: false, avatar: avatar, avatarCache: RemoteImageCache(), onSaved: {}
            )
        }
        XCTAssertTrue(avatar.hasPendingEdit, "rendering the form must not save the picked photo")
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: tag, ofType: "lproj")
        }.first
        return try XCTUnwrap(Bundle(path: XCTUnwrap(path)))
    }

    private func capture(name: String, size: CGSize, @ViewBuilder content: () -> some View) async {
        let previous = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows).first { $0.isKeyWindow }
        let host = UIHostingController(rootView: content())
        if #available(iOS 16.4, *) { host.safeAreaRegions = [] }
        let window = previous?.windowScene.map(UIWindow.init(windowScene:)) ?? UIWindow(frame: CGRect(
            origin: .zero,
            size: size
        ))
        window.frame = CGRect(origin: .zero, size: size)
        window.rootViewController = host
        window.makeKeyAndVisible()
        defer {
            window.endEditing(true)
            window.isHidden = true
            window.rootViewController = nil
            previous?.makeKeyAndVisible()
        }
        for _ in 0 ..< 6 {
            try? await Task.sleep(nanoseconds: 50_000_000)
            host.view.layoutIfNeeded()
        }
        let image = UIGraphicsImageRenderer(bounds: window.bounds).image { _ in
            window.drawHierarchy(in: window.bounds, afterScreenUpdates: true)
        }
        let attachment = XCTAttachment(image: image)
        attachment.name = name
        attachment.lifetime = .keepAlways
        add(attachment)
    }
}

private final class ConfirmationFixture: EmailConfirmationClient {
    func confirmEmail(email _: String, code _: String, password _: String) async -> ApiResult<LoginOutcome> {
        .success(.authenticated)
    }

    func resendConfirmation(email _: String, language _: String) async -> ApiResult<Bool> {
        .success(true)
    }
}
