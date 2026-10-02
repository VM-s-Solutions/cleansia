import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

extension CustomerWireContractTests {
    // MARK: the Plus plans — priced per market, so every figure travels with its unit

    func planPayload() -> GetMembershipPlansResponse {
        GetMembershipPlansResponse(
            code: "PLUS_MONTHLY",
            name: "Monthly",
            price: 199,
            monthlyEquivalentPrice: 199,
            billingInterval: 1,
            discountPercentage: 5,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            expressUpgradesPerMonth: 2,
            trialPeriodDays: 0,
            savingsPercentVsMonthly: 0,
            currencyCode: "CZK"
        )
    }

    func testAFullyPopulatedPlanMapsWithItsCurrency() throws {
        let plan = try planPayload().toDomain()
        XCTAssertEqual(plan.price, 199)
        XCTAssertEqual(plan.currencyCode, "CZK")
        XCTAssertEqual(plan.billingInterval, 1)
        XCTAssertEqual(plan.expressUpgradesPerMonth, 2)
    }

    /// A plan without its unit would be printed with a unit guessed for it, and one without its
    /// money would be advertised as free.
    func testABrokenPlanIsRefusedRatherThanPricedOrLabelledByGuess() {
        for (field, corrupt) in [
            ("price", { (dto: inout GetMembershipPlansResponse) in dto.price = nil }),
            ("monthlyEquivalentPrice", { dto in dto.monthlyEquivalentPrice = nil }),
            ("billingInterval", { dto in dto.billingInterval = nil }),
            ("expressUpgradesPerMonth", { dto in dto.expressUpgradesPerMonth = nil }),
            ("currencyCode", { dto in dto.currencyCode = nil }),
            ("currencyCode", { dto in dto.currencyCode = "" })
        ] {
            var payload = planPayload()
            corrupt(&payload)
            assertRefused(field) { try payload.toDomain() }
        }
    }

    // MARK: my membership — its own price and currency, for the life of the subscription

    func testAMembershipMapsItsOwnPriceAndCurrency() throws {
        let membership = try GetMyMembershipResponse(
            hasMembership: true,
            planCode: "PLUS_MONTHLY",
            planName: "Cleansia Plus",
            price: 199,
            cancelRequested: false,
            billingInterval: 1,
            monthlyEquivalentPrice: 199,
            currencyCode: "CZK"
        ).toDomain()
        XCTAssertEqual(membership.price, 199)
        XCTAssertEqual(membership.monthlyEquivalentPrice, 199)
        XCTAssertEqual(membership.currencyCode, "CZK")
    }

    /// `price` is null without a membership and when the plan's row in that currency is gone; neither
    /// is a broken wire, so the snapshot still maps and the figures are simply not printed.
    func testAMembershipWithoutAPriceRowStillMaps() throws {
        let membership = try GetMyMembershipResponse(hasMembership: false, cancelRequested: false).toDomain()
        XCTAssertFalse(membership.hasMembership)
        XCTAssertNil(membership.price)
        XCTAssertNil(membership.currencyCode)
    }

    /// A failed renewal keeps the enrolment alive, so `hasMembership` stays true; only the status says that
    /// no benefit runs.
    func testAPastDueOrPausedMembershipIsLiveButItsBenefitsArePaused() throws {
        for status in [MembershipStatus._2, ._4] {
            let membership = try GetMyMembershipResponse(
                hasMembership: true,
                status: status,
                cancelRequested: false
            ).toDomain()
            XCTAssertTrue(membership.hasMembership)
            XCTAssertTrue(membership.benefitsPaused, "status \(status.rawValue)")
        }
    }

    /// One free trial per account: a trial the server has not confirmed is never advertised.
    func testTrialEligibilityMapsAndAnUnstatedAnswerOffersNoTrial() throws {
        let cases: [(stated: Bool?, expected: Bool)] = [(true, true), (false, false), (nil, false)]
        for (stated, expected) in cases {
            let membership = try GetMyMembershipResponse(
                hasMembership: false,
                cancelRequested: false,
                trialEligible: stated
            ).toDomain()
            XCTAssertEqual(membership.trialEligible, expected, "trialEligible \(String(describing: stated))")
        }
    }

    func testAnActiveMembershipRunsItsBenefits() throws {
        let membership = try GetMyMembershipResponse(
            hasMembership: true,
            status: ._1,
            cancelRequested: false
        ).toDomain()
        XCTAssertFalse(membership.benefitsPaused)
    }

    // MARK: the express-waiver quota — a claim, not a number

    /// The one case where the coerced value is the OPPOSITE of what the server's null means:
    /// *null = no membership*, and `?? 0` turned it into "you used your allowance up".
    func testAnUnreportedQuotaIsNotReportedAsUsedUp() {
        XCTAssertEqual(
            ExpressWaiverStatus.resolve(
                hasMembership: true,
                upgradesPerMonth: 2,
                upgradesRemaining: nil
            ),
            .none
        )
    }

    // MARK: notification preferences — read, then written back

    func testEveryPreferenceIsRefusedBecauseTheScreenWritesThemBack() {
        for (field, corrupt) in [
            ("orderUpdates", { (dto: inout NotificationPreferencesDto) in dto.orderUpdates = nil }),
            ("promo", { dto in dto.promo = nil }),
            ("disputeReply", { dto in dto.disputeReply = nil })
        ] {
            var payload = preferencesPayload()
            corrupt(&payload)
            assertRefused(field) { try payload.toDomain() }
        }
    }

    func testAFullyPopulatedPreferencePayloadMaps() throws {
        let preferences = try preferencesPayload().toDomain()
        XCTAssertTrue(preferences.orderUpdates)
        XCTAssertFalse(preferences.promo)
    }

    func preferencesPayload() -> NotificationPreferencesDto {
        NotificationPreferencesDto(
            orderUpdates: true,
            cleanerOnTheWay: true,
            orderCompleted: true,
            orderCancelled: true,
            refundIssued: true,
            membershipExpiring: true,
            membershipCancelled: true,
            tierUpgrade: true,
            promo: false,
            disputeReply: true,
            recurringScheduled: true
        )
    }

    // MARK: the referral code — the one string a spec sweep reads clean

    func testAReferralAccountWithNoCodeRefusesRatherThanSharingNothing() {
        var payload = GetMyReferralResponse(
            code: "JANE-2026",
            timesUsed: 3,
            qualifiedCount: 2,
            acceptedCount: 3,
            pointsPerReferral: 250
        )
        XCTAssertEqual(try? payload.toDomain().code, "JANE-2026")
        payload.code = ""
        assertRefused("code") { try payload.toDomain() }
    }

    // MARK: the saved-address picker

    func testASavedAddressIsRefusedRatherThanLosingItsDefaultFlag() {
        var payload = SavedAddressDto(
            id: "addr-1",
            label: "Home",
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            isDefault: true
        )
        XCTAssertEqual(try? payload.toDomain().isDefault, true)
        payload.isDefault = nil
        assertRefused("isDefault") { try payload.toDomain() }
    }

    /// The recurring form prices its catalogue for the picked address's country, so the row it picks
    /// from has to carry it; a row without an id is dropped, one without a country reads the default.
    func testARecurringAddressCarriesItsCountry() {
        var payload = SavedAddressDto(
            id: "addr-1",
            label: "Home",
            street: "Hlavná 1",
            city: "Bratislava",
            zipCode: "811 01",
            countryId: "svk",
            isDefault: true
        )
        XCTAssertEqual(payload.toRecurringAddress()?.countryId, "svk")
        XCTAssertEqual(payload.toRecurringAddress()?.isDefault, true)

        payload.countryId = nil
        XCTAssertNil(payload.toRecurringAddress()?.countryId)
        XCTAssertNotNil(payload.toRecurringAddress())

        payload.id = nil
        XCTAssertNil(payload.toRecurringAddress())
    }

    func assertRefused(
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
