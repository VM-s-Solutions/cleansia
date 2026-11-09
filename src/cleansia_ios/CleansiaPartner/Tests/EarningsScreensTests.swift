import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The invoices list's Total earned, the counts and copy of the pay screens, and what the invoice and period
/// screens print.
final class EarningsScreensTests: XCTestCase {
    private var restoreBundle: Bundle!
    private var restoreTag: String!

    override func setUp() {
        super.setUp()
        restoreBundle = L10n.bundle
        restoreTag = CoreL10n.languageTag
    }

    override func tearDown() {
        L10n.bundle = restoreBundle
        CoreL10n.apply(languageTag: restoreTag)
        super.tearDown()
    }

    // MARK: - Total earned

    /// Money received is a Paid invoice; a pending, approved, disputed, rejected or cancelled one is not.
    func testTotalEarnedCountsPaidInvoicesOnly() {
        let invoices: [Invoice] = [
            .stub(id: "pending", totalAmount: 100, status: ._1),
            .stub(id: "approved", totalAmount: 200, status: ._2),
            .stub(id: "paid", totalAmount: 300, status: ._3),
            .stub(id: "disputed", totalAmount: 400, status: ._4),
            .stub(id: "rejected", totalAmount: 500, status: ._5),
            .stub(id: "cancelled", totalAmount: 600, status: ._6)
        ]
        XCTAssertEqual(invoices.paid.map(\.id), ["paid"])
        XCTAssertEqual(invoices.paid.totalsByCurrency.map(\.amount), [300])
    }

    /// A koruna and a euro invoice are never one sum: one figure per currency, in the order the list names them.
    func testTotalEarnedIsOneFigurePerCurrency() {
        let invoices: [Invoice] = [
            .stub(id: "1", totalAmount: 4120.25, currencyCode: "EUR", status: ._3),
            .stub(id: "2", totalAmount: 1000, currencyCode: "CZK", status: ._3),
            .stub(id: "3", totalAmount: 79.75, currencyCode: "EUR", status: ._3)
        ]
        let totals = invoices.paid.totalsByCurrency
        XCTAssertEqual(totals.map(\.currencyCode), ["EUR", "CZK"])
        XCTAssertEqual(totals.map(\.amount), [4200, 1000])
    }

    func testNothingPaidIsNoTotal() {
        let unpaid: [Invoice] = [.stub(id: "1", totalAmount: 900, status: ._2)]
        XCTAssertTrue(unpaid.paid.isEmpty)
        XCTAssertTrue(unpaid.paid.totalsByCurrency.isEmpty)
    }

    // MARK: - Counts and copy

    /// Each count takes its language's forms and prints the number: one job is never "1 jobs".
    func testEachCountTakesItsLanguagesFormsAndPrintsTheNumber() throws {
        let counts: [String: (Int) -> String] = [
            "jobs done": { L10n.Earnings.jobsDoneCount($0) },
            "days remaining": { L10n.Earnings.daysRemaining($0) },
            "period pay jobs": { L10n.PeriodPay.jobsCount($0) },
            "invoices": { L10n.Invoices.summaryCount($0) },
            "invoice card jobs": { L10n.Invoices.cardJobsCount($0) },
            "invoice period jobs": { L10n.Invoices.periodJobs($0) }
        ]
        for language in ["en", "cs", "sk", "uk", "ru"] {
            try use(language)
            for (name, text) in counts {
                for count in [1, 2, 5, 21] {
                    XCTAssertTrue(text(count).contains("\(count)"), "\(name) · \(language) · \(text(count))")
                }
                XCTAssertNotEqual(
                    text(1).filter { !$0.isNumber },
                    text(5).filter { !$0.isNumber },
                    "\(name) · \(language): one and five read alike"
                )
            }
        }
    }

    func testTheFormsAreTheLanguagesOwn() throws {
        let expected: [String: [Int: String]] = [
            "en": [1: "1 day remaining", 2: "2 days remaining"],
            "cs": [1: "Zbývá 1 den", 2: "Zbývají 2 dny", 5: "Zbývá 5 dní"],
            "sk": [1: "Zostáva 1 deň", 3: "Zostávajú 3 dni", 5: "Zostáva 5 dní"],
            "uk": [1: "Залишився 1 день", 2: "Залишилось 2 дні", 5: "Залишилось 5 днів", 21: "Залишився 21 день"],
            "ru": [1: "Остался 1 день", 2: "Осталось 2 дня", 5: "Осталось 5 дней", 21: "Остался 21 день"]
        ]
        for (language, forms) in expected {
            try use(language)
            for (count, text) in forms {
                XCTAssertEqual(L10n.Earnings.daysRemaining(count), text, "\(language) \(count)")
            }
        }
        try use("ru")
        XCTAssertEqual(L10n.Invoices.summaryCount(1), "Всего 1 счёт")
        XCTAssertEqual(L10n.Invoices.summaryCount(5), "Всего 5 счетов")
    }

    /// The cash a cleaner holds is set off on their next invoice; no payout is promised.
    func testCashHeldIsSetOffWhenTheNextInvoiceIsIssued() throws {
        try use("en")
        XCTAssertTrue(L10n.Earnings.cashHeldSubtitle.hasSuffix("It is set off when your next invoice is issued."))
        for (language, payout) in ["cs": "výplat", "sk": "výplat", "uk": "виплат", "ru": "выплат"] {
            try use(language)
            XCTAssertFalse(L10n.Earnings.cashHeldSubtitle.contains(payout), language)
        }
    }

    // MARK: - What the screens print

    /// Under the total, once cash was set off, the PDF's statement: the cash set off as a minus, then the bank
    /// transfer, bold.
    func testTheInvoiceEndsWithTheCashSetOffAndTheTransferOnceCashWasSetOff() throws {
        let detail = try compactSource("InvoiceDetailContent.swift")
        for line in [
            "ifinvoice.cashSetOffAmount>0{",
            "label:L10n.Invoices.cashSetOff,amount:-invoice.cashSetOffAmount,",
            "label:L10n.Invoices.bankTransfer,amount:invoice.transferAmount,code:invoice.currencyCode,bold:true"
        ] {
            XCTAssertTrue(detail.contains(line), line)
        }
    }

    /// The list, the invoice and My Pay name the period from its days in the in-app language, never the server's
    /// English label.
    func testEveryPayScreenNamesThePeriodFromItsDays() throws {
        let screens = [
            "InvoicesListContent.swift": "invoice",
            "InvoiceDetailContent.swift": "invoice",
            "PeriodPayContent.swift": "summary"
        ]
        for (file, owner) in screens {
            let source = try compactSource(file)
            let call = "EarningsFormat.period(start:\(owner).payPeriodStart,end:\(owner).payPeriodEnd,locale:locale)"
            XCTAssertTrue(source.contains(call), file)
            XCTAssertFalse(source.contains("payPeriodLabel"), file)
        }
    }

    // MARK: - Helpers

    private func use(_ language: String) throws {
        let path = [Bundle.main, Bundle(for: Self.self)].lazy.compactMap {
            $0.path(forResource: language, ofType: "lproj")
        }.first
        L10n.bundle = try XCTUnwrap(Bundle(path: XCTUnwrap(path, "no \(language).lproj")))
        CoreL10n.apply(languageTag: language)
    }

    private func compactSource(_ file: String) throws -> String {
        let earnings = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features/Earnings")
        return try String(contentsOf: earnings.appendingPathComponent(file), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }
}
