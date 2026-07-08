import Foundation

public func apiResult<T>(
    mapError: (Error) -> ApiError = ApiError.from,
    _ body: () async throws -> T
) async -> ApiResult<T> {
    do {
        return try await .success(body())
    } catch is CancellationError {
        return .failure(ApiError(code: ApiError.cancelledCode))
    } catch {
        return .failure(mapError(error))
    }
}

public extension ApiError {
    static func from(_ error: Error) -> ApiError {
        if isCancellation(error) {
            return ApiError(code: cancelledCode)
        }
        if let urlError = error as? URLError {
            return ApiError(code: "network.unreachable", message: urlError.localizedDescription)
        }
        return ApiError(code: "network.unknown", message: error.localizedDescription)
    }
}
