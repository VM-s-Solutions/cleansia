import Foundation

public struct ApiError: Error, Equatable {
    public let code: String?
    public let message: String?
    public let httpStatus: Int?

    public init(code: String? = nil, message: String? = nil, httpStatus: Int? = nil) {
        self.code = code
        self.message = message
        self.httpStatus = httpStatus
    }
}

public extension ApiError {
    /// Marker for a superseded/cancelled request (tab switch, pull-to-refresh
    /// replacing a prior load, sheet dismissal). Android's coroutine
    /// cancellation never reaches the snackbar; `showApiError` drops this code.
    static let cancelledCode = "network.cancelled"

    var isCancellation: Bool {
        code == Self.cancelledCode
    }

    /// True for any cancellation-class transport error however it arrives:
    /// Swift structured-concurrency `CancellationError`, `URLError.cancelled`,
    /// or the bridged `NSURLErrorCancelled` (-999) the generated client wraps
    /// inside its `ErrorResponse`.
    static func isCancellation(_ error: Error) -> Bool {
        if error is CancellationError { return true }
        let nsError = error as NSError
        return nsError.domain == NSURLErrorDomain && nsError.code == NSURLErrorCancelled
    }
}

public extension ApiError {
    /// Android `ApiErrorParser` parity: extract the business error key from a
    /// ProblemDetails body so `ApiErrorLocalizer` can catalog-match it; the
    /// raw body text is only a last-resort message.
    static func fromProblemDetails(httpStatus: Int, body: Data?, fallbackMessage: String? = nil) -> ApiError {
        let problem = body.flatMap { try? JSONDecoder().decode(ProblemDetails.self, from: $0) }
        let raw = body.flatMap { String(data: $0, encoding: .utf8) }
        return ApiError(
            code: problem?.firstErrorKey ?? problem?.errorCode ?? problem?.type,
            message: problem?.detail ?? problem?.title ?? raw ?? fallbackMessage,
            httpStatus: httpStatus
        )
    }
}
