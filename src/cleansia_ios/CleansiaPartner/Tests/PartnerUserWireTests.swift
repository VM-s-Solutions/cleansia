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
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [GenMockURLProtocol.self]
        PartnerGeneratedAuth.install(
            bridge: GeneratedClientAuthBridge(
                headerAdapter: HeaderAdapter(deviceIdProvider: WireDeviceId()),
                tokenStore: SessionTokenStore(signedIn: true),
                sessionRefresher: SessionRefresher(
                    tokenStore: SessionTokenStore(signedIn: true),
                    refreshClient: NeverRefreshing(),
                    sessionManager: SessionManager(),
                    sessionScopedCaches: SessionScopedCacheRegistry()
                ),
                session: URLSession(configuration: config)
            ),
            basePath: "https://api.test"
        )
        CleansiaPartnerApiAPI.apiResponseQueue = DispatchQueue(label: "cz.cleansia.wire.test")
        GenMockURLProtocol.handler = { [bodies] request in
            bodies?.record(request)
            return request.url?.path == "/api/User/GetCurrent"
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
        _ = await LivePartnerUserClient().updateCurrentUser(
            firstName: "Ondrej",
            lastName: "Novak",
            phoneNumber: "",
            birthDate: Date(timeIntervalSince1970: 400_030_200),
            languageCode: "uk"
        )

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
            GenMockURLProtocol.handler = { [bodies] request in
                bodies?.record(request)
                return request.url?.path == "/api/User/GetCurrent"
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

private final class WireBodies: @unchecked Sendable {
    private let lock = NSLock()
    private var recorded: [(path: String, method: String, body: Data?)] = []

    func record(_ request: URLRequest) {
        lock.withLock {
            recorded.append((
                path: request.url?.path ?? "",
                method: request.httpMethod ?? "",
                body: Self.readBody(from: request)
            ))
        }
    }

    var paths: [String] {
        lock.withLock { recorded.map(\.path) }
    }

    func method(ofPath path: String) -> String? {
        lock.withLock { recorded.last { $0.path == path }?.method }
    }

    func text(ofPath path: String) -> String? {
        data(ofPath: path).flatMap { String(data: $0, encoding: .utf8) }
    }

    func json(ofPath path: String) -> [String: Any]? {
        data(ofPath: path).flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] }
    }

    private func data(ofPath path: String) -> Data? {
        lock.withLock { recorded.last { $0.path == path }?.body }
    }

    /// `URLSession` moves an uploaded body onto `httpBodyStream`, so reading `httpBody` alone reports
    /// every request as empty.
    private static func readBody(from request: URLRequest) -> Data? {
        if let httpBody = request.httpBody { return httpBody }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let size = 4096
        var buffer = [UInt8](repeating: 0, count: size)
        while stream.hasBytesAvailable {
            let read = stream.read(&buffer, maxLength: size)
            if read <= 0 { break }
            data.append(buffer, count: read)
        }
        return data
    }
}

private struct WireDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-wire"
    }
}

private struct NeverRefreshing: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}
