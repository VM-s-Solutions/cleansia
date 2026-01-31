package cz.cleansia.partner.core.network

/**
 * A wrapper class that represents the result of an API operation.
 * Can be either Success with data or Error with an ApiError.
 */
sealed class ApiResult<out T> {

    /**
     * Represents a successful result with data
     */
    data class Success<T>(val data: T) : ApiResult<T>()

    /**
     * Represents a failed result with an error
     */
    data class Error(val error: ApiError) : ApiResult<Nothing>()

    /**
     * Returns true if this is a Success result
     */
    val isSuccess: Boolean get() = this is Success

    /**
     * Returns true if this is an Error result
     */
    val isError: Boolean get() = this is Error

    /**
     * Returns the data if this is a Success, or null if it's an Error
     */
    fun getOrNull(): T? = when (this) {
        is Success -> data
        is Error -> null
    }

    /**
     * Returns the error if this is an Error, or null if it's a Success
     */
    fun errorOrNull(): ApiError? = when (this) {
        is Success -> null
        is Error -> error
    }

    /**
     * Returns the data if this is a Success, or throws the error if it's an Error
     */
    fun getOrThrow(): T = when (this) {
        is Success -> data
        is Error -> throw error
    }

    /**
     * Returns the data if this is a Success, or the provided default value if it's an Error
     */
    fun getOrDefault(default: @UnsafeVariance T): T = when (this) {
        is Success -> data
        is Error -> default
    }

    /**
     * Maps the success data to a new type
     */
    inline fun <R> map(transform: (T) -> R): ApiResult<R> = when (this) {
        is Success -> Success(transform(data))
        is Error -> this
    }

    /**
     * Performs the given action on the encapsulated value if this is a Success
     */
    inline fun onSuccess(action: (T) -> Unit): ApiResult<T> {
        if (this is Success) action(data)
        return this
    }

    /**
     * Performs the given action on the encapsulated error if this is an Error
     */
    inline fun onError(action: (ApiError) -> Unit): ApiResult<T> {
        if (this is Error) action(error)
        return this
    }

    companion object {
        /**
         * Creates a Success result with the given data
         */
        fun <T> success(data: T): ApiResult<T> = Success(data)

        /**
         * Creates an Error result with the given error
         */
        fun error(error: ApiError): ApiResult<Nothing> = Error(error)
    }
}

/**
 * Extension function to convert a nullable value to ApiResult
 */
fun <T> T?.toApiResult(errorMessage: String = "Data not found"): ApiResult<T> =
    if (this != null) ApiResult.Success(this) else ApiResult.Error(ApiError.NotFound(errorMessage))
