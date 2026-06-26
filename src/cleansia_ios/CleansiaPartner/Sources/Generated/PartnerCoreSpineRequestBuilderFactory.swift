import CleansiaCore
import CleansiaPartnerApi
import Foundation

enum PartnerGeneratedAuth {
    private static let lock = NSLock()
    nonisolated(unsafe) private static var installedBridge: GeneratedClientAuthBridge?

    static var bridge: GeneratedClientAuthBridge? {
        lock.lock()
        defer { lock.unlock() }
        return installedBridge
    }

    static func install(bridge: GeneratedClientAuthBridge, basePath: String) {
        lock.lock()
        installedBridge = bridge
        lock.unlock()
        CleansiaPartnerApiAPI.basePath = basePath
        CleansiaPartnerApiAPI.requestBuilderFactory = PartnerCoreSpineRequestBuilderFactory()
    }
}

private func unauthorizedStatus(of error: Error) -> Int? {
    guard case let .error(code, _, _, _) = error as? ErrorResponse else { return nil }
    return code
}

final class PartnerCoreSpineRequestBuilderFactory: RequestBuilderFactory {
    func getNonDecodableBuilder<T>() -> RequestBuilder<T>.Type {
        PartnerCoreSpineRequestBuilder<T>.self
    }

    func getBuilder<T: Decodable>() -> RequestBuilder<T>.Type {
        PartnerCoreSpineDecodableRequestBuilder<T>.self
    }
}

final class PartnerCoreSpineRequestBuilder<T>: URLSessionRequestBuilder<T> {
    override func createURLSession() -> URLSessionProtocol {
        PartnerGeneratedAuth.bridge?.session ?? super.createURLSession()
    }

    override func createURLRequest(
        urlSession: URLSessionProtocol,
        method: HTTPMethod,
        encoding: ParameterEncoding,
        headers: [String: String]
    ) throws -> URLRequest {
        var request = try super.createURLRequest(urlSession: urlSession, method: method, encoding: encoding, headers: headers)
        PartnerGeneratedAuth.bridge?.authorize(&request)
        return request
    }

    override func execute() async throws -> Response<T> {
        guard let bridge = PartnerGeneratedAuth.bridge else { return try await super.execute() }
        return try await bridge.executeWithRetry(
            attempt: { try await super.execute() },
            unauthorizedStatus: unauthorizedStatus(of:)
        )
    }
}

final class PartnerCoreSpineDecodableRequestBuilder<T: Decodable>: URLSessionDecodableRequestBuilder<T> {
    override func createURLSession() -> URLSessionProtocol {
        PartnerGeneratedAuth.bridge?.session ?? super.createURLSession()
    }

    override func createURLRequest(
        urlSession: URLSessionProtocol,
        method: HTTPMethod,
        encoding: ParameterEncoding,
        headers: [String: String]
    ) throws -> URLRequest {
        var request = try super.createURLRequest(urlSession: urlSession, method: method, encoding: encoding, headers: headers)
        PartnerGeneratedAuth.bridge?.authorize(&request)
        return request
    }

    override func execute() async throws -> Response<T> {
        guard let bridge = PartnerGeneratedAuth.bridge else { return try await super.execute() }
        return try await bridge.executeWithRetry(
            attempt: { try await super.execute() },
            unauthorizedStatus: unauthorizedStatus(of:)
        )
    }
}
