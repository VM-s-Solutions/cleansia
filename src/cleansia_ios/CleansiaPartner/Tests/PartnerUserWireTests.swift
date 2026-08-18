import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Asserts the bytes the production client puts on the wire, not a re-encoding of the command object.
/// `Nullable` is enabled on the mobile partner host and `Command.PhoneNumber` is a non-nullable
/// reference type, so an OMITTED member is refused by the model binder before the handler runs — and
/// `encodeIfPresent` omits exactly what a nil maps to. A blank string is what "nothing to say" looks
/// like there, and the backend preserves the stored number when one arrives.
@MainActor
final class PartnerUserWireTests: XCTestCase {
    private var bodies: WireBodies!

    override func setUp() {
        super.setUp()
        bodies = WireBodies()
        GeneratedWireSpine.install(recording: bodies) { request in
            request.url?.path == "/api/User/GetCurrent"
                ? (200, Self.profileBody)
                : (200, Data(#"{"id":"user-1"}"#.utf8))
        }
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        bodies = nil
        super.tearDown()
    }

    func testABlankPhoneReachesTheWireAsABlankRatherThanAnOmittedMember() async throws {
        await push()

        let wire = try XCTUnwrap(bodies.text(ofPath: Self.updatePath))
        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertTrue(body.keys.contains("phoneNumber"), wire)
        XCTAssertEqual(body["phoneNumber"] as? String, "", wire)
    }

    /// The vacuity guard for the blank: the same field, populated, has to survive the same round trip.
    func testAStoredPhoneIsReplayedRatherThanBlanked() async throws {
        await push(profile: Self.profileBody(phoneNumber: "+420777111222"))

        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertEqual(body["phoneNumber"] as? String, "+420777111222")
    }

    /// `UpdateCurrentUser` replaces first and last name outright, so a language-only command would blank
    /// the cleaner's name — and the birth date and phone would fall back to whatever the handler holds
    /// only because they are the "nothing to say" fields. All of it travels.
    func testTheWholeProfileIsReplayedBesideTheNewLanguage() async throws {
        await push(profile: Self.profileBody(phoneNumber: "+420777111222"))

        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertEqual(body["firstName"] as? String, "Ondrej")
        XCTAssertEqual(body["lastName"] as? String, "Novak")
        XCTAssertEqual(body["birthDate"] as? String, "1982-09-04")
        XCTAssertEqual(body["languageCode"] as? String, "uk")
    }

    /// A day fed through the profile stub always decodes to midnight UTC, where a positive device offset
    /// lands inside the same day and the encoded string is unchanged — so the assertion above says
    /// nothing about which zone the day was read in. Drive the client directly with an instant late in
    /// the UTC day, which is the case that separates the two.
    func testTheBirthDayIsReadInGreenwichWhateverInstantCarriesIt() async throws {
        _ = await LivePartnerUserClient().updateCurrentUser(CurrentUserUpdate(
            firstName: "Ondrej",
            lastName: "Novak",
            phoneNumber: "",
            birthDate: Date(timeIntervalSince1970: 400_030_200),
            languageCode: "uk"
        ))

        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertEqual(body["birthDate"] as? String, "1982-09-04")
    }

    /// The avatar is a three-way choice and a language save says nothing about it.
    func testTheReplayNeverAsksForTheAvatarToBeRemoved() async throws {
        await push()

        let body = try XCTUnwrap(bodies.json(ofPath: Self.updatePath))
        XCTAssertNil(body["photo"])
        XCTAssertEqual(body["removePhoto"] as? Bool, false)
    }

    func testThePushReadsAndWritesTheMobilePartnerUserEndpoints() async {
        await push()

        XCTAssertEqual(bodies.paths, ["/api/User/GetCurrent", Self.updatePath])
        XCTAssertEqual(bodies.method(ofPath: Self.updatePath), "PUT")
    }

    /// Why the seam's phone is a non-optional `String`: this is what the encoder does with the nil the
    /// customer app's `ifBlank { null }` would produce, and it is the 400 that shape would have earned.
    func testANilPhoneWouldBeDroppedFromTheBodyEntirely() throws {
        let data = try JSONEncoder().encode(
            UpdateCurrentUserCommand(firstName: "Ondrej", lastName: "Novak", phoneNumber: nil)
        )

        let body = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertFalse(body.keys.contains("phoneNumber"))
    }

    private static let updatePath = "/api/User/UpdateCurrentUser"

    private static let profileBody = profileBody(phoneNumber: nil)

    private static func profileBody(phoneNumber: String?) -> Data {
        let phone = phoneNumber.map { #""phoneNumber":"\#($0)","# } ?? ""
        return Data("""
        {"email":"ondrej@example.com","firstName":"Ondrej","lastName":"Novak",\(phone)
         "birthDate":"1982-09-04","preferredLanguageCode":"en"}
        """.utf8)
    }

    private func push(profile: Data? = nil, languageCode: String = "uk") async {
        if let profile {
            GeneratedWireSpine.install(recording: bodies) { request in
                request.url?.path == "/api/User/GetCurrent"
                    ? (200, profile)
                    : (200, Data(#"{"id":"user-1"}"#.utf8))
            }
        }
        await LiveLanguagePreferenceSync(
            tokenStore: SessionTokenStore(signedIn: true),
            client: LivePartnerUserClient()
        ).push(languageCode: languageCode)
    }
}
