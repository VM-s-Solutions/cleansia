import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Per-surface pins for the money mappers: a payload with every member populated maps, and removing
/// a member the C# property declares non-nullable **fails** the mapping rather than yielding a
/// plausible zero. The refusal names the field, so each assertion also pins the name.
///
/// Nullability is read from the C# records — never from the spec, whose 359 plain-string properties
/// are `nullable: true` without exception and therefore discriminate nothing.
final class PartnerWireContractTests: XCTestCase {
    // MARK: DashboardStatsDto

    private func statsPayload() -> DashboardStatsDto {
        DashboardStatsDto(
            availableOrdersCount: 4,
            myActiveOrdersCount: 1,
            thisMonthCompletedOrders: 26,
            lastMonthCompletedOrders: 22,
            todayEarnings: 1238,
            todayCompletedCount: 1,
            weekEarnings: 6262,
            weekCompletedCount: 4,
            lastMonthEarnings: 18450,
            currentPeriodEarnings: 9500,
            currentPayPeriodStart: Date(),
            currentPayPeriodEnd: Date(),
            nextPayoutDate: Date(),
            averageRating: 4.8,
            ratingCount: 12,
            latestInvoiceStatus: "Paid",
            currencyCode: "CZK"
        )
    }

    func testAFullyPopulatedStatsPayloadMaps() throws {
        let stats = try DashboardStats(statsPayload())
        XCTAssertEqual(stats.currentPeriodEarnings, 9500)
        XCTAssertEqual(stats.weekCompletedCount, 4)
        XCTAssertEqual(stats.averageRating, 4.8)
    }

    func testEveryNonNullableStatsFigureIsRefusedRatherThanZeroed() {
        let cases: [(String, (inout DashboardStatsDto) -> Void)] = [
            ("todayEarnings", { $0.todayEarnings = nil }),
            ("todayCompletedCount", { $0.todayCompletedCount = nil }),
            ("weekEarnings", { $0.weekEarnings = nil }),
            ("weekCompletedCount", { $0.weekCompletedCount = nil }),
            ("lastMonthEarnings", { $0.lastMonthEarnings = nil }),
            ("lastMonthCompletedOrders", { $0.lastMonthCompletedOrders = nil }),
            ("thisMonthCompletedOrders", { $0.thisMonthCompletedOrders = nil }),
            ("currentPeriodEarnings", { $0.currentPeriodEarnings = nil }),
            ("ratingCount", { $0.ratingCount = nil })
        ]
        for (field, break_) in cases {
            var payload = statsPayload()
            break_(&payload)
            assertRefused(field) { try DashboardStats(payload) }
        }
    }

    /// `nullable: true` on the wire carries a real "never reviewed", and forcing it would destroy
    /// the distinction the server drew.
    func testAnUnratedCleanerIsNotARefusal() throws {
        var payload = statsPayload()
        payload.averageRating = nil
        XCTAssertNil(try DashboardStats(payload).averageRating)
    }

    // MARK: EmployeeInvoiceDto — refuse the page

    private func invoicePayload() -> EmployeeInvoiceDto {
        EmployeeInvoiceDto(
            id: "inv-1",
            employeeId: "emp-1",
            employeeName: "Jana",
            payPeriodId: "pp-1",
            payPeriodLabel: "1 – 15 Jun 2026",
            invoiceNumber: "INV-2026-001",
            totalOrders: 3,
            subTotal: 4000,
            bonusAmount: 250,
            deductionAmount: 50,
            totalAmount: 4200,
            currencyCode: "CZK",
            status: ._3,
            pdfGenerationFailed: false,
            generatedAt: Date(),
            paidAt: Date()
        )
    }

    func testAFullyPopulatedInvoiceRowMaps() throws {
        let invoice = try Invoice(invoicePayload())
        XCTAssertEqual(invoice.id, "inv-1")
        XCTAssertEqual(invoice.totalAmount, 4200)
        XCTAssertEqual(invoice.status, ._3)
    }

    /// The rollup sums the rows, so a broken row must not be silently dropped out of the total.
    func testABrokenInvoiceRowRefusesTheWholePage() {
        for (field, break_) in [
            ("totalAmount", { (dto: inout EmployeeInvoiceDto) in dto.totalAmount = nil }),
            ("totalOrders", { dto in dto.totalOrders = nil }),
            ("status", { dto in dto.status = nil }),
            ("id", { dto in dto.id = "" })
        ] {
            var payload = invoicePayload()
            break_(&payload)
            assertRefused(field) { try [payload].map(Invoice.init) }
        }
    }

    /// The card renders the absence of a date, so leaving it nil fabricates nothing.
    func testAnUngeneratedInvoiceDateIsNotARefusal() throws {
        var payload = invoicePayload()
        payload.generatedAt = nil
        payload.paidAt = nil
        XCTAssertNil(try Invoice(payload).generatedAt)
    }

    // MARK: PeriodPaySummaryDto — drop the line, refuse the summary

    private func summaryPayload() -> PeriodPaySummaryDto {
        PeriodPaySummaryDto(
            payPeriodId: "pp-1",
            payPeriodLabel: "1 – 15 Jun 2026",
            employeeId: "emp-1",
            employeeName: "Jana",
            totalOrders: 2,
            totalBasePay: 3600,
            totalExtrasPay: 300,
            totalExpensesPay: 200,
            totalBonusPay: 150,
            totalDeductionPay: 50,
            grandTotal: 4200,
            hasInvoice: false,
            orderPays: [
                OrderEmployeePayDto(id: "line-1", orderNumber: "ORD-1", totalPay: 2100, createdOn: Date()),
                OrderEmployeePayDto(id: "line-2", orderNumber: "ORD-2", totalPay: 2100, createdOn: Date())
            ]
        )
    }

    func testAFullyPopulatedSummaryMaps() throws {
        let summary = try PeriodPaySummary(summaryPayload())
        XCTAssertEqual(summary.grandTotal, 4200)
        XCTAssertEqual(summary.orderPays.count, 2)
    }

    func testEveryNonNullableSummaryTotalIsRefused() {
        for (field, break_) in [
            ("grandTotal", { (dto: inout PeriodPaySummaryDto) in dto.grandTotal = nil }),
            ("totalBasePay", { dto in dto.totalBasePay = nil }),
            ("totalExtrasPay", { dto in dto.totalExtrasPay = nil }),
            ("totalExpensesPay", { dto in dto.totalExpensesPay = nil }),
            ("totalBonusPay", { dto in dto.totalBonusPay = nil }),
            ("totalDeductionPay", { dto in dto.totalDeductionPay = nil }),
            ("totalOrders", { dto in dto.totalOrders = nil })
        ] {
            var payload = summaryPayload()
            break_(&payload)
            assertRefused(field) { try PeriodPaySummary(payload) }
        }
    }

    /// Every figure on the screen is the summary's own, so a line that cannot be identified is
    /// dropped rather than blanking a payslip the server answered correctly.
    func testAnUnidentifiableLineIsDroppedAndTheSummaryStillRenders() throws {
        var payload = summaryPayload()
        payload.orderPays?[0].id = nil
        let summary = try PeriodPaySummary(payload)
        XCTAssertEqual(summary.orderPays.map(\.id), ["line-2"])
        XCTAssertEqual(summary.grandTotal, 4200)
    }

    /// The drop covers identity only. A surviving line whose own money is broken refuses, and
    /// because the line is an element of the summary that refusal refuses the summary.
    func testALineWithBrokenMoneyRefusesTheSummary() {
        var payload = summaryPayload()
        payload.orderPays?[1].totalPay = nil
        assertRefused("totalPay") { try PeriodPaySummary(payload) }
    }

    // MARK: OrderItem — the detail

    func testAFullyPopulatedOrderMaps() throws {
        var item = OrderItem.wireComplete()
        item.rooms = 3
        item.bathrooms = 2
        let detail = try OrderDetail(item)
        XCTAssertEqual(detail.id, "order-1")
        XCTAssertEqual(detail.rooms, 3)
    }

    func testTheOrderDetailRefusesSynthesizedIdentityQuantitiesAndFlags() {
        for (field, break_) in [
            ("id", { (item: inout OrderItem) in item.id = "" }),
            ("displayOrderNumber", { item in item.displayOrderNumber = nil }),
            ("rooms", { item in item.rooms = nil }),
            ("bathrooms", { item in item.bathrooms = nil }),
            ("isAssignedToCurrentUser", { item in item.isAssignedToCurrentUser = nil }),
            ("hasAfterPhotos", { item in item.hasAfterPhotos = nil })
        ] {
            var item = OrderItem.wireComplete()
            break_(&item)
            assertRefused(field) { try OrderDetail(item) }
        }
    }

    /// A server that predates the seat block says nothing about seats; one that sends a partial
    /// block would otherwise say "full", which is a claim about whether the cleaner can take it.
    func testAPartialSeatBlockRefusesWhileAnAbsentOneStaysSilent() throws {
        var absent = OrderItem.wireComplete()
        absent.requiredEmployees = nil
        XCTAssertNil(try OrderCrew(absent))

        for (field, break_) in [
            ("availableSpots", { (item: inout OrderItem) in item.availableSpots = nil }),
            ("hasAvailableSpots", { item in item.hasAvailableSpots = nil })
        ] {
            var partial = OrderItem.wireComplete()
            partial.requiredEmployees = 2
            partial.availableSpots = 1
            partial.hasAvailableSpots = true
            break_(&partial)
            assertRefused(field) { try OrderCrew(partial) }
        }
    }

    private func assertRefused(
        _ field: String,
        file: StaticString = #filePath,
        line: UInt = #line,
        _ map: () throws -> some Any
    ) {
        XCTAssertThrowsError(try map(), "\(field) was supplied a value instead of refusing", file: file, line: line) {
            XCTAssertEqual(
                $0 as? WireContractViolation,
                WireContractViolation(field: field),
                "the refusal must name \(field)",
                file: file,
                line: line
            )
        }
    }
}
