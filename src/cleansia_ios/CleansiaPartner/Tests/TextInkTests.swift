import CleansiaPartnerApi
import SwiftUI
import XCTest
@testable import CleansiaPartner

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
            .contains("varforeground:Color{switchstatus{case._1:CleansiaColors.primaryTextOnContainer"))
    }

    /// Every invoice status pill's label reads 4.5:1 or more on its fill in both modes (finding 2026-10-05):
    /// "Approved" was white on the light primary, sky-600 (4.10:1), and the dark primary's ink on sky-400
    /// (4.42:1); it is white on sky-700 in both modes now, as Android's.
    func testEveryInvoiceStatusPillReadsOnItsFillInBothModes() {
        let statuses: [EmployeeInvoiceStatus?] = EmployeeInvoiceStatus.allCases + [nil]
        for style in [UIUserInterfaceStyle.light, .dark] {
            for status in statuses {
                let badge = InvoiceStatusBadge(status: status)
                XCTAssertGreaterThanOrEqual(
                    contrast(rgb(badge.foreground, style), rgb(badge.background, style)),
                    4.5,
                    "\(String(describing: status)), style \(style.rawValue)"
                )
            }
            let approved = InvoiceStatusBadge(status: ._2)
            XCTAssertEqual(
                contrast(rgb(approved.foreground, style), rgb(approved.background, style)),
                5.93,
                accuracy: 0.01
            )
        }
    }

    /// The partner app's informational blue text — card eyebrows, pay amounts, step counters and names, status
    /// words, the address "why" bullets — reads in the text ink as its links do (owner decision 2026-10-05, Y3):
    /// no partner `Text` names the primary in its own modifier chain.
    func testNoPartnerTextIsDrawnInThePrimary() throws {
        let found = try primaryTexts()

        XCTAssertTrue(found.isEmpty, found.map { "\($0.file): \($0.text)" }.joined(separator: "\n"))
    }

    /// The texts whose ink arrives through a value rather than their own `.foregroundColor`, and the icons on
    /// the same line as one of them.
    func testTheTextsInkedThroughAValueTakeTheTextInk() throws {
        XCTAssertTrue(
            try compactSource("Dashboard/DashboardCards.swift")
                .contains("isUp?CleansiaColors.primaryText:CleansiaColors.error"),
            "the month delta chip"
        )
        XCTAssertTrue(
            try compactSource("Orders/OrdersListComponents.swift")
                .contains("label:L10n.Orders.startsSoon,tint:CleansiaColors.primaryText"),
            "Starts soon"
        )
        XCTAssertTrue(try compactSource("Profile/OnboardingChainHeader.swift")
            .contains("privatevarlabelColor:Color{switchstate{case.current:CleansiaColors.primaryText"))
        XCTAssertTrue(try compactSource("Profile/LegalDocuments/LegalDocumentsView.swift")
            .contains(".foregroundColor(document.isAccepted?CleansiaColors.primaryText:CleansiaColors.error)"))
        XCTAssertTrue(try compactSource("Orders/PendingOfferComponents.swift").contains(
            "Image(systemName:\"clock\").font(.system(size:14)).foregroundColor(CleansiaColors.primaryText)"
        ), "the clock beside \"Yours until\"")
    }

    private var featureSources: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features")
    }

    /// Every `Text(…)` whose own modifier chain sets a primary foreground (Core's `ComponentTextInkTests` scan).
    private func primaryTexts() throws -> [(file: String, text: String)] {
        let files = try XCTUnwrap(FileManager.default.enumerator(at: featureSources, includingPropertiesForKeys: nil))
            .compactMap { $0 as? URL }
            .filter { $0.pathExtension == "swift" }
        XCTAssertGreaterThan(files.count, 50, "the partner sources were not found")
        let primary = try NSRegularExpression(pattern: #"CleansiaColors\.primary(?![A-Za-z])"#)
        var found: [(file: String, text: String)] = []
        for file in files {
            let lines = try String(contentsOf: file, encoding: .utf8)
                .components(separatedBy: .newlines)
                .map { $0.trimmingCharacters(in: .whitespaces) }
            for (index, line) in lines.enumerated() where line.hasPrefix("Text(") {
                var depth = parens(line)
                var cursor = index + 1
                while depth > 0, cursor < lines.count {
                    depth += parens(lines[cursor])
                    cursor += 1
                }
                while cursor < lines.count, depth > 0 || lines[cursor].hasPrefix(".") {
                    let modifier = lines[cursor]
                    let range = NSRange(modifier.startIndex..., in: modifier)
                    if modifier.hasPrefix(".foregroundColor("), primary.firstMatch(in: modifier, range: range) != nil {
                        found.append((file.lastPathComponent, line))
                    }
                    depth += parens(modifier)
                    cursor += 1
                }
            }
        }
        return found
    }

    private func rgb(_ color: Color, _ style: UIUserInterfaceStyle) -> SIMD3<Double> {
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        UIColor(color).resolvedColor(with: UITraitCollection(userInterfaceStyle: style))
            .getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        return SIMD3(Double(red), Double(green), Double(blue))
    }

    private func contrast(_ first: SIMD3<Double>, _ second: SIMD3<Double>) -> Double {
        func luminance(_ color: SIMD3<Double>) -> Double {
            let linear = [color.x, color.y, color.z].map { $0 <= 0.04045 ? $0 / 12.92 : pow(($0 + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
        }
        let (lighter, darker) = (max(luminance(first), luminance(second)), min(luminance(first), luminance(second)))
        return (lighter + 0.05) / (darker + 0.05)
    }

    private func parens(_ line: String) -> Int {
        line.filter { $0 == "(" }.count - line.filter { $0 == ")" }.count
    }

    private func compactSource(_ path: String) throws -> String {
        try String(contentsOf: featureSources.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }
}
