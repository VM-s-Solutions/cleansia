import Foundation

enum JwtFactory {
    static func make(exp: Int) -> String {
        let header = base64URL(Data(#"{"alg":"HS256","typ":"JWT"}"#.utf8))
        let payload = base64URL(Data(#"{"exp":\#(exp)}"#.utf8))
        return "\(header).\(payload).sig"
    }

    private static func base64URL(_ data: Data) -> String {
        data.base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }
}

final class RequestRecorder: @unchecked Sendable {
    private let lock = NSLock()
    private var requests: [URLRequest] = []

    func record(_ request: URLRequest) {
        lock.lock()
        requests.append(request)
        lock.unlock()
    }

    func last(matching pathFragment: String) -> URLRequest? {
        lock.lock()
        defer { lock.unlock() }
        return requests.last { $0.url?.path.contains(pathFragment) == true }
    }

    func reset() {
        lock.lock()
        requests.removeAll()
        lock.unlock()
    }
}

final class MockURLProtocol: URLProtocol {
    static var handler: ((URLRequest) -> (Int, Data))?
    static let recorder = RequestRecorder()

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        MockURLProtocol.recorder.record(request)
        guard let handler = MockURLProtocol.handler, let url = request.url else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        let (status, data) = handler(request)
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
}
