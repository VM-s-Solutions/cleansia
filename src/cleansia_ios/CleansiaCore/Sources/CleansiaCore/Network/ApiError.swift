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
