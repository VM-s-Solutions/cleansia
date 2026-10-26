import Foundation
import XCTest

/// The birth-date sheet carries the field's name with Cancel and Done, as Android's dialog carries its dismiss
/// and confirm: the wheels spin a draft, Done stores it, and Cancel or a swipe down leaves the field as it was.
/// Read from source, the `ComponentTextInkTests` idiom: a sheet's toolbar is not observable in a unit test.
final class BirthDateFieldSheetTests: XCTestCase {
    private func compactSource() throws -> String {
        try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/BirthDateField.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()
    }

    func testTheWheelsSpinADraftThatOnlyDoneStores() throws {
        let source = try compactSource()

        XCTAssertTrue(source.contains("draft=birthDateshowPicker=true"), "the draft opens on the stored day")
        XCTAssertTrue(source.contains("selection:CalendarDay.pickerBinding($draft)"))
        XCTAssertFalse(source.contains("pickerBinding($birthDate)"), "the wheels write the field directly")
        XCTAssertTrue(source.contains("birthDate=CalendarDay.confirmed(draft)"))
    }

    func testTheSheetCarriesCancelAndDoneInTheTextInk() throws {
        let source = try compactSource()

        XCTAssertTrue(source.contains("ToolbarItem(placement:.cancellationAction)"))
        XCTAssertTrue(source.contains("ToolbarItem(placement:.confirmationAction)"))
        XCTAssertTrue(source.contains("Button(CoreL10n.localized(\"birth_date_field.cancel\"))"))
        XCTAssertTrue(source.contains("Button(CoreL10n.localized(\"birth_date_field.done\"))"))
        XCTAssertTrue(source.contains(".navigationTitle(label)"))
        XCTAssertTrue(source.contains(".tint(CleansiaColors.primaryText)"))
    }
}
