import CleansiaCore
import CleansiaCustomerApi
import SwiftUI
import XCTest
@testable import CleansiaCustomer

/// The order's photo grid takes as many columns as its width holds. Every iPhone today keeps the three columns it
/// had, with each thumbnail exactly where it was, the 375pt phones included, and the iPhone Duo's outer display
/// too; the inner display holds more, each wide enough for its thumbnail.
@MainActor
final class OrderPhotosGridTests: XCTestCase {
    func testPhotoCollectionsKeepEntranceAndMissingPhaseOutOfBefore() {
        let gallery = OrderPhotos(photos: [
            GetOrderPhotosOrderPhotoDto(id: "before", photoType: ._1),
            GetOrderPhotosOrderPhotoDto(id: "after", photoType: ._2),
            GetOrderPhotosOrderPhotoDto(id: "entrance", photoType: ._3),
            GetOrderPhotosOrderPhotoDto(id: "unknown")
        ], beforeCount: 1, afterCount: 1)
        XCTAssertEqual(gallery.photos(for: .before).map(\.id), ["before"])
        XCTAssertEqual(gallery.photos(for: .after).map(\.id), ["after"])
        XCTAssertEqual(gallery.photos(for: .entrance).map(\.id), ["entrance"])
        XCTAssertEqual(gallery.photos(for: .other).map(\.id), ["unknown"])
    }

    func testPreviewResolvesTheSelectedPhotoByIdentityAfterReordering() {
        let selection = OrderPhotoSelection(phase: .after, photoID: "selected")
        let gallery = OrderPhotos(photos: [
            GetOrderPhotosOrderPhotoDto(id: "new-photo", photoType: ._2),
            GetOrderPhotosOrderPhotoDto(id: "selected", photoType: ._2),
            GetOrderPhotosOrderPhotoDto(id: "before", photoType: ._1)
        ], beforeCount: 1, afterCount: 2)
        XCTAssertEqual(gallery.previewIndex(for: selection), 1)
        XCTAssertNil(gallery.previewIndex(for: OrderPhotoSelection(phase: .before, photoID: "selected")))
        XCTAssertNil(gallery.previewIndex(for: OrderPhotoSelection(phase: .after, photoID: "removed")))
        XCTAssertNil(gallery.previewIndex(for: OrderPhotoSelection(phase: .after, photoID: nil)))
    }

    func testTheDetailThumbnailAndFreshGalleryUseTheStablePhotoSelection() throws {
        let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        func source(_ path: String) throws -> String {
            try String(contentsOf: root.appendingPathComponent("Sources/Features/Orders/" + path), encoding: .utf8)
                .components(separatedBy: .whitespacesAndNewlines).joined()
        }
        XCTAssertTrue(try source("OrderDetailPhotos.swift").contains("onPreviewPhoto(phase,photo.id)"))
        XCTAssertTrue(try source("OrderDetailView.swift")
            .contains("photoSelection=OrderPhotoSelection(phase:phase,photoID:id)"))
        XCTAssertTrue(try source("OrderDetailView.swift").contains("initialSelection:photoSelection"))
        XCTAssertTrue(try source("Photos/OrderPhotosScreen.swift")
            .contains("gallery.previewIndex(for:initialSelection)"))
    }

    private let thumbSize: CGFloat = 112
    private let threeColumns = [GridItem(.flexible()), GridItem(.flexible()), GridItem(.flexible())]

    func testEveryPhoneKeepsItsThreeColumnsAndEveryThumbnailWhereItWas() async throws {
        for width: CGFloat in [375, 390, 393, 402, 440, 466] {
            let before = try await thumbnails(columns: threeColumns, width: width)
            let after = try await thumbnails(columns: OrderPhotosScreen.gridColumns, width: width)

            XCTAssertEqual(columns(after), 3, "\(width)")
            XCTAssertEqual(after.count, before.count, "\(width)")
            for (moved, kept) in zip(after, before) {
                XCTAssertEqual(moved.minX, kept.minX, accuracy: 0.5, "\(width)")
                XCTAssertEqual(moved.minY, kept.minY, accuracy: 0.5, "\(width)")
            }
        }
    }

    func testAWiderScreenHoldsMoreColumnsEachWideEnoughForItsThumbnail() async throws {
        for (width, expected): (CGFloat, Int) in [(669, 5), (951, 7)] {
            let frames = try await thumbnails(columns: OrderPhotosScreen.gridColumns, width: width)
            let lefts = Set(frames.map { $0.minX.rounded() }).sorted()

            XCTAssertEqual(lefts.count, expected, "\(width)")
            for (left, next) in zip(lefts, lefts.dropFirst()) {
                XCTAssertGreaterThanOrEqual(next - left, thumbSize + Spacing.xs, "thumbnails crowd at \(width)")
            }
        }
    }

    func testTheScreenLaysItsThumbnailsOutInTheseColumns() throws {
        let source = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/Features/Orders/Photos/OrderPhotosScreen.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()

        XCTAssertTrue(source.contains("LazyVGrid(columns:Self.gridColumns,spacing:Spacing.xs){"))
        XCTAssertTrue(source.contains("PhotoThumb(urlString:photo.blobUrl,size:112)"))
        XCTAssertTrue(source.contains("gallery.photos(for:phase)"))
    }

    /// The screen's grid as it lays out, thumbnails in reading order: a scroll view, the grid at the screen's
    /// spacing and margin, a 112pt thumbnail in each cell.
    private func thumbnails(columns: [GridItem], width: CGFloat) async throws -> [CGRect] {
        let probes = LayoutProbes()
        let count = 12
        let side = thumbSize
        let canvas = try HostedCanvas(size: CGSize(width: width, height: 900)) {
            ScrollView {
                LazyVGrid(columns: columns, spacing: Spacing.xs) {
                    ForEach(0 ..< count, id: \.self) { index in
                        Color.gray
                            .frame(width: side, height: side)
                            .probed("\(index)", in: probes)
                    }
                }
                .padding(Spacing.m)
            }
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames.count == count }
        return try (0 ..< count).map { index in
            try XCTUnwrap(probes.frames["\(index)"], "thumbnail \(index) never laid out at \(width)")
        }
    }

    private func columns(_ frames: [CGRect]) -> Int {
        Set(frames.map { $0.minX.rounded() }).count
    }
}
