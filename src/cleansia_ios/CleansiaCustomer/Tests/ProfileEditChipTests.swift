import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCustomer

/// The profile hero's edit chip: its label is the verb alone, it stays on one line however narrow the
/// hero gets, and VoiceOver still hears the whole word. Reads the COMPILED bundle rather than the
/// `.xcstrings` source, so a value that survives the catalog but not the build fails here.
@MainActor
final class ProfileEditChipTests: XCTestCase {
    private static let verbOnly = [
        "en": "Edit",
        "cs": "Upravit",
        "sk": "Upraviť",
        "uk": "Редагувати",
        "ru": "Редактировать"
    ]

    private static let fullWording = [
        "en": "Edit profile",
        "cs": "Upravit profil",
        "sk": "Upraviť profil",
        "uk": "Редагувати профіль",
        "ru": "Редактировать профиль"
    ]

    private var restoreBundle: Bundle?

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
    }

    override func tearDown() {
        L10n.bundle = restoreBundle ?? .main
        super.tearDown()
    }

    func testTheChipLabelIsTheVerbAloneInEveryLocale() throws {
        for (tag, verb) in Self.verbOnly {
            L10n.bundle = try localeBundle(tag)
            XCTAssertEqual(L10n.Profile.rowEditProfile, verb, "profile_row_edit in \(tag)")
        }
    }

    /// The account list row is a separate, unconstrained surface that Android does not have at all.
    /// Shortening it to a bare verb was never asked for, so it keeps the full wording.
    func testTheAccountRowKeepsTheFullWordingAndNotTheChipLabel() throws {
        for (tag, wording) in Self.fullWording {
            L10n.bundle = try localeBundle(tag)
            XCTAssertEqual(ProfileTab.editRowLabel, wording, "the account row's label in \(tag)")
            XCTAssertNotEqual(
                ProfileTab.editRowLabel,
                L10n.Profile.rowEditProfile,
                "the account row must not collapse onto the chip's verb-only label (\(tag))"
            )
        }
    }

    func testTheChipStaysOneLineWhenSqueezedBelowItsIdealWidth() throws {
        for tag in ["ru", "uk"] {
            L10n.bundle = try localeBundle(tag)
            let ideal = chipSize(proposedWidth: .greatestFiniteMagnitude)
            let squeezed = chipSize(proposedWidth: ideal.width * Self.squeezeFactor)

            XCTAssertEqual(
                squeezed.height,
                ideal.height,
                accuracy: 0.5,
                "the chip grew from \(ideal.height)pt to \(squeezed.height)pt in \(tag) — it wrapped instead "
                    + "of truncating"
            )
        }
    }

    /// Reads the real UIAccessibility tree VoiceOver consumes rather than inferring it from SwiftUI's
    /// documented behaviour. SwiftUI only builds that tree when an assistive technology is running, so
    /// on a plain simulator this skips instead of silently asserting nothing.
    func testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated() throws {
        try XCTSkipUnless(
            UIAccessibility.isVoiceOverRunning,
            "needs the simulator's accessibility server: xcrun simctl spawn <device> defaults write "
                + "com.apple.Accessibility ApplicationAccessibilityEnabled -int 1"
        )
        for (tag, verb) in Self.verbOnly {
            L10n.bundle = try localeBundle(tag)
            let ideal = chipSize(proposedWidth: .greatestFiniteMagnitude)
            let announced = accessibilityLabels(renderedAt: ideal.width * Self.squeezeFactor, height: ideal.height)

            XCTAssertTrue(
                announced.contains { $0.contains(verb) },
                "VoiceOver heard \(announced) in \(tag), which does not carry the whole word \"\(verb)\""
            )
        }
    }

    // MARK: - Rendering

    /// Narrow enough that the longest label cannot fit, derived from the label itself so no width here
    /// goes stale when the type or the padding moves.
    private static let squeezeFactor: CGFloat = 0.7

    private func chipSize(proposedWidth: CGFloat) -> CGSize {
        let host = UIHostingController(rootView: EditProfileChip(onEdit: {}))
        return host.sizeThatFits(in: CGSize(width: proposedWidth, height: .greatestFiniteMagnitude))
    }

    private func accessibilityLabels(renderedAt width: CGFloat, height: CGFloat) -> [String] {
        let host = UIHostingController(rootView: EditProfileChip(onEdit: {}))
        let frame = CGRect(x: 0, y: 0, width: width, height: height)
        let window = UIWindow(frame: frame)
        window.rootViewController = host
        window.makeKeyAndVisible()
        host.view.frame = frame
        host.view.layoutIfNeeded()
        var seen: Set<ObjectIdentifier> = []
        return Self.labels(under: host.view, seen: &seen)
    }

    /// SwiftUI hosting views vend their accessibility nodes through the `UIAccessibilityContainer`
    /// methods rather than the `accessibilityElements` array, so walk both.
    private static func labels(under object: NSObject, seen: inout Set<ObjectIdentifier>) -> [String] {
        guard seen.insert(ObjectIdentifier(object)).inserted else { return [] }
        var found = [object.accessibilityLabel].compactMap { $0 }

        let count = object.accessibilityElementCount()
        if count != NSNotFound, count > 0 {
            for index in 0 ..< count {
                guard let child = object.accessibilityElement(at: index) as? NSObject else { continue }
                found += labels(under: child, seen: &seen)
            }
        }
        for element in object.accessibilityElements ?? [] {
            guard let element = element as? NSObject else { continue }
            found += labels(under: element, seen: &seen)
        }
        if let view = object as? UIView {
            found += view.subviews.flatMap { labels(under: $0, seen: &seen) }
        }
        return found
    }

    // MARK: - Bundles

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
