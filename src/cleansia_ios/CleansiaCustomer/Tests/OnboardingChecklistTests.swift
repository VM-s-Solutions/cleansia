import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// What the onboarding opens on, and what it counts as done. One checklist feeds the marks, the count, the prompt,
/// the example's Call and SMS, the mascot and Save.
@MainActor
final class OnboardingChecklistTests: XCTestCase {
    private static let honestNote = [
        "en": "Optional. Never shown to your cleaner.",
        "cs": "Nepovinné. Uklízeči se nikdy nezobrazí.",
        "sk": "Nepovinné. Upratovačovi sa nikdy nezobrazí.",
        "uk": "Необов’язково. Клінер її ніколи не побачить.",
        "ru": "Необязательно. Клинер её никогда не увидит."
    ]

    private static let birthdayPromises = [
        "birthday perks", "narozeninové výhody", "narodeninové výhody", "бонусів до дня народження",
        "бонусов ко дню рождения"
    ]

    /// The length rule itself is Core's (`PhoneNumberKitDisplayFormatterTests`); these are the two examples the
    /// screen is judged on.
    func testAPhoneIsDoneOnlyWhenItsCountryAllowsItsLength() {
        XCTAssertFalse(checklist(phone: "+42077712").phone)
        XCTAssertTrue(checklist(phone: "+420777123456").phone)
    }

    func testTheCountThePromptAndReadinessComeFromOneValue() {
        for named in [false, true] {
            for reachable in [false, true] {
                let list = checklist(firstName: named ? "Jana" : " ", phone: reachable ? "+420777123456" : "+42077712")
                let expected: OnboardingChecklist.Prompt = switch (named, reachable) {
                case (true, true): .ready
                case (true, false): .addPhone
                case (false, true): .addName
                case (false, false): .addBoth
                }

                XCTAssertEqual(list.doneCount, 1 + (named ? 1 : 0) + (reachable ? 1 : 0), "\(named) \(reachable)")
                XCTAssertEqual(list.prompt, expected, "\(named) \(reachable)")
                XCTAssertEqual(list.isReady, named && reachable, "\(named) \(reachable)")
            }
        }
    }

    /// A typed name is never compared with the words the server writes for a hidden one.
    func testATypedNameIsJudgedOnlyOnBeingFilledIn() {
        XCTAssertTrue(checklist(firstName: "Jana", lastName: "Customer").name)
        XCTAssertTrue(checklist(firstName: "Cleansia", lastName: "Customer").name)
        XCTAssertFalse(checklist(firstName: " ", lastName: "Doe").name)
        XCTAssertFalse(checklist(firstName: "Jana", lastName: "").name)
    }

    func testAStandInNameOpensTheNameStepEmptyAndUnnamed() {
        let start = OnboardingStart(
            ProfileFixtures.user(firstName: "Cleansia", lastName: "Customer", isNamePlaceholder: true)
        )

        XCTAssertEqual(start.step, .name)
        XCTAssertEqual(start.firstName, "")
        XCTAssertEqual(start.lastName, "")
    }

    func testAGivenNameOpensThePhoneStepPrefilled() {
        let start = OnboardingStart(ProfileFixtures.user(firstName: "Jana", lastName: "Dvořáková"))

        XCTAssertEqual(start.step, .phone)
        XCTAssertEqual(start.firstName, "Jana")
        XCTAssertEqual(start.lastName, "Dvořáková")
    }

    func testABlankStoredPartStillOpensTheNameStep() {
        let start = OnboardingStart(ProfileFixtures.user(firstName: "Jana", lastName: " "))

        XCTAssertEqual(start.step, .name)
        XCTAssertEqual(start.firstName, "Jana")
    }

    /// The local part gives way and the domain stays whole, so the split is at the last `@`.
    func testAnAddressSplitsAtItsLastAt() {
        let long = EmailValue.parts("jana.dvorakova.vinohrady.praha@gmail.com")
        let twice = EmailValue.parts("a@b@c.cz")
        let none = EmailValue.parts("noat")

        XCTAssertEqual(long.local, "jana.dvorakova.vinohrady.praha")
        XCTAssertEqual(long.domain, "@gmail.com")
        XCTAssertEqual(twice.local, "a@b")
        XCTAssertEqual(twice.domain, "@c.cz")
        XCTAssertEqual(none.local, "noat")
        XCTAssertEqual(none.domain, "")
    }

    /// Reads the compiled bundles, so a value that survives the catalog but not the build fails here, and the
    /// catalog on disk, so no locale keeps a promise of a perk nothing gives.
    func testTheBirthDateNoteIsHonestInEveryLocale() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for (tag, note) in Self.honestNote {
            L10n.bundle = try localeBundle(tag)
            XCTAssertEqual(L10n.Onboarding.birthDateHelper, note, tag)
        }
        let catalog = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Resources/Localizable.xcstrings"),
            encoding: .utf8
        )
        for promise in Self.birthdayPromises {
            XCTAssertFalse(catalog.contains(promise), promise)
        }
    }

    private func checklist(
        firstName: String = "Jana",
        lastName: String = "Dvořáková",
        phone: String = "+420777123456"
    ) -> OnboardingChecklist {
        OnboardingChecklist(firstName: firstName, lastName: lastName, email: "jana.dvorakova@gmail.com", phone: phone)
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
