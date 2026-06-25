import Foundation

public typealias ApiResult<T> = Result<T, ApiError>

public extension Result where Failure == ApiError {
    var apiErrorOrNil: ApiError? {
        if case let .failure(error) = self { return error }
        return nil
    }
}
