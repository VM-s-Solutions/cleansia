import XCTest
@testable import CleansiaPartner

final class ChipFlowPackingTests: XCTestCase {
    private func rows(_ sizes: [CGSize], spacing: CGFloat = 8, maxWidth: CGFloat) -> [ChipFlowPacking.Row] {
        ChipFlowPacking.rows(sizes: sizes, spacing: spacing, maxWidth: maxWidth)
    }

    func testAllChipsFitOnOneRow() {
        let result = rows(
            [CGSize(width: 40, height: 20), CGSize(width: 50, height: 24), CGSize(width: 60, height: 20)],
            maxWidth: 200
        )

        XCTAssertEqual(result.map(\.indices), [[0, 1, 2]])
        XCTAssertEqual(result[0].width, 40 + 8 + 50 + 8 + 60)
        XCTAssertEqual(result[0].height, 24)
    }

    func testWrapsWhenWidthRunsOut() {
        let result = rows(
            [CGSize(width: 50, height: 20), CGSize(width: 50, height: 20), CGSize(width: 50, height: 20)],
            maxWidth: 120
        )

        XCTAssertEqual(result.map(\.indices), [[0, 1], [2]])
        XCTAssertEqual(result[0].width, 50 + 8 + 50)
        XCTAssertEqual(result[1].width, 50)
    }

    func testSingleOversizedChipKeepsItsOwnRow() {
        let result = rows(
            [CGSize(width: 40, height: 20), CGSize(width: 200, height: 20), CGSize(width: 40, height: 20)],
            maxWidth: 100
        )

        XCTAssertEqual(result.map(\.indices), [[0], [1], [2]])
        XCTAssertEqual(result[1].width, 200)
    }

    func testOversizedFirstChipIsStillPlaced() {
        let result = rows([CGSize(width: 300, height: 20)], maxWidth: 100)

        XCTAssertEqual(result.map(\.indices), [[0]])
        XCTAssertEqual(result[0].width, 300)
    }

    func testEmptyInputYieldsNoRows() {
        XCTAssertTrue(rows([], maxWidth: 100).isEmpty)
    }

    func testExactFitAtTheWrapBoundaryStaysOnOneRow() {
        let sizes = [CGSize(width: 50, height: 20), CGSize(width: 50, height: 20)]

        XCTAssertEqual(rows(sizes, maxWidth: 108).map(\.indices), [[0, 1]])
        XCTAssertEqual(rows(sizes, maxWidth: 107).map(\.indices), [[0], [1]])
    }

    func testRowHeightIsTheTallestChipInTheRow() {
        let result = rows(
            [CGSize(width: 40, height: 20), CGSize(width: 40, height: 32), CGSize(width: 40, height: 26)],
            maxWidth: 200
        )

        XCTAssertEqual(result.map(\.height), [32])
    }
}
