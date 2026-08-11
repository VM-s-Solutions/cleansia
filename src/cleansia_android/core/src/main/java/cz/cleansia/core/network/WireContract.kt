package cz.cleansia.core.network

import retrofit2.Response

/**
 * Neither mobile spec declares a `required` array, so the generator types every property
 * optional-with-null regardless of `nullable: false` and the contract lands in the repository's
 * mapper. A null in a field the spec marks non-nullable is a renamed or broken wire field, never a
 * zero — "0 Kč" to a cleaner who earned 4,800 is worse than an error screen.
 */
class WireContractViolation(field: String) :
    IllegalStateException("$field is null but the mobile API contract declares it non-nullable")

fun <T : Any> T?.required(field: String): T = this ?: throw WireContractViolation(field)

/**
 * Runs a total mapper over a successful response, turning a contract violation into
 * [ApiError.Server] — a 200 whose body breaks the contract is a server fault, and Server is the arm
 * that says *the server answered and the answer was wrong* rather than blaming the connection.
 *
 * The field name is **carried, not delivered**, and [ApiError.Server.diagnostic] is where it is
 * carried — never `message`, which is what gets rendered. Partner initialises no Sentry
 * (`AndroidManifest.xml` removes `SentryInitProvider`, and `core/build.gradle.kts` says the deps are
 * inert there), so nothing records it yet. This is the only idiom that keeps the name at all, and a
 * name kept can be routed the day a sink exists; a name lost cannot. Q-OBS-01 decides when that day
 * is.
 *
 * Catches [WireContractViolation] alone: a genuine bug in the mapper is not a wire violation and
 * reporting it as one loses the stack that would have named it.
 */
fun <T, R> ApiResult<T>.mapWire(transform: (T) -> R): ApiResult<R> = when (this) {
    is ApiResult.Error -> this
    is ApiResult.Success -> try {
        ApiResult.Success(transform(data))
    } catch (violation: WireContractViolation) {
        ApiResult.Error(wireViolationError(violation))
    }
}

/**
 * The [Response] counterpart, for the adapters that map inside a Retrofit response and leave the
 * [ApiResult] to the repository. The transform is total — `(T?) -> R`, never `-> R?` — because a
 * mapped-to-null body is indistinguishable from a body the server never sent, and that ambiguity is
 * how the offending field name was being lost.
 *
 * The violation deliberately propagates instead of being flattened here: the repository owns the
 * `ApiResult`, so [wireResult] is where it becomes one.
 */
inline fun <T, R : Any> Response<T>.mapWire(transform: (T?) -> R): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

/**
 * Catches the refusal a [Response]-shaped adapter raises at the call. [networkCall] cannot: it
 * answers `null` for every throwable and the repository reads that as a network failure — the one
 * thing that did not fail — which is why it rethrows [WireContractViolation] for this to see.
 *
 * One wrapper, so the transport is decided once rather than once per call site.
 */
inline fun <T> wireResult(block: () -> ApiResult<T>): ApiResult<T> = try {
    block()
} catch (violation: WireContractViolation) {
    ApiResult.Error(wireViolationError(violation))
}

/**
 * The one place a [WireContractViolation] becomes an [ApiError], so the split between what is shown
 * and what is carried is made once rather than at each of the three catch sites.
 *
 * The offending field name goes to [ApiError.Server.diagnostic] and never to `message`: it is the
 * whole point of the idiom that the name survives, and it is equally the point that a customer never
 * reads it. `ApiError.Server(200, …)` still says *the server answered and the answer was wrong*.
 */
@PublishedApi
internal fun wireViolationError(violation: WireContractViolation): ApiError.Server = ApiError.Server(
    statusCode = HTTP_OK,
    message = ApiError.SERVER_USER_MESSAGE,
    diagnostic = violation.message,
)

/**
 * The body of a 2xx a total mapper produced. Retrofit types it nullable and a total mapper cannot
 * return null, so the `?:` every call site used to carry was reachable only by a bodiless 2xx —
 * where the old fallbacks reported `Success` with no rows or blamed the network. Refuses instead,
 * through the same [wireResult] the mapper's own refusals travel.
 */
fun <R : Any> Response<R>.requiredBody(): R = body().required("body of ${raw().request.url.encodedPath}")

@PublishedApi
internal const val HTTP_OK = 200
