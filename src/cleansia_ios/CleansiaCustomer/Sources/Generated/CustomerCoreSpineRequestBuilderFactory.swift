import CleansiaCore
import CleansiaCustomerApi
import Foundation

enum CustomerGeneratedAuth {
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
        CleansiaCustomerApiAPI.basePath = basePath
        CleansiaCustomerApiAPI.requestBuilderFactory = CustomerCoreSpineRequestBuilderFactory()
    }
}

private func unauthorizedStatus(of error: Error) -> Int? {
    guard case let .error(code, _, _, _) = error as? ErrorResponse else { return nil }
    return code
}

final class CustomerCoreSpineRequestBuilderFactory: RequestBuilderFactory {
    func getNonDecodableBuilder<T>() -> RequestBuilder<T>.Type {
        CustomerCoreSpineRequestBuilder<T>.self
    }

    func getBuilder<T: Decodable>() -> RequestBuilder<T>.Type {
        CustomerCoreSpineDecodableRequestBuilder<T>.self
    }
}

final class CustomerCoreSpineRequestBuilder<T>: URLSessionRequestBuilder<T> {
    override func createURLSession() -> URLSessionProtocol {
        CustomerGeneratedAuth.bridge?.session ?? super.createURLSession()
    }

    override func createURLRequest(
        urlSession: URLSessionProtocol,
        method: HTTPMethod,
        encoding: ParameterEncoding,
        headers: [String: String]
    ) throws -> URLRequest {
        var request = try super.createURLRequest(urlSession: urlSession, method: method, encoding: encoding, headers: headers)
        CustomerGeneratedAuth.bridge?.authorize(&request)
        return request
    }

    override func execute() async throws -> Response<T> {
        guard let bridge = CustomerGeneratedAuth.bridge else { return try await super.execute() }
        return try await bridge.executeWithRetry(
            attempt: { try await super.execute() },
            unauthorizedStatus: unauthorizedStatus(of:)
        )
    }
}

final class CustomerCoreSpineDecodableRequestBuilder<T: Decodable>: URLSessionDecodableRequestBuilder<T> {
    override func createURLSession() -> URLSessionProtocol {
        CustomerGeneratedAuth.bridge?.session ?? super.createURLSession()
    }

    override func createURLRequest(
        urlSession: URLSessionProtocol,
        method: HTTPMethod,
        encoding: ParameterEncoding,
        headers: [String: String]
    ) throws -> URLRequest {
        var request = try super.createURLRequest(urlSession: urlSession, method: method, encoding: encoding, headers: headers)
        CustomerGeneratedAuth.bridge?.authorize(&request)
        return request
    }

    override func execute() async throws -> Response<T> {
        guard let bridge = CustomerGeneratedAuth.bridge else { return try await super.execute() }
        return try await bridge.executeWithRetry(
            attempt: { try await super.execute() },
            unauthorizedStatus: unauthorizedStatus(of:)
        )
    }
}
