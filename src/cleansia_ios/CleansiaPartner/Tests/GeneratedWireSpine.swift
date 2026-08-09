import CleansiaCore
import CleansiaPartnerApi
import Foundation
@testable import CleansiaPartner

/// Records the requests the generated APIs actually hand to `URLSession` through the installed Core
/// spine — a body read here is the bytes on the wire, not a re-encoding of the command object, which
/// is the only way to tell an omitted member from a null one.
final class WireBodies: @unchecked Sendable {
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

@MainActor
enum GeneratedWireSpine {
    static let basePath = "https://api.test"

    static func install(recording bodies: WireBodies, answer: @escaping (URLRequest) -> (Int, Data)) {
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
            basePath: basePath
        )
        CleansiaPartnerApiAPI.apiResponseQueue = DispatchQueue(label: "cz.cleansia.wire.test")
        GenMockURLProtocol.handler = { request in
            bodies.record(request)
            return answer(request)
        }
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
