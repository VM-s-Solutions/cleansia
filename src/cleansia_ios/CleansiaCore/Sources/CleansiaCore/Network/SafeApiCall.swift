import Foundation

/// The one place a thrown failure becomes an `ApiResult`. `endpoint` defaults to the enclosing client
/// method, so a wire refusal names where it happened without any call site being asked to supply it.
public func apiResult<T>(
    mapError: (Error) -> ApiError = ApiError.from,
    endpoint: String = #function,
    _ body: () async throws -> T
) async -> ApiResult<T> {
    do {
        return try await .success(body())
    } catch is CancellationError {
        return .failure(ApiError(code: ApiError.cancelledCode))
    } catch let violation as WireContractViolation {
        return .failure(ApiError(violation, endpoint: endpoint))
    } catch let refusal as ApiError {
        // A client that already decided the error keeps it; `mapError` would rewrite it to
        // `network.unknown` and blame the transport for a refusal the transport never made.
        return .failure(refusal)
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
