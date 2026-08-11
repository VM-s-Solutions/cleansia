import XCTest
@testable import CleansiaPartner

final class JobRadiusFormTests: XCTestCase {
    func testTheBoundsMirrorTheServerRange() {
        XCTAssertEqual(JobRadiusBounds.minimumKm, 1)
        XCTAssertEqual(JobRadiusBounds.maximumKm, 500)
        XCTAssertTrue((JobRadiusBounds.minimumKm ... JobRadiusBounds.maximumKm).contains(JobRadiusBounds.startingKm))
    }

    func testAnUnsetRadiusIsTheAnywhereChoiceAndCarriesNoDistance() {
        let form = JobRadiusForm(radiusKm: nil)

        XCTAssertFalse(form.isLimited)
        XCTAssertEqual(form.selection, .anywhere)
        XCTAssertNil(form.selection.wireValue)
    }

    func testAnUnsetRadiusNeverGoesOnTheWireAsZero() {
        XCTAssertNotEqual(JobRadiusForm(radiusKm: nil).selection.wireValue, 0)
        XCTAssertNotEqual(JobRadiusSelection.anywhere.wireValue, 0)
    }

    func testASetRadiusSeedsTheSliderAndTravelsAsItself() {
        let form = JobRadiusForm(radiusKm: 40)

        XCTAssertTrue(form.isLimited)
        XCTAssertEqual(form.kilometres, 40)
        XCTAssertEqual(form.selection, .within(40))
        XCTAssertEqual(form.selection.wireValue, 40)
    }

    func testAnOutOfRangeSeedIsClampedRatherThanSubmittedAsIs() {
        XCTAssertEqual(JobRadiusForm(radiusKm: 0).kilometres, JobRadiusBounds.minimumKm)
        XCTAssertEqual(JobRadiusForm(radiusKm: 9999).kilometres, JobRadiusBounds.maximumKm)
        XCTAssertEqual(JobRadiusForm(radiusKm: -5).selection.wireValue, JobRadiusBounds.minimumKm)
    }

    func testSliderValuesAreRoundedAndClamped() {
        var form = JobRadiusForm(radiusKm: 10)

        form.setKilometres(42.6)
        XCTAssertEqual(form.kilometres, 43)

        form.setKilometres(0.2)
        XCTAssertEqual(form.kilometres, JobRadiusBounds.minimumKm)

        form.setKilometres(10000)
        XCTAssertEqual(form.kilometres, JobRadiusBounds.maximumKm)
    }

    func testTurningTheLimitOffAndBackOnRestoresTheChosenDistance() {
        var form = JobRadiusForm(radiusKm: 120)

        form.setLimited(false)
        XCTAssertEqual(form.selection, .anywhere)
        XCTAssertNil(form.selection.wireValue)

        form.setLimited(true)
        XCTAssertEqual(form.selection, .within(120))
    }

    func testAFreshFormOffersTheStartingDistanceOnceTheLimitIsTurnedOn() {
        var form = JobRadiusForm(radiusKm: nil)
        form.setLimited(true)

        XCTAssertEqual(form.selection, .within(JobRadiusBounds.startingKm))
    }

    func testTheSelectionMapsBackFromAServerValue() {
        XCTAssertEqual(JobRadiusSelection(radiusKm: nil), .anywhere)
        XCTAssertEqual(JobRadiusSelection(radiusKm: 30), .within(30))
        XCTAssertEqual(JobRadiusSelection(radiusKm: 900), .within(JobRadiusBounds.maximumKm))
    }
}
