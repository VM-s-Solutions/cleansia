package cz.cleansia.partner.data.profile

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.MyPayoutDetails
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.api.model.UpdateBankDetailsCommand
import cz.cleansia.partner.api.model.UpdateBankDetailsResponse
import cz.cleansia.partner.api.model.UpdateJobRadiusCommand
import cz.cleansia.partner.api.model.UpdateJobRadiusResponse
import io.mockk.coEvery
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * Pins the [SessionScopedCache] contract of [ProfileRepositoryImpl]: the
 * registration-status watermark resets on sign-out so the next account's
 * registration gate re-fetches instead of trusting the prior user's cache.
 * Plus the payout contract: every field the payout command now carries has
 * to reach the wire, and "no payout details yet" is not a failure.
 */
class ProfileRepositoryTest {

    private lateinit var employeeApi: EmployeeApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    /** Matches NetworkModule's instance — `explicitNulls = false` is what drops a cleared radius. */
    private val wireJson = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    @Before
    fun setUp() {
        employeeApi = mockk()
    }

    private fun newRepo() = ProfileRepositoryImpl(employeeApi, json)

    private fun wireRepo(server: MockWebServer) = ProfileRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(wireJson.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(EmployeeApi::class.java),
        json,
    )

    @Test
    fun clear_resetsRegistrationStatusWatermark() = runTest {
        coEvery { employeeApi.employeeCheckCurrentEmployee(any()) } returns
            Response.success(mockk<RegistrationCompletionStatus>(relaxed = true))
        val repo = newRepo()
        repo.getRegistrationStatus()
        assertFalse(
            "watermark should be fresh after a successful fetch",
            repo.getRegistrationStatusStaleness().isStale(),
        )

        (repo as SessionScopedCache).clear()

        assertTrue(
            "watermark must be stale again after clear()",
            repo.getRegistrationStatusStaleness().isStale(),
        )
    }

    @Test
    fun getPayoutDetails_whenNoneRecorded_isSuccessWithNull() = runTest {
        val body = """{"type":"PayoutDetailsNotFound","detail":"payout.not_found",""" +
            """"errors":{"PayoutDetailsNotFound":"payout.not_found"}}"""
        coEvery { employeeApi.employeeGetMyPayoutDetails(any()) } returns Response.error<MyPayoutDetails>(
            400,
            body.toResponseBody("application/json".toMediaType()),
        )

        val result = newRepo().getPayoutDetails()

        assertTrue("a cleaner who never saved payout details is not an error", result is ApiResult.Success)
        assertNull((result as ApiResult.Success).data)
    }

    @Test
    fun getPayoutDetails_whenTheCallFails_staysAnError() = runTest {
        coEvery { employeeApi.employeeGetMyPayoutDetails(any()) } returns Response.error<MyPayoutDetails>(
            500,
            "boom".toResponseBody("text/plain".toMediaType()),
        )

        assertTrue(newRepo().getPayoutDetails() is ApiResult.Error)
    }

    @Test
    fun getPayoutDetails_returnsTheStoredDestination() = runTest {
        coEvery { employeeApi.employeeGetMyPayoutDetails(any()) } returns Response.success(
            MyPayoutDetails(accountNumber = "5885638003", bankCode = "5500"),
        )

        val result = newRepo().getPayoutDetails()

        assertEquals("5885638003", (result as ApiResult.Success).data?.accountNumber)
    }

    @Test
    fun updateBankDetails_sendsEveryFieldOnTheGeneratedCommand() = runTest {
        val command = slot<UpdateBankDetailsCommand>()
        coEvery { employeeApi.employeeUpdateBankDetails(capture(command)) } returns
            Response.success(UpdateBankDetailsResponse(employeeId = "emp-1"))

        newRepo().updateBankDetails(
            employeeId = "emp-1",
            bankCountryId = "country-cz",
            accountPrefix = "19",
            accountNumber = "2000145399",
            bankCode = "0800",
            iban = "CZ6508000000192000145399",
            swift = "GIBACZPX",
            bankName = "Česká spořitelna",
            holderName = "Jan Novák",
        )

        assertEquals(
            UpdateBankDetailsCommand(
                employeeId = "emp-1",
                iban = "CZ6508000000192000145399",
                bankCountryId = "country-cz",
                accountPrefix = "19",
                accountNumber = "2000145399",
                bankCode = "0800",
                swift = "GIBACZPX",
                bankName = "Česká spořitelna",
                holderName = "Jan Novák",
            ),
            command.captured,
        )
    }

    /**
     * The decode, over a socket, because the field is the whole reason the radius screen once had
     * its own hand-written client: `EmployeeItem` predated `jobRadiusKm` and the generated decoder
     * dropped it silently. A mocked API hands back a Kotlin object and would never have noticed.
     * This is the seed for the radius control — if it decodes away again, the toggle reads "every
     * job" for a cleaner who set 40 km.
     */
    @Test
    fun getCurrentEmployee_decodesTheJobRadiusOffTheWire() = runTest {
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody("""{"id":"emp-1","firstName":"Jan","jobRadiusKm":40}"""),
            )

            val result = wireRepo(server).getCurrentEmployee()

            assertEquals(40, (result as ApiResult.Success).data.jobRadiusKm)
            assertEquals("emp-1", result.data.id)
        } finally {
            server.shutdown()
        }
    }

    /** A cleaner on the country-wide board reads back as null, not as a zero-kilometre radius. */
    @Test
    fun getCurrentEmployee_readsAnAbsentJobRadiusAsNoLimit() = runTest {
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody("""{"id":"emp-1","firstName":"Jan"}"""),
            )

            val result = wireRepo(server).getCurrentEmployee()

            assertNull((result as ApiResult.Success).data.jobRadiusKm)
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun updateJobRadius_sendsTheChosenKilometres() = runTest {
        val command = slot<UpdateJobRadiusCommand>()
        coEvery { employeeApi.employeeUpdateJobRadius(capture(command)) } returns
            Response.success(UpdateJobRadiusResponse(employeeId = "emp-1", radiusKm = 25))

        newRepo().updateJobRadius(employeeId = "emp-1", radiusKm = 25)

        assertEquals(UpdateJobRadiusCommand(employeeId = "emp-1", radiusKm = 25), command.captured)
    }

    /**
     * The wire contract, not the Kotlin one. `radiusKm` is `int?` on the command and the app-wide
     * `Json` drops nulls, so a cleared preference travels as an ABSENT member — which binds to null
     * and reaches `SetJobRadius(null)`, the country-wide choice. A mocked interface would happily
     * pass a `0` here; over a socket, `"radiusKm":0` is a radius that matches nothing.
     */
    @Test
    fun updateJobRadius_clearsWithAnAbsentRadiusNeverZero() = runTest {
        val server = MockWebServer()
        server.start()
        try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody("""{"employeeId":"emp-1"}"""),
            )

            wireRepo(server).updateJobRadius(employeeId = "emp-1", radiusKm = null)

            val request = server.takeRequest()
            val body = request.body.readUtf8()
            assertEquals("PUT", request.method)
            assertEquals("/api/Employee/UpdateJobRadius", request.path)
            assertFalse("a cleared radius must not travel as zero", body.contains("\"radiusKm\":0"))
            assertFalse("a cleared radius must not carry a radius at all", body.contains("radiusKm"))
            assertTrue("the caller's own id still rides along", body.contains("\"employeeId\":\"emp-1\""))
        } finally {
            server.shutdown()
        }
    }
}
