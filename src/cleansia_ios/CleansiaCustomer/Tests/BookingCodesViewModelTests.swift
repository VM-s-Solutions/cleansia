import CleansiaCore
import XCTest
@testable import CleansiaCustomer

@MainActor
final class BookingCodesViewModelTests: XCTestCase {
    private func makeVM(
        extra: FakeExtraClient = FakeExtraClient(),
        promo: FakePromoCodeClient = FakePromoCodeClient(),
        referral: FakeReferralClient = FakeReferralClient(),
        quote: FakeQuoteClient = FakeQuoteClient()
    ) -> BookingViewModel {
        BookingViewModel(
            catalogClient: FakeCatalogClient(),
            quoteClient: quote,
            extraClient: extra,
            promoClient: promo,
            referralClient: referral,
            scheduler: TestScheduler.dispatch.eraseToAnyScheduler()
        )
    }

    func testExtrasStartLoadingThenLoadedSorted() async {
        let extra = FakeExtraClient(result: .success(CatalogFixtures.extras))
        let vm = makeVM(extra: extra)

        XCTAssertTrue(vm.extrasState.isLoading)
        await vm.loadExtras()

        XCTAssertEqual(vm.extrasState.loadedValue?.map(\.slug), ["windows", "inside-oven"])
        XCTAssertEqual(extra.callCount, 1)
    }

    func testLoadExtrasIsIdempotentOnceLoaded() async {
        let extra = FakeExtraClient(result: .success(CatalogFixtures.extras))
        let vm = makeVM(extra: extra)
        await vm.loadExtras()
        await vm.loadExtras()

        XCTAssertEqual(extra.callCount, 1)
    }

    func testToggleExtraMutatesBookingStateSlugSet() {
        let vm = makeVM()

        vm.toggleExtra("windows")
        XCTAssertEqual(vm.state.selectedExtraSlugs, ["windows"])

        vm.toggleExtra("inside-oven")
        XCTAssertEqual(vm.state.selectedExtraSlugs, ["windows", "inside-oven"])

        vm.toggleExtra("windows")
        XCTAssertEqual(vm.state.selectedExtraSlugs, ["inside-oven"])
    }

    func testValidatePromoWalksIdleValidatingValidAndPersistsCode() async {
        let promo = FakePromoCodeClient(result: .success(PromoValidation(
            isValid: true,
            discountAmount: 150,
            errorCode: nil
        )))
        let vm = makeVM(promo: promo)

        let outcome = await vm.validatePromoCode(" welcome20 ")

        XCTAssertEqual(outcome, .valid(discountAmount: 150))
        XCTAssertEqual(vm.promoState, .valid(discountAmount: 150))
        XCTAssertEqual(vm.state.promoCode, "WELCOME20")
        XCTAssertEqual(promo.lastCode, "WELCOME20")
    }

    func testValidPromoFeedsTheDisplayDiscount() async {
        let promo = FakePromoCodeClient(result: .success(PromoValidation(
            isValid: true,
            discountAmount: 150,
            errorCode: nil
        )))
        let vm = makeVM(promo: promo)

        _ = await vm.validatePromoCode("WELCOME20")

        XCTAssertEqual(vm.promoState.discount, 150)
    }

    func testValidatePromoMapsErrorCodeToTypedInvalid() async {
        let promo = FakePromoCodeClient(result: .success(PromoValidation(
            isValid: false,
            discountAmount: nil,
            errorCode: "Expired"
        )))
        let vm = makeVM(promo: promo)

        let outcome = await vm.validatePromoCode("OLDCODE")

        XCTAssertEqual(outcome, .invalid(.expired))
        XCTAssertEqual(vm.promoState, .invalid(.expired))
        XCTAssertEqual(vm.state.promoCode, "")
    }

    func testValidatePromoNetworkFailureIsGenericInvalid() async {
        let promo = FakePromoCodeClient(result: .failure(ApiError(code: "x")))
        let vm = makeVM(promo: promo)

        let outcome = await vm.validatePromoCode("ANY")

        XCTAssertEqual(outcome, .invalid(nil))
        XCTAssertEqual(vm.promoState, .invalid(nil))
    }

    func testBlankPromoResetsToIdleWithoutCallingClient() async {
        let promo = FakePromoCodeClient()
        let vm = makeVM(promo: promo)

        let outcome = await vm.validatePromoCode("   ")

        XCTAssertEqual(outcome, .idle)
        XCTAssertEqual(promo.callCount, 0)
    }

    func testPromoSubtotalComesFromQuotedTotal() async {
        let quote = FakeQuoteClient(result: .success(BookingQuote(totalPrice: 2400, currencyCode: "CZK")))
        let promo = FakePromoCodeClient()
        let vm = makeVM(promo: promo, quote: quote)

        let scheduler = TestScheduler.dispatch
        let vmWithSched = BookingViewModel(
            catalogClient: FakeCatalogClient(),
            quoteClient: quote,
            extraClient: FakeExtraClient(),
            promoClient: promo,
            referralClient: FakeReferralClient(),
            scheduler: scheduler.eraseToAnyScheduler()
        )
        vmWithSched.update { var s = $0
            s.selectedServiceIds = ["s-1"]
            return s
        }
        scheduler.advance(by: .milliseconds(400))
        for _ in 0 ..< 5 {
            await Task.yield()
        }

        _ = await vmWithSched.validatePromoCode("WELCOME20")
        XCTAssertEqual(promo.lastSubtotal, 2400)
        _ = vm
    }

    func testClearPromoDropsStateAndPayload() async {
        let promo = FakePromoCodeClient(result: .success(PromoValidation(
            isValid: true,
            discountAmount: 100,
            errorCode: nil
        )))
        let vm = makeVM(promo: promo)
        _ = await vm.validatePromoCode("WELCOME20")
        XCTAssertEqual(vm.state.promoCode, "WELCOME20")

        vm.clearPromoCode()

        XCTAssertEqual(vm.promoState, .idle)
        XCTAssertEqual(vm.state.promoCode, "")
    }

    func testValidateReferralValidPersistsCodeAndName() async {
        let referral = FakeReferralClient(result: .success(ReferralValidation(
            isValid: true,
            referrerFirstName: "Eva",
            errorCode: nil
        )))
        let vm = makeVM(referral: referral)

        let outcome = await vm.validateReferralCode(" anna7 ")

        XCTAssertEqual(outcome, .valid(referrerFirstName: "Eva"))
        XCTAssertEqual(vm.referralState, .valid(referrerFirstName: "Eva"))
        XCTAssertEqual(vm.state.referralCode, "ANNA7")
        XCTAssertEqual(referral.lastCode, "ANNA7")
    }

    func testReferralInvalidMapsTypedErrorFailSoft() async {
        let referral = FakeReferralClient(result: .success(ReferralValidation(
            isValid: false,
            referrerFirstName: nil,
            errorCode: "AlreadyReferred"
        )))
        let vm = makeVM(referral: referral)

        let outcome = await vm.validateReferralCode("DUPE")

        XCTAssertEqual(outcome, .invalid(.alreadyReferred))
        XCTAssertEqual(vm.referralState, .invalid(.alreadyReferred))
    }

    func testReferralNetworkFailureIsGenericInvalidNotAFatalError() async {
        let referral = FakeReferralClient(result: .failure(ApiError(code: "x")))
        let vm = makeVM(referral: referral)

        let outcome = await vm.validateReferralCode("ANY")

        XCTAssertEqual(outcome, .invalid(nil))
        XCTAssertEqual(vm.referralState, .invalid(nil))
    }

    func testBlankReferralResetsToIdle() async {
        let referral = FakeReferralClient()
        let vm = makeVM(referral: referral)

        let outcome = await vm.validateReferralCode("")

        XCTAssertEqual(outcome, .idle)
        XCTAssertEqual(referral.callCount, 0)
    }

    func testClearReferralDropsStateAndPayload() async {
        let referral = FakeReferralClient(result: .success(ReferralValidation(
            isValid: true,
            referrerFirstName: "Eva",
            errorCode: nil
        )))
        let vm = makeVM(referral: referral)
        _ = await vm.validateReferralCode("ANNA7")

        vm.clearReferralCode()

        XCTAssertEqual(vm.referralState, .idle)
        XCTAssertEqual(vm.state.referralCode, "")
    }

    func testApplyAddressPopulatesStreetCityZipAndCoords() {
        let vm = makeVM()
        let picked = GeocodedAddress(
            latitude: 50.0755,
            longitude: 14.4378,
            street: "Vinohradská 12",
            city: "Praha",
            zipCode: "120 00",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Vinohradská 12, Praha"
        )

        vm.applyAddress(picked)

        XCTAssertEqual(vm.state.street, "Vinohradská 12")
        XCTAssertEqual(vm.state.city, "Praha")
        XCTAssertEqual(vm.state.zipCode, "120 00")
        XCTAssertEqual(vm.state.countryIsoCode, "cz")
        XCTAssertNil(vm.state.savedAddressId)
    }

    func testApplyAddressFallsBackToFormattedWhenStreetEmpty() {
        let vm = makeVM()
        let picked = GeocodedAddress(
            latitude: 50.0,
            longitude: 14.0,
            street: "",
            city: "Praha",
            zipCode: "",
            country: "Czechia",
            countryIsoCode: "cz",
            formatted: "Náměstí Míru, Praha"
        )

        vm.applyAddress(picked)

        XCTAssertEqual(vm.state.street, "Náměstí Míru, Praha")
        XCTAssertEqual(vm.state.city, "Praha")
    }

    func testSelectDayAndTimeSetInstant() throws {
        let calendar = Calendar(identifier: .gregorian)
        let now = calendar.date(from: DateComponents(year: 2026, month: 7, day: 1, hour: 8)) ?? Date()
        let vm = makeVM()
        let tomorrow = try XCTUnwrap(calendar.date(byAdding: .day, value: 1, to: now))

        vm.selectDay(tomorrow, calendar: calendar)
        XCTAssertFalse(vm.state.selectedDate.isEmpty)
        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertNil(vm.state.selectedInstant)

        vm.selectTime("09:00", on: tomorrow, calendar: calendar)
        XCTAssertEqual(vm.state.selectedTime, "09:00")
        let instant = vm.state.selectedInstant
        XCTAssertNotNil(instant)
        XCTAssertEqual(try calendar.component(.hour, from: XCTUnwrap(instant)), 9)
    }

    func testClearSelectedTimeIfUnavailableDropsSlotMissingFromTheDay() {
        let vm = makeVM()
        vm.update { var s = $0
            s.selectedTime = "08:00"
            s.selectedInstant = Date()
            return s
        }

        vm.clearSelectedTimeIfUnavailable(slots: [BookingTimeSlot(time: "10:00", state: .available)])

        XCTAssertEqual(vm.state.selectedTime, "")
        XCTAssertNil(vm.state.selectedInstant)
    }

    func testClearSelectedTimeIfUnavailableKeepsStillAvailableSlot() {
        let vm = makeVM()
        vm.update { var s = $0
            s.selectedTime = "10:00"
            return s
        }

        vm.clearSelectedTimeIfUnavailable(slots: [BookingTimeSlot(time: "10:00", state: .available)])

        XCTAssertEqual(vm.state.selectedTime, "10:00")
    }
}
