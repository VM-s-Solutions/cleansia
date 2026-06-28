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
    private var bodies: [String: Data] = [:]

    func record(_ request: URLRequest, body: Data?) {
        lock.lock()
        requests.append(request)
        if let body, let key = request.url?.path {
            bodies[key] = body
        }
        lock.unlock()
    }

    func last(matching pathFragment: String) -> URLRequest? {
        lock.lock()
        defer { lock.unlock() }
        return requests.last { $0.url?.path.contains(pathFragment) == true }
    }

    func body(of request: URLRequest) -> Data? {
        lock.lock()
        defer { lock.unlock() }
        guard let key = request.url?.path else { return nil }
        return bodies[key]
    }

    func reset() {
        lock.lock()
        requests.removeAll()
        bodies.removeAll()
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

    static func body(of request: URLRequest) -> Data? {
        recorder.body(of: request)
    }

    override func startLoading() {
        MockURLProtocol.recorder.record(request, body: MockURLProtocol.readBody(from: request))
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

    private static func readBody(from request: URLRequest) -> Data? {
        if let body = request.httpBody { return body }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let bufferSize = 4096
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: bufferSize)
        defer { buffer.deallocate() }
        while stream.hasBytesAvailable {
            let read = stream.read(buffer, maxLength: bufferSize)
            if read <= 0 { break }
            data.append(buffer, count: read)
        }
        return data
    }
}
