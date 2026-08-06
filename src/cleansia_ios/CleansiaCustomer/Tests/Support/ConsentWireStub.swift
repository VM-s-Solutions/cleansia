import CleansiaCustomerApi
import Foundation

/// The whole signup-consent flow's wire: the auth endpoints the spine calls and the Gdpr
/// endpoints the generated client calls, so a test can assert the commands that actually left.
final class ConsentWireStub: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var grants: [GrantConsentCommand] = []

    nonisolated(unsafe) static var sessionEmail = "ada@example.com"
    nonisolated(unsafe) static var registerResponse: () -> (Int, Data) = { (200, Data("true".utf8)) }
    nonisolated(unsafe) static var consentsResponse: () -> (Int, Data) = { (200, Data("[]".utf8)) }
    nonisolated(unsafe) static var grantResponse: () -> (Int, Data) = { (200, Data()) }

    static var grantedTypes: [ConsentType] {
        lock.withLock { grants.compactMap(\.consentType) }
    }

    static func reset() {
        lock.withLock { grants.removeAll() }
        sessionEmail = "ada@example.com"
        registerResponse = { (200, Data("true".utf8)) }
        consentsResponse = { (200, Data("[]".utf8)) }
        grantResponse = { (200, Data()) }
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badURL))
            return
        }
        let (status, data) = ConsentWireStub.answer(for: request)
        guard let response = HTTPURLResponse(
            url: url,
            statusCode: status,
            httpVersion: nil,
            headerFields: ["Content-Type": "application/json"]
        ) else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    private static func answer(for request: URLRequest) -> (Int, Data) {
        let path = request.url?.path ?? ""
        // GrantConsent needs an authenticated caller — the whole reason the tick is parked
        // rather than granted at signup — so an unauthenticated Gdpr call is refused here too.
        if path.contains("/Gdpr/"), request.value(forHTTPHeaderField: "Authorization") == nil {
            return (401, Data(#"{"title":"Unauthorized"}"#.utf8))
        }
        switch (path, request.httpMethod ?? "") {
        case ("/api/v1/Gdpr/consents", "POST"):
            if let body = body(of: request),
               let command = try? JSONDecoder().decode(GrantConsentCommand.self, from: body)
            {
                lock.withLock { grants.append(command) }
            }
            return grantResponse()
        case ("/api/v1/Gdpr/consents", _):
            return consentsResponse()
        case ("/api/Auth/Login", _):
            return (200, Data("""
            {"token":"header.payload.signature","isEmailConfirmed":true,"userId":"u-1",
             "email":"\(sessionEmail)","refreshToken":"r-1","refreshTokenExpiresAt":"2099-01-01T00:00:00Z"}
            """.utf8))
        default:
            return registerResponse()
        }
    }

    private static func body(of request: URLRequest) -> Data? {
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
