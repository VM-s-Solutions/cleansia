package cz.cleansia.core.network

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * [networkCall] is a shared primitive: both apps and every repository in them cross it, so a change
 * to which throwables it swallows is a change to how every one of them reports failure. It answers
 * `null` for a transport failure and the caller turns that into its own localized
 * [ApiError.Network] — which makes the set of throwables it folds into `null` the whole contract.
 *
 * Two throwables are exempt and each for a different reason: cancellation must propagate or
 * structured concurrency breaks, and a [WireContractViolation] is not a transport failure at all.
 * Folding the violation in was how the field name got lost and how a 200 with a broken body came to
 * be reported as "check your internet connection".
 */
class NetworkCallTest {

    @Test
    fun givenASuccessfulCall_returnsTheValue() = runTest {
        assertEquals("payload", networkCall { "payload" })
    }

    @Test
    fun givenATransportFailure_returnsNullForTheCallerToLocalize() = runTest {
        assertNull(networkCall<String> { throw java.io.IOException("boom") })
    }

    @Test
    fun givenAnUnexpectedThrowable_stillReturnsNull() = runTest {
        assertNull(networkCall<String> { throw IllegalStateException("bug") })
    }

    /**
     * Catching this is what produced the spurious "check your internet connection" toast on
     * Home/Profile after a fast tab switch.
     */
    @Test
    fun givenCancellation_rethrowsSoStructuredConcurrencyStillWorks() = runTest {
        var caught: CancellationException? = null

        try {
            networkCall<String> { throw CancellationException("nav away") }
        } catch (ce: CancellationException) {
            caught = ce
        }

        assertNotNull("cancellation must not be folded into null", caught)
    }

    /**
     * The arm this test exists for. A total mapper raises its refusal at the call — the customer's
     * adapters map inside the Retrofit response — so `networkCall` is where it would be swallowed,
     * and `null` here means "the network failed", which is the one thing that did not.
     */
    @Test
    fun givenAWireContractViolation_rethrowsItRatherThanFoldingItIntoNull() = runTest {
        val violation = WireContractViolation("totalPay")
        var caught: Throwable? = null

        try {
            networkCall<String> { throw violation }
        } catch (t: Throwable) {
            caught = t
        }

        assertSame("the violation must reach the caller as itself", violation, caught)
    }

    @Test
    fun givenAWireContractViolation_keepsTheFieldNameOnTheWayOut() = runTest {
        val caught = runCatching { networkCall<String> { throw WireContractViolation("grandTotal") } }
            .exceptionOrNull()

        assertTrue("expected WireContractViolation but was $caught", caught is WireContractViolation)
        assertTrue(caught!!.message!!.startsWith("grandTotal "))
    }

    /**
     * The composition production actually runs: a repository wraps its body in [wireResult] and reads
     * `networkCall(...) ?: networkError()`. If the violation is folded into `null` it takes the
     * `networkError()` branch, and a broken contract is reported through the silent channel with the
     * field name gone. This asserts the two primitives compose to the opposite of that.
     */
    @Test
    fun composedWithWireResult_aViolationIsAServerFaultAndNeverANetworkOne() = runTest {
        val result = wireResult<String> {
            val value = networkCall<String> { throw WireContractViolation("setupIntentClientSecret") }
                ?: return@wireResult ApiResult.Error(ApiError.Network("check your connection"))
            ApiResult.Success(value)
        }

        val error = (result as ApiResult.Error).error
        assertTrue("a contract violation must never surface as Network, but was $error", error is ApiError.Server)
        assertEquals(200, (error as ApiError.Server).statusCode)
        assertTrue(error.diagnostic!!.startsWith("setupIntentClientSecret "))
        assertFalse(error.getUserMessage().contains("setupIntentClientSecret"))
    }

    @Test
    fun composedWithWireResult_arealTransportFailureStillTakesTheNetworkBranch() = runTest {
        val result = wireResult<String> {
            val value = networkCall<String> { throw java.io.IOException("boom") }
                ?: return@wireResult ApiResult.Error(ApiError.Network("check your connection"))
            ApiResult.Success(value)
        }

        assertTrue((result as ApiResult.Error).error is ApiError.Network)
    }
}
