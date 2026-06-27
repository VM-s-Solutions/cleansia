import XCTest
@testable import CleansiaPartner

final class ProfileSectionRoutingTests: XCTestCase {
    func testFirstMissingResolvesToOwningSectionInPriorityOrder() {
        let route = ProfileSectionRouting.firstMissingSection(
            missingFields: ["profile.fields.iban"],
            forOnboarding: true
        )
        XCTAssertEqual(route, .bank(onboarding: true))
    }

    func testFirstMissingPrefersEarlierSectionWhenMultipleMissing() {
        let route = ProfileSectionRouting.firstMissingSection(
            missingFields: ["profile.fields.iban", "profile.fields.firstName"],
            forOnboarding: true
        )
        XCTAssertEqual(route, .personal(onboarding: true))
    }

    func testFirstMissingResolvesAddressFields() {
        let route = ProfileSectionRouting.firstMissingSection(
            missingFields: ["profile.fields.city"],
            forOnboarding: true
        )
        XCTAssertEqual(route, .address(onboarding: true))
    }

    func testFirstMissingResolvesIdentificationFields() {
        let route = ProfileSectionRouting.firstMissingSection(
            missingFields: ["profile.fields.passportId"],
            forOnboarding: true
        )
        XCTAssertEqual(route, .identification(onboarding: true))
    }

    func testFirstMissingFallsBackToPersonalWhenUnknown() {
        let route = ProfileSectionRouting.firstMissingSection(
            missingFields: ["profile.fields.unknownField"],
            forOnboarding: false
        )
        XCTAssertEqual(route, .personal(onboarding: false))
    }
}
