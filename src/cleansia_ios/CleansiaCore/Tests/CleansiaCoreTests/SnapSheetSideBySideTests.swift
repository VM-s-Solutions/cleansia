import MapKit
import SwiftUI
import XCTest
@testable import CleansiaCore

/// The order detail's sheet lies over its backdrop in every container but a wide, short one, where it stands
/// beside it: the backdrop on the leading side, the content in a column on the trailing side. On every iPhone
/// today, on the iPhone Duo's outer display and on its inner display held upright, it is laid out as before.
@MainActor
final class SnapSheetSideBySideTests: XCTestCase {
    private let phoneSafeArea = EdgeInsets(top: 59, leading: 0, bottom: 34, trailing: 0)
    private let prague = Coordinate(latitude: 50.0755, longitude: 14.4378)

    func testOnAPhoneTheSheetLiesOverTheWholeBackdropAsBefore() async throws {
        for size in [CGSize(width: 393, height: 852), CGSize(width: 440, height: 956)] {
            try await assertOver(in: size, safeArea: phoneSafeArea)
        }
    }

    func testOnTheDuosOuterDisplayAndItsUprightInnerOneTheSheetStillLiesOverTheBackdrop() async throws {
        for size in [CGSize(width: 466, height: 678), CGSize(width: 669, height: 951)] {
            try await assertOver(in: size, safeArea: EdgeInsets(top: 24, leading: 0, bottom: 20, trailing: 0))
        }
    }

    func testOnAWideShortCanvasTheSheetStandsBesideTheBackdrop() async throws {
        let size = CGSize(width: 951, height: 669)
        let safeArea = EdgeInsets(top: 24, leading: 0, bottom: 20, trailing: 0)
        let probes = try await layOut(in: size, safeArea: safeArea)
        let height = size.height - safeArea.top - safeArea.bottom
        let backdrop = try XCTUnwrap(probes.frames["backdrop"])
        let content = try XCTUnwrap(probes.frames["content"])
        let columnWidth: CGFloat = 440

        XCTAssertEqual(content.width, columnWidth, accuracy: 0.5, "the column")
        XCTAssertEqual(content.maxX, size.width, accuracy: 0.5, "the column stands on the trailing side")
        XCTAssertEqual(content.minY, safeArea.top, accuracy: 0.5, "the column starts at the safe area's top")
        XCTAssertEqual(content.maxY, safeArea.top + height, accuracy: 0.5, "the column's foot is the safe area's")
        XCTAssertEqual(backdrop.minX, 0, accuracy: 0.5)
        XCTAssertEqual(backdrop.width, size.width - columnWidth, accuracy: 0.5, "the backdrop has the rest")
        XCTAssertEqual(backdrop.minY, 0, accuracy: 0.5, "the backdrop still reaches the screen's edges")
        XCTAssertEqual(backdrop.height, size.height, accuracy: 0.5)
        XCTAssertEqual(
            try XCTUnwrap(probes.values["sheetTop"]),
            safeArea.top + height,
            accuracy: 0.5,
            "a sheet beside the backdrop covers none of it"
        )
        XCTAssertNil(probes.frames["ornament"], "the ornament rides an edge the column does not have")
    }

    /// Beside the sheet the map is uncovered, so the order's pin is at its centre rather than lifted into the
    /// strip above a sheet; over it, the pin stays lifted as before.
    func testTheMapCentresTheOrderBesideTheSheetAndLiftsItOverTheSheet() async throws {
        let cases = [
            MapCase(
                size: CGSize(width: 951, height: 669),
                safeArea: EdgeInsets(top: 24, leading: 0, bottom: 20, trailing: 0),
                width: 511,
                lifted: false
            ),
            MapCase(size: CGSize(width: 393, height: 852), safeArea: phoneSafeArea, width: 393, lifted: true)
        ]
        let pin = CLLocationCoordinate2D(latitude: prague.latitude, longitude: prague.longitude)
        for mapCase in cases {
            let size = mapCase.size
            let canvas = HostedCanvas(size: size, safeArea: mapCase.safeArea) {
                SnapSheet(anchor: .constant(.peek)) {
                    MapKitMapProvider().fullBleedMap(coordinate: prague)
                } content: {
                    Color.clear
                }
            }
            defer { canvas.close() }
            await canvas.settle { canvas.views(of: MKMapView.self).first?.bounds.width ?? 0 > 0 }
            let map = try XCTUnwrap(canvas.views(of: MKMapView.self).first, "no map at \(size)")
            let point = map.convert(pin, toPointTo: map)

            XCTAssertEqual(map.convert(map.bounds, to: canvas.window).width, mapCase.width, accuracy: 0.5, "\(size)")
            XCTAssertEqual(point.x, map.bounds.midX, accuracy: 1, "\(size)")
            if mapCase.lifted {
                XCTAssertLessThan(point.y, map.bounds.midY - 100, "the pin is not lifted above the sheet at \(size)")
            } else {
                XCTAssertEqual(point.y, map.bounds.midY, accuracy: 1, "the pin is not centred at \(size)")
            }
        }
    }

    func testTheSheetStandsBesideTheBackdropOnlyInAWideShortContainer() {
        let beside = [CGSize(width: 951, height: 669), CGSize(width: 951, height: 625), CGSize(width: 700, height: 699)]
        let over = [
            CGSize(width: 393, height: 759),
            CGSize(width: 440, height: 863),
            CGSize(width: 466, height: 678),
            CGSize(width: 669, height: 951),
            CGSize(width: 699, height: 400),
            CGSize(width: 800, height: 800)
        ]
        for size in beside {
            XCTAssertTrue(SnapSheetColumn.standsBeside(in: size), "\(size)")
        }
        for size in over {
            XCTAssertFalse(SnapSheetColumn.standsBeside(in: size), "\(size)")
        }
    }

    func testTheRegionIsLiftedOnlyForTheSheetThatCoversTheMap() {
        let over = FullBleedMapGeometry.region(for: prague)
        let beside = FullBleedMapGeometry.region(for: prague, coveredFraction: 0)

        XCTAssertLessThan(over.center.latitude, prague.latitude)
        XCTAssertEqual(beside.center.latitude, prague.latitude, accuracy: 0.000_001)
        XCTAssertEqual(beside.center.longitude, prague.longitude, accuracy: 0.000_001)
        XCTAssertEqual(beside.span.latitudeDelta, over.span.latitudeDelta, accuracy: 0.000_001)
    }

    /// A fold moves the sheet beside the map or back over it mid-session; the region follows, and an anchor
    /// change within one layout still leaves a pan alone.
    func testTheMapIsReCentredWhenTheSheetMovesBesideItOrBackOverIt() {
        let map = FullBleedOrderMap(coordinate: prague, height: 669)
        let mapView = map.makeMapView()
        mapView.frame = CGRect(x: 0, y: 0, width: 393, height: 669)
        let placement = FullBleedOrderMap.Placement()
        map.apply(to: mapView, placement: placement)
        let lifted = mapView.region.center.latitude

        map.apply(to: mapView, placement: placement, besideSheet: true)
        XCTAssertGreaterThan(mapView.region.center.latitude, lifted, "beside the sheet the pin is centred")
        XCTAssertEqual(mapView.region.center.latitude, prague.latitude, accuracy: 0.000_1)

        map.apply(to: mapView, placement: placement, besideSheet: false)
        XCTAssertEqual(mapView.region.center.latitude, lifted, accuracy: 0.000_1, "back over the map it is lifted")
    }

    private func assertOver(
        in size: CGSize,
        safeArea: EdgeInsets,
        file: StaticString = #filePath,
        line: UInt = #line
    ) async throws {
        let probes = try await layOut(in: size, safeArea: safeArea)
        let height = size.height - safeArea.top - safeArea.bottom
        let sheetTop = SnapResolver.sheetTop(for: .peek, containerHeight: height)
        let backdrop = try XCTUnwrap(probes.frames["backdrop"], file: file, line: line)
        let content = try XCTUnwrap(probes.frames["content"], file: file, line: line)
        let ornament = try XCTUnwrap(probes.frames["ornament"], file: file, line: line)
        let size128 = SnapSheetOrnament.defaultSize

        XCTAssertEqual(backdrop.minX, 0, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(backdrop.minY, 0, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(backdrop.width, size.width, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(backdrop.height, size.height, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(content.minX, 0, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(content.width, size.width, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertGreaterThan(content.minY, safeArea.top + sheetTop, "the handle is above it", file: file, line: line)
        XCTAssertEqual(content.maxY, safeArea.top + height, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(
            try XCTUnwrap(probes.values["sheetTop"], file: file, line: line),
            safeArea.top + sheetTop,
            accuracy: 0.5,
            "\(size)",
            file: file,
            line: line
        )
        XCTAssertEqual(ornament.width, size128, accuracy: 0.5, file: file, line: line)
        XCTAssertEqual(ornament.maxX, size.width - Spacing.m, accuracy: 0.5, "\(size)", file: file, line: line)
        XCTAssertEqual(ornament.midY, safeArea.top + sheetTop, accuracy: 0.5, "\(size)", file: file, line: line)
    }

    private func layOut(in size: CGSize, safeArea: EdgeInsets) async throws -> LayoutProbes {
        let probes = LayoutProbes()
        let canvas = HostedCanvas(size: size, safeArea: safeArea) {
            SnapSheet(anchor: .constant(.peek)) {
                BackdropProbe(probes: probes)
            } ornament: {
                Color.clear.probed("ornament", in: probes)
            } content: {
                Color.clear
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .probed("content", in: probes)
            }
        }
        defer { canvas.close() }
        await canvas.settle { probes.frames["content"] != nil && probes.frames["backdrop"] != nil }
        return probes
    }
}

private struct MapCase {
    let size: CGSize
    let safeArea: EdgeInsets
    let width: CGFloat
    let lifted: Bool
}

private struct BackdropProbe: View {
    @Environment(\.snapSheetTop) private var sheetTop
    let probes: LayoutProbes

    var body: some View {
        Color.clear
            .probed("backdrop", in: probes)
            .onAppear { probes.record("sheetTop", value: sheetTop) }
            .onChange(of: sheetTop) { probes.record("sheetTop", value: $0) }
    }
}
