import XCTest

/// Text links and text buttons take the text ink, not the primary (owner decision 2026-10-05): the light-mode
/// primary, sky-600, reads 4.10:1 on white, under the 4.5:1 floor for text, so they take
/// `CleansiaColors.primaryText` (sky-700, 5.93:1; dark mode is the primary's sky-400 either way). An icon
/// inside the same control takes its label's ink, so no control shows two blues; fills, borders, filled
/// buttons and standalone icons keep the primary. These are the partner app's own text buttons; the shared
/// ones (`CleansiaTextLink`, the chip, the dropdown, the reveal panel) are pinned in Core.
final class TextInkTests: XCTestCase {
    func testTheTabBarAndTheToolbarsTextButtonsTakeTheTextInk() throws {
        let shell = try compactSource("Shell/PartnerShellView.swift")
        XCTAssertTrue(shell.contains(".tint(CleansiaColors.primaryText)}}"), "the tab bar's tint")
        XCTAssertFalse(shell.contains(".tint(CleansiaColors.primary)"))
        XCTAssertTrue(
            try compactSource("Notifications/NotificationsInboxSheet.swift")
                .contains("Button(L10n.NotificationsInbox.close){dismiss()}.tint(CleansiaColors.primaryText)")
        )
    }

    /// The registration lock's stack is mounted beside the shell, not inside it, so it carries the shell's tint
    /// itself: without it the profile sections it pushes drew their back buttons in iOS's own blue, 4.0:1 on
    /// white, where the same sections reached from Profile draw the text ink (finding 2026-10-05).
    func testTheRegistrationLocksStackTakesTheShellsTint() throws {
        let source = try compactSource("RegistrationLock/RegistrationLockView.swift")
        let stack = try XCTUnwrap(
            source.range(of: ".navigationDestination(for:ProfileRoute.self,destination:sectionDestination)}")
        )
        let task = try XCTUnwrap(source.range(of: ".task{", range: stack.upperBound ..< source.endIndex))
        XCTAssertTrue(source[stack.upperBound ..< task.lowerBound].contains(".tint(CleansiaColors.primaryText)"))
    }

    /// Each site's label, and the icon beside it where it has one, named in the text ink.
    private static let sites: [(path: String, snippet: String)] = [
        (
            "Dashboard/DashboardCards.swift",
            "Text(L10n.Dashboard.earningsViewDetails).font(CleansiaTypography.labelMedium)"
                + ".foregroundColor(CleansiaColors.primaryText)Image(systemName:\"arrow.right\")"
                + ".font(.system(size:12)).foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Profile/ProfileAvatarField.swift",
            "Text(L10n.Profile.photoAdd).font(CleansiaTypography.labelLarge)"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Earnings/InvoiceDetailContent.swift",
            "Text(L10n.Invoices.viewPeriodPay).font(CleansiaTypography.bodyLarge)"
                + ".foregroundColor(CleansiaColors.primaryText)Spacer()Image(systemName:\"chevron.right\")"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Orders/OrderDetailCards.swift",
            "Label(label,systemImage:icon).font(CleansiaTypography.labelLarge)"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Orders/OrderDetailCards.swift",
            "Label(L10n.Orders.copyInstruction,systemImage:\"doc.on.doc\").font(CleansiaTypography.labelMedium)"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Orders/OrdersListContent.swift",
            "Button(L10n.Orders.locationPromptAction,action:onEnable).font(CleansiaTypography.labelLarge)"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Orders/OrdersListContent.swift",
            "Image(systemName:\"chevron.down\").font(.system(size:12))}"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Onboarding/OnboardingView.swift",
            "Image(systemName:\"chevron.down\").font(.system(size:11,weight:.semibold))}"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Orders/PendingOffersCard.swift",
            "Text(L10n.Offers.cardCta).font(CleansiaTypography.labelLarge)"
                + ".foregroundColor(CleansiaColors.primaryText)Image(systemName:\"arrow.right\")"
                + ".font(.system(size:13)).foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "RegistrationLock/RegistrationLockView.swift",
            "Text(L10n.RegistrationLock.actionContactSupport).font(CleansiaTypography.labelMedium)"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ),
        (
            "Orders/PhotosSection.swift",
            "Text(L10n.Orders.addPhoto).font(CleansiaTypography.labelSmall)}"
                + ".foregroundColor(CleansiaColors.primaryText)"
        )
    ]

    func testTheTextButtonsAndTheIconsBesideThemTakeTheTextInk() throws {
        for site in Self.sites {
            let source = try compactSource(site.path)
            XCTAssertTrue(source.contains(site.snippet), "\(site.path): \(site.snippet.prefix(60))")
        }
    }

    /// Blue text on the primary container takes its text ink, which reads 4.5:1 in both modes (finding
    /// 2026-10-05: the primary read 3.57:1 on sky-100 in light mode and 2.77:1 on sky-700 in dark).
    func testTheBadgesOnThePrimaryContainerTakeItsTextInk() throws {
        XCTAssertTrue(try compactSource("Devices/DevicesView.swift").contains(
            "Text(L10n.Devices.thisDevice).font(CleansiaTypography.labelSmall)"
                + ".foregroundColor(CleansiaColors.primaryTextOnContainer)"
        ))
        let badge = try compactSource("Earnings/InvoiceStatusBadge.swift")
        XCTAssertTrue(badge.contains("case._1:CleansiaColors.primaryContainer"))
        XCTAssertTrue(badge
            .contains("privatevarforeground:Color{switchstatus{case._1:CleansiaColors.primaryTextOnContainer"))
    }

    private func compactSource(_ path: String) throws -> String {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features")
            .appendingPathComponent(path)
        return try String(contentsOf: url, encoding: .utf8).components(separatedBy: .whitespacesAndNewlines).joined()
    }
}
