package cz.cleansia.partner.core.network

/**
 * Result of an API call — either success with data or error with [ApiError].
 * Used at the repository boundary; ViewModels translate to UI state.
 */
sealed class ApiResult<out T> {

    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val error: ApiError) : ApiResult<Nothing>()

    val isSuccess: Boolean get() = this is Success
    val isError: Boolean get() = this is Error

    fun getOrNull(): T? = (this as? Success)?.data
    fun errorOrNull(): ApiError? = (this as? Error)?.error

    inline fun <R> map(transform: (T) -> R): ApiResult<R> = when (this) {
        is Success -> Success(transform(data))
        is Error -> this
    }

    inline fun onSuccess(action: (T) -> Unit): ApiResult<T> {
        if (this is Success) action(data)
        return this
    }

    inline fun onError(action: (ApiError) -> Unit): ApiResult<T> {
        if (this is Error) action(error)
        return this
    }
}
