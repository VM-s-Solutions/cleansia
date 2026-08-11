package cz.cleansia.core.network

import okhttp3.Protocol
import okhttp3.Request
import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

/**
 * The one refusal transport both apps share. It exists to carry the offending field name across a
 * boundary that the alternatives lost it at, so every assertion here is about the name surviving —
 * not merely about the call failing.
 */
class WireContractTest {

    private fun rawSuccess(path: String) = okhttp3.Response.Builder()
        .code(200)
        .message("OK")
        .protocol(Protocol.HTTP_1_1)
        .request(Request.Builder().url("https://mobile.cleansia.cz$path").build())
        .build()

    @Test
    fun requiredNamesTheFieldItRefused() {
        val violation = assertThrows(WireContractViolation::class.java) {
            null.required("totalPay")
        }

        assertTrue(violation.message!!.startsWith("totalPay "))
    }

    @Test
    fun requiredPassesARealValueThrough() {
        assertEquals(0.0, 0.0.required("totalPay"), 0.0)
    }

    @Test
    fun mapWireTurnsAViolationIntoAServerErrorCarryingTheField() {
        val result = ApiResult.Success("payload").mapWire<String, String> {
            null.required("grandTotal")
        }

        val error = (result as ApiResult.Error).error
        assertTrue("expected Server but was $error", error is ApiError.Server)
        assertEquals(200, (error as ApiError.Server).statusCode)
        assertTrue(error.message.startsWith("grandTotal "))
    }

    /**
     * Catching `Throwable` here reported a bug in the mapper as a wire violation, which sent triage
     * at the server for a defect on the client.
     */
    @Test
    fun mapWireLetsAMapperBugCrashRatherThanBlamingTheWire() {
        assertThrows(IllegalArgumentException::class.java) {
            ApiResult.Success("payload").mapWire<String, String> { throw IllegalArgumentException("bug") }
        }
    }

    @Test
    fun mapWirePassesAnExistingErrorThroughUntouched() {
        val original = ApiResult.Error(ApiError.Unauthorized)

        assertSame(original, original.mapWire { it })
    }

    @Test
    fun wireResultCatchesAViolationRaisedInsideTheBlock() {
        val result = wireResult<String> { throw WireContractViolation("refundAmount") }

        val error = (result as ApiResult.Error).error
        assertTrue("expected Server but was $error", error is ApiError.Server)
        assertTrue((error as ApiError.Server).message.startsWith("refundAmount "))
    }

    @Test
    fun wireResultLeavesAnOrdinaryResultAlone() {
        assertEquals(ApiResult.Success("ok"), wireResult { ApiResult.Success("ok") })
    }

    @Test
    fun responseMapWireRewrapsTheMappedBodyKeepingStatusAndUrl() {
        val mapped = Response.success("raw", rawSuccess("/api/Order/GetById")).mapWire { it!!.length }

        assertEquals(200, mapped.code())
        assertEquals(3, mapped.body())
    }

    @Test
    fun responseMapWireLetsTheViolationOutForWireResultToAttribute() {
        assertThrows(WireContractViolation::class.java) {
            Response.success("raw", rawSuccess("/api/Order/GetById")).mapWire<String, Int> {
                null.required("total")
            }
        }
    }

    @Test
    fun requiredBodyNamesTheEndpointThatAnsweredWithNothing() {
        val violation = assertThrows(WireContractViolation::class.java) {
            Response.success<String>(null, rawSuccess("/api/Order/GetMyOrders")).requiredBody()
        }

        assertTrue(violation.message!!.contains("/api/Order/GetMyOrders"))
    }

    @Test
    fun requiredBodyPassesARealBodyThrough() {
        assertEquals("ok", Response.success("ok", rawSuccess("/api/Order/GetMyOrders")).requiredBody())
    }
}
