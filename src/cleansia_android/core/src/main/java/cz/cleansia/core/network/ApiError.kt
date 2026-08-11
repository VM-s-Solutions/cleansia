package cz.cleansia.core.network

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.JsonElement

/**
 * Wire shape covering both ASP.NET `ProblemDetails` (`detail`/`type`/`status`)
 * and the bespoke `{message,code,errors}` shape some endpoints still return.
 * `errors` is a [JsonElement] so we can lazily parse either
 * `Map<String,String>` or `Map<String,List<String>>` at the call site.
 */
@Serializable
data class ApiErrorResponse(
    val message: String? = null,
    val code: String? = null,
    val title: String? = null,
    val errors: JsonElement? = null,
    val detail: String? = null,
    val type: String? = null,
    val status: Int? = null,
) {
    val effectiveMessage: String?
        get() = detail ?: message ?: title
}

sealed class ApiError : Exception() {

    data class Network(override val message: String) : ApiError()

    /**
     * [message] is rendered. [diagnostic] is not, ever.
     *
     * A wire-contract violation is a 200 whose body broke the contract, and the sentence that
     * identifies it — *"totalPrice is null but the mobile API contract declares it non-nullable"* —
     * is triage, not copy: a customer cannot act on it, it reads as a crash, and it puts an internal
     * field name on their screen. That is worse than the coercion the refusal replaced, so the two
     * facts live in two fields and only one of them can reach [getUserMessage].
     *
     * Same reasoning [AuthRejected] already applies to the raw backend key, and built the same way:
     * the arm that carries something unrenderable keeps a renderable [message] beside it.
     *
     * [diagnostic] is null for an ordinary HTTP 5xx, whose [message] is the server's own line and is
     * meant to be shown. Use [wireViolationError] rather than filling this in by hand.
     */
    data class Server(
        val statusCode: Int,
        override val message: String,
        val diagnostic: String? = null,
    ) : ApiError()

    data object Unauthorized : ApiError() {
        private fun readResolve(): Any = Unauthorized
        override val message: String = "Session expired. Please login again."
    }

    /**
     * 401 the *controller* authored: the auth command itself was rejected for a
     * stated business reason (signed up with a different provider, no employee
     * record, …) and `errorKey` carries the backend translation key.
     *
     * Deliberately NOT a widened [Unauthorized]: that one means "the session
     * layer rejected you" and is what session-teardown reasoning is allowed to
     * key off. A login failure that looked identical to an expired session
     * would sign users out at the wrong moment.
     *
     * [message] mirrors [Unauthorized] instead of carrying the server text
     * because on this arm the server's `detail` *is* the raw key — piping it
     * through would print `auth.internal_type_error` at the ~30 call sites that
     * use [getUserMessage] with no localizer. The key is resolved by the
     * app-local localizers instead.
     */
    data class AuthRejected(
        val errorKey: String,
        override val message: String = Unauthorized.message,
    ) : ApiError()

    data class NotFound(override val message: String = "Resource not found") : ApiError()

    /**
     * 400 with optional structured validation errors. `errorKey` is the first
     * server-supplied translation key (e.g. `user.not_existing_email`) that the
     * app-local error localizer can map to a localized string at the UI layer.
     */
    data class BadRequest(
        override val message: String,
        val code: String? = null,
        val validationErrors: Map<String, List<String>>? = null,
        val errorKey: String? = null,
    ) : ApiError()

    data class Unknown(override val message: String = "An unexpected error occurred") : ApiError()

    /**
     * The render path. Every arm's [message] is copy a person can act on — which is an invariant of
     * the arms, not of this function: nothing unrenderable may be put in [message] in the first
     * place. [Server.diagnostic] is deliberately unreachable from here.
     *
     * Apps may localize on top (partner's `ApiErrorTranslator` maps each arm onto its own string);
     * this is the shared floor for the call sites that render an [ApiError] directly.
     */
    fun getUserMessage(): String = message ?: GENERIC_USER_MESSAGE

    companion object {
        internal const val GENERIC_USER_MESSAGE = "An unexpected error occurred"

        /** Matches both apps' own `error_server` copy, for the call sites that do not localize. */
        const val SERVER_USER_MESSAGE = "Server error. Please try again later."
    }
}
