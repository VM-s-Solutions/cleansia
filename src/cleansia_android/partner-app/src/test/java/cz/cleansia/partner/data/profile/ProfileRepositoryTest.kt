package cz.cleansia.partner.data.profile

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.MyPayoutDetails
import cz.cleansia.partner.api.model.RegistrationCompletionStatus
import cz.cleansia.partner.api.model.UpdateBankDetailsCommand
import cz.cleansia.partner.api.model.UpdateBankDetailsResponse
import io.mockk.coEvery
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

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

    @Before
    fun setUp() {
        employeeApi = mockk()
    }

    private fun newRepo() = ProfileRepositoryImpl(employeeApi, json)

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
}
