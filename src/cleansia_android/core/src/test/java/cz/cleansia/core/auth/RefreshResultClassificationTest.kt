package cz.cleansia.core.auth

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The cross-platform refresh-failure classification rule (kept in lockstep with
 * the iOS SessionRefresher): 401/403 or a parseable business rejection is
 * TERMINAL; 5xx/429/unknown is RETRYABLE.
 */
class RefreshResultClassificationTest {

    @Test
    fun http401_isRejected() =
        assertEquals(RefreshResult.Rejected, RefreshResult.classifyHttpFailure(401, null))

    @Test
    fun http403_isRejected() =
        assertEquals(RefreshResult.Rejected, RefreshResult.classifyHttpFailure(403, null))

    @Test
    fun http429_isUnavailable() =
        assertEquals(RefreshResult.Unavailable, RefreshResult.classifyHttpFailure(429, null))

    @Test
    fun http500_isUnavailable() =
        assertEquals(RefreshResult.Unavailable, RefreshResult.classifyHttpFailure(500, null))

    @Test
    fun http503_isUnavailable() =
        assertEquals(RefreshResult.Unavailable, RefreshResult.classifyHttpFailure(503, null))

    @Test
    fun invalidRefreshTokenBusinessKey_isRejectedRegardlessOfStatus() =
        assertEquals(
            RefreshResult.Rejected,
            RefreshResult.classifyHttpFailure(
                400,
                """{"code":"Token","message":"auth.invalid_refresh_token"}""",
            ),
        )

    @Test
    fun refreshTokenReusedBusinessKey_isRejectedRegardlessOfStatus() =
        assertEquals(
            RefreshResult.Rejected,
            RefreshResult.classifyHttpFailure(
                400,
                """{"code":"Token","message":"auth.refresh_token_reused"}""",
            ),
        )

    @Test
    fun http400WithUnrelatedBody_isUnavailable() =
        assertEquals(
            RefreshResult.Unavailable,
            RefreshResult.classifyHttpFailure(400, """{"title":"Bad Request"}"""),
        )

    @Test
    fun unknownStatusWithoutBody_isUnavailable() =
        assertEquals(RefreshResult.Unavailable, RefreshResult.classifyHttpFailure(418, null))
}
