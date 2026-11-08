import SwiftUI
import UIKit
import XCTest
@testable import CleansiaCore

/// The birth-date sheet is wheels with a Done that only closes: a wheel that moves stores its day at once, and a sheet
/// closed untouched leaves an empty field empty, so the day the wheels open on is never stored unchosen. Its buttons
/// are read from source, the `ComponentTextInkTests` idiom; what the wheels store is read from a hosted sheet.
@MainActor
final class BirthDateFieldSheetTests: XCTestCase {
    override static func setUp() {
        super.setUp()
        ApplicationAccessibility.enable()
    }

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

    func testTheWheelsWriteTheFieldAndDoneOnlyCloses() throws {
        let source = try compactSource()

        XCTAssertTrue(source.contains("publicstructBirthDateWheelSheet"))
        XCTAssertTrue(source.contains("selection:CalendarDay.pickerBinding($birthDate)"))
        XCTAssertTrue(source.contains("Button{dismiss()}label:{Text(CoreL10n.localized(\"birth_date_field.done\"))"))
        XCTAssertTrue(source.contains(".presentationDetents([.medium])"))
    }

    func testTheSheetHasNoCancelAndNothingWaitsForDone() throws {
        let source = try compactSource()

        for gone in ["draft", "confirmed(", "cancellationAction", "birth_date_field.cancel"] {
            XCTAssertFalse(source.contains(gone), gone)
        }
    }

    func testAnUntouchedSheetStoresNothingAndAMovedWheelStoresItsDay() async throws {
        let day = DayBox()
        let canvas = HostedCanvas(size: CGSize(width: 390, height: 480)) {
            WheelSheetHost(day: day)
        }
        defer { canvas.close() }
        await canvas.settle(until: { !canvas.views(of: UIDatePicker.self).isEmpty })

        XCTAssertNil(day.value, "the wheels opened on a day and stored it untouched")

        let picker = try XCTUnwrap(canvas.views(of: UIDatePicker.self).first)
        let afternoon = DateComponents(year: 1991, month: 3, day: 14, hour: 15)
        picker.date = try XCTUnwrap(CalendarDay.calendar.date(from: afternoon))
        // This test host does not deliver sendActions(for:), so the picker's own value-changed targets are called.
        for target in picker.allTargets {
            for action in picker.actions(forTarget: target, forControlEvent: .valueChanged) ?? [] {
                (target as NSObject).perform(NSSelectorFromString(action), with: picker)
            }
        }
        await canvas.settle(until: { day.value != nil })

        XCTAssertEqual(day.value, CalendarDay.calendar.date(from: DateComponents(year: 1991, month: 3, day: 14)))

        let done = try XCTUnwrap(ApplicationAccessibility.elements(under: canvas.window).first {
            $0.accessibilityLabel == CoreL10n.localized("birth_date_field.done")
        })
        XCTAssertGreaterThanOrEqual(done.accessibilityFrame.width, 44)
        XCTAssertGreaterThanOrEqual(done.accessibilityFrame.height, 44)
    }
}

private final class DayBox: ObservableObject {
    @Published var value: Date?
}

private struct WheelSheetHost: View {
    @ObservedObject var day: DayBox

    var body: some View {
        BirthDateWheelSheet(birthDate: $day.value, title: "Birth date")
    }
}
