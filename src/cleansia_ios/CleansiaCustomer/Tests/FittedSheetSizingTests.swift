import CleansiaCore
import SwiftUI
import XCTest
@testable import CleansiaCustomer

/// The three sheets sized by their measured content keep that size where the system centres a sheet and sets
/// detents aside, as it does in a regular-width container such as the iPhone Duo's inner display. On a phone the
/// measured detent sizes the sheet, exactly as before.
@MainActor
final class FittedSheetSizingTests: XCTestCase {
    func testTheMeasuredSheetsAreFittedToTheirContent() throws {
        let sheets = [
            "CleansiaCustomer/Sources/Features/Booking/Confirm/CodeSheetShell.swift":
                ".presentationDetents([.height(contentHeight)]).fittedPresentationSizing()",
            "CleansiaCustomer/Sources/Features/Booking/Steps/PackageDetailsSheet.swift":
                ".presentationDetents([.height(contentHeight)]).fittedPresentationSizing()",
            "CleansiaCustomer/Sources/Features/Rewards/CreditViews.swift":
                ".presentationDetents([.height(contentHeight+buttonHeight),.large]).fittedPresentationSizing()"
        ]
        for (path, chain) in sheets {
            XCTAssertTrue(try compact(path).contains(chain), path)
        }
    }

    func testOnAPhoneAFittedSheetIsTheSameSheetAsBefore() async throws {
        for size in [CGSize(width: 393, height: 852), CGSize(width: 440, height: 956)] {
            let before = try await presentedFrame(fitted: false, in: size)
            let after = try await presentedFrame(fitted: true, in: size)

            XCTAssertEqual(after.minX, before.minX, accuracy: 0.5, "\(size)")
            XCTAssertEqual(after.minY, before.minY, accuracy: 0.5, "\(size)")
            XCTAssertEqual(after.width, before.width, accuracy: 0.5, "\(size)")
            XCTAssertEqual(after.height, before.height, accuracy: 0.5, "\(size)")
            XCTAssertGreaterThan(after.width, size.width - 2 * Spacing.m, "not a phone's sheet at \(size)")
        }
    }

    /// Where the sheet is centred it keeps the form's width and the height of what it holds, never the container's:
    /// fitted in both axes, a sheet whose content fills its width took the whole window.
    func testInARegularWidthContainerAFittedSheetKeepsTheFormWidthAndItsContentsHeight() async throws {
        guard #available(iOS 18, *) else { throw XCTSkip("presentation sizing is iOS 18 and later") }
        for size in [CGSize(width: 669, height: 951), CGSize(width: 951, height: 669)] {
            let before = try await presentedFrame(fitted: false, in: size, regularWidth: true)
            let after = try await presentedFrame(fitted: true, in: size, regularWidth: true)

            XCTAssertEqual(after.midX, size.width / 2, accuracy: 0.5, "not centred at \(size)")
            XCTAssertEqual(after.width, before.width, accuracy: 0.5, "not the form's width at \(size)")
            XCTAssertLessThan(after.width, size.width, "\(size)")
            XCTAssertLessThanOrEqual(after.height, before.height + 0.5, "taller than its content at \(size)")
            XCTAssertLessThan(after.height, size.height / 2, "stretched to the window at \(size)")
        }
    }

    private func presentedFrame(fitted: Bool, in size: CGSize, regularWidth: Bool = false) async throws -> CGRect {
        let canvas = try HostedCanvas(size: size) { MeasuredSheetHarness(fitted: fitted) }
        defer { canvas.close() }
        if regularWidth, #available(iOS 17, *) {
            canvas.window.traitOverrides.horizontalSizeClass = .regular
            canvas.window.traitOverrides.verticalSizeClass = .regular
        }
        var last = CGRect.null
        var steady = 0
        await canvas.settle(timeout: 5) {
            guard let sheet = canvas.host.presentedViewController, !sheet.isBeingPresented else { return false }
            let frame = sheet.view.convert(sheet.view.bounds, to: canvas.window)
            steady = frame == last ? steady + 1 : 0
            last = frame
            return steady >= 3
        }
        return try XCTUnwrap(steady >= 3 ? last : nil, "no sheet settled at \(size)")
    }

    private func compact(_ path: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }
}

/// A sheet sized by a measured detent, as the three shells are, presented as soon as its host appears.
private struct MeasuredSheetHarness: View {
    let fitted: Bool
    @State private var shown = false

    var body: some View {
        Color.white
            .task {
                try? await Task.sleep(nanoseconds: 200_000_000)
                shown = true
            }
            .sheet(isPresented: $shown) {
                if fitted {
                    content.fittedPresentationSizing()
                } else {
                    content
                }
            }
    }

    private var content: some View {
        VStack(spacing: Spacing.m) {
            Text(verbatim: "Promo code")
            Text(verbatim: "A sheet whose height is measured from what it holds, as the three shells measure theirs.")
            Color.gray.frame(height: 48)
        }
        .padding(Spacing.l)
        .frame(maxWidth: .infinity)
        .presentationDetents([.height(220)])
    }
}
