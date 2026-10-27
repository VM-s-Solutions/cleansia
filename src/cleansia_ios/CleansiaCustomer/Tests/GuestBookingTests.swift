import CleansiaCore
import CleansiaCustomerApi
import Foundation
import XCTest
@testable import CleansiaCustomer

/// A booking made without an account under the signed-in customer's e-mail: My orders lists it, labelled,
/// and it opens read-only with Cancel. Everything else on it stays refused on the server, so the app never
/// offers it (owner ruling 2026-10-26, option B).
final class GuestBookingTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]
    private let keys = ["orders_guest_booking", "order_detail_guest_booking_note"]

    // MARK: - The wire

    func testARowReadsTheGuestFlagAndAnOlderServerThatSendsNoneMeansAnAccountBooking() throws {
        var guest = OrderListItem.wireComplete()
        guest.isGuestBooking = true

        XCTAssertEqual(try CustomerOrderSummary(guest)?.isGuestBooking, true)
        XCTAssertEqual(try CustomerOrderSummary(.wireComplete())?.isGuestBooking, false)
    }

    func testTheDetailReadsTheGuestFlagAndAnOlderServerThatSendsNoneMeansAnAccountBooking() throws {
        var guest = OrderItem.wireComplete()
        guest.isGuestBooking = true

        XCTAssertTrue(try CustomerOrderDetail(guest).isGuestBooking)
        XCTAssertFalse(try CustomerOrderDetail(.wireComplete()).isGuestBooking)
    }

    // MARK: - The completion prompt

    func testAGuestBookingIsNeverTheReviewPromptsCandidate() {
        let completed = Code(type: "OrderStatus", name: nil, value: 5)
        let account = OrderFixtures.summary(
            id: "account",
            statusCode: completed,
            cleaningDateTime: Date(timeIntervalSince1970: 1_750_000_000)
        )
        var guest = OrderFixtures.summary(
            id: "guest",
            statusCode: completed,
            cleaningDateTime: Date(timeIntervalSince1970: 1_756_000_000)
        )
        guest.isGuestBooking = true

        XCTAssertEqual(ReviewPrompt.candidate(orders: [account, guest], alreadyPrompted: [])?.id, "account")
        XCTAssertNil(ReviewPrompt.candidate(orders: [guest], alreadyPrompted: []))
    }

    // MARK: - The detail's cards

    func testAGuestBookingShowsNoReviewCardAndNoReceipt() {
        var guest = OrderFixtures.detail(statusValue: 5)
        guest.isGuestBooking = true
        var withReceipt = OrderFixtures.detail(statusCode: Code(value: 6), receiptNumber: "R-1")
        withReceipt.isGuestBooking = true

        XCTAssertFalse(guest.offersReview)
        XCTAssertFalse(guest.offersReceipt)
        XCTAssertFalse(withReceipt.offersReceipt)
    }

    func testAnAccountBookingKeepsItsReviewCardAndReceipt() {
        let completed = OrderFixtures.detail(statusValue: 5)
        let cancelledWithReceipt = OrderFixtures.detail(statusCode: Code(value: 6), receiptNumber: "R-1")
        let confirmed = OrderFixtures.detail(statusValue: 2)

        XCTAssertTrue(completed.offersReview)
        XCTAssertTrue(completed.offersReceipt)
        XCTAssertTrue(cancelledWithReceipt.offersReceipt)
        XCTAssertFalse(confirmed.offersReview)
        XCTAssertFalse(confirmed.offersReceipt)
    }

    // MARK: - The footer

    func testAGuestBookingOffersNoIssueReportInAnyStatus() {
        for status in [OrderStatus._2, ._3, ._4, ._5] {
            XCTAssertFalse(OrderDetailFooterActions.showReportIssue(status, isGuestBooking: true), "\(status)")
            XCTAssertTrue(OrderDetailFooterActions.showReportIssue(status, isGuestBooking: false), "\(status)")
        }
    }

    private func showsFooter(
        _ status: OrderStatus,
        guest: Bool,
        canCancel: Bool = false,
        authoring: RecurringAuthoringGate = .allowed
    ) -> Bool {
        OrderDetailFooterActions.showFooter(
            status,
            authoring: authoring,
            isGuestBooking: guest,
            canCancel: canCancel,
            canReportCleanerNoShow: false
        )
    }

    /// In progress, an account booking's footer is its issue report alone; a guest booking has none, so it
    /// draws no empty footer.
    func testAnInProgressGuestBookingDrawsNoFooter() {
        XCTAssertFalse(showsFooter(._4, guest: true))
        XCTAssertTrue(showsFooter(._4, guest: false))
    }

    func testAGuestBookingKeepsCancelBeforeItsStartAndBookAgainOnceDone() {
        for status in [OrderStatus._0, ._1, ._2, ._3] {
            XCTAssertTrue(showsFooter(status, guest: true, canCancel: true, authoring: .upsell), "\(status)")
        }
        XCTAssertTrue(showsFooter(._5, guest: true, authoring: .upsell))
        XCTAssertFalse(showsFooter(._6, guest: true))
    }

    /// Cancel withheld past a staffed start and no no-show report on a guest booking: the status alone still
    /// counts as cancellable, but nothing would be drawn.
    func testAGuestBookingThatCanNeitherCancelNorReportANoShowDrawsNoFooterBeforeItIsDone() {
        for status in [OrderStatus._0, ._1, ._2, ._3] {
            XCTAssertFalse(showsFooter(status, guest: true), "\(status)")
        }
    }

    func testTheViewBindsTheFooterAndTheCardsToTheGuestFlag() throws {
        let view = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailView.swift")
            .replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)
        XCTAssertTrue(view.contains(
            "showReportIssue: OrderDetailFooterActions.showReportIssue( order.status, "
                + "isGuestBooking: order.isGuestBooking ),"
        ))
        let content = try readSource("CleansiaCustomer/Sources/Features/Orders/OrderDetailContent.swift")
        XCTAssertTrue(content.contains("if order.offersReview {"))
        XCTAssertTrue(content.contains("if order.offersReceipt {"))
        XCTAssertTrue(content.contains("if order.isGuestBooking {"))
        let list = try readSource("CleansiaCustomer/Sources/Features/Orders/OrdersTab.swift")
        XCTAssertTrue(list.contains("if order.isGuestBooking {"))
    }

    // MARK: - Copy

    func testTheLabelAndTheNoteAreWrittenInAllFiveLanguages() throws {
        let english = try localizableTable(for: "en")
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = try XCTUnwrap(table[key], "\(key) missing from \(language).lproj")
                XCTAssertFalse(value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, "\(language) \(key)")
                if language != "en" {
                    XCTAssertNotEqual(value, english[key], "\(language).lproj left \(key) in English")
                }
            }
        }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(appBundle.url(forResource: language, withExtension: "lproj"), language)
        let strings = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(NSDictionary(contentsOf: strings) as? [String: String], language)
    }

    private func readSource(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
