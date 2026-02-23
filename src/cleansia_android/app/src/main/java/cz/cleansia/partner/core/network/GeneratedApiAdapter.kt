package cz.cleansia.partner.core.network

/**
 * Adapter to integrate the generated OpenAPI client with the existing app architecture.
 *
 * After running `./gradlew updateApiClient`, the generated API interfaces and models
 * will be available in the `cz.cleansia.partner.api.generated` package.
 *
 * ## Usage
 *
 * 1. Run your backend locally: `dotnet run` in the Cleansia.App project
 * 2. Download and generate the API client: `./gradlew updateApiClient`
 * 3. The generated code will be in `build/generated/openapi/src/main/kotlin/`
 *
 * ## Generated Structure
 *
 * - `cz.cleansia.partner.api.generated.api.*` - Retrofit API interfaces
 * - `cz.cleansia.partner.api.generated.models.*` - Data models matching backend DTOs
 *
 * ## Integration
 *
 * Once generated, you can:
 * 1. Use generated models directly in your repositories
 * 2. Create type aliases if needed for backward compatibility
 * 3. Replace manual ApiService with generated interfaces
 *
 * Example:
 * ```kotlin
 * // In your Hilt module:
 * @Provides
 * fun provideAuthApi(retrofit: Retrofit): AuthApi {
 *     return retrofit.create(AuthApi::class.java)
 * }
 * ```
 */
object GeneratedApiAdapter {

    /**
     * Converts a generated API response to our internal ApiResult type.
     * Use this when integrating generated API calls with existing repository pattern.
     */
    inline fun <T> toApiResult(
        response: retrofit2.Response<T>
    ): ApiResult<T> {
        return if (response.isSuccessful) {
            val body = response.body()
            if (body != null) {
                ApiResult.Success(body)
            } else {
                ApiResult.Error(ApiError.Unknown("Empty response body"))
            }
        } else {
            val error = when (response.code()) {
                401 -> ApiError.Unauthorized
                404 -> ApiError.NotFound(response.message())
                in 400..499 -> ApiError.BadRequest(response.message())
                in 500..599 -> ApiError.Server(response.code(), response.message())
                else -> ApiError.Unknown("HTTP ${response.code()}: ${response.message()}")
            }
            ApiResult.Error(error)
        }
    }
}
