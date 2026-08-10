package cz.cleansia.partner.data

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult

/**
 * No schema in the partner mobile spec declares a `required` array, so the generator types every
 * property optional-with-null regardless of `nullable: false` and the contract lands in the
 * repository's mapper. A null in a field the spec marks non-nullable is a renamed or broken wire
 * field, never a zero — "0 Kč" to a cleaner who earned 4,800 is worse than an error screen.
 */
internal class WireContractViolation(field: String) :
    IllegalStateException("$field is null but the mobile API contract declares it non-nullable")

internal fun <T : Any> T?.required(field: String): T = this ?: throw WireContractViolation(field)

/**
 * Runs a [toDomain] mapper over a successful response, turning a contract violation into
 * [ApiError.Server] — a 200 whose body breaks the contract is a server fault, and Server renders as
 * the generic line, so the offending field name reaches triage rather than the cleaner.
 */
internal fun <T, R> ApiResult<T>.mapWire(transform: (T) -> R): ApiResult<R> = when (this) {
    is ApiResult.Error -> this
    is ApiResult.Success -> runCatching { transform(data) }.fold(
        onSuccess = { ApiResult.Success(it) },
        onFailure = { ApiResult.Error(ApiError.Server(HTTP_OK, it.message.orEmpty())) },
    )
}

private const val HTTP_OK = 200
