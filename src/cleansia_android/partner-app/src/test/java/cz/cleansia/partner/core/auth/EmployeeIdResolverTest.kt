package cz.cleansia.partner.core.auth

import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.model.EmployeeItem
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.Response

/**
 * The resolver exists for installs that are *already* holding a null employee
 * id — sessions minted by the confirm-email path before it hydrated. Those
 * sessions last up to 30 days and may never run login again, so the back-fill
 * has to happen on first read rather than at sign-in.
 */
class EmployeeIdResolverTest {

    private val employeeApi = mockk<EmployeeApi>()
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private val baseProfile = UserProfileData(
        userId = "user-1",
        email = "cleaner@cleansia.cz",
        employeeId = null,
        isEmailConfirmed = true,
        hasAdminAccess = false,
        firstName = "Jana",
        lastName = "Nováková",
        role = "Employee",
    )

    /** In-memory stand-in for the DataStore-backed store. */
    private var storedProfile: UserProfileData? = baseProfile
    private val userProfileStore = mockk<UserProfileStore>().also { store ->
        coEvery { store.current() } answers { storedProfile }
        coEvery { store.updateEmployeeId(any()) } answers {
            storedProfile = storedProfile?.copy(employeeId = firstArg())
        }
    }

    private fun resolver() = EmployeeIdResolver(userProfileStore, employeeApi, json)

    private fun employeeResponse(id: String?) =
        Response.success(EmployeeItem(id = id, firstName = "Jana", lastName = "Nováková"))

    @Test
    fun `returns the persisted id without touching the network`() = runTest {
        storedProfile = baseProfile.copy(employeeId = "emp-1")

        assertEquals("emp-1", resolver().resolve())

        coVerify(exactly = 0) { employeeApi.employeeGetCurrentEmployee() }
    }

    @Test
    fun `fetches and persists the id when the profile has none`() = runTest {
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns employeeResponse("emp-7")

        assertEquals("emp-7", resolver().resolve())

        // Persisting is what fixes the OTHER read sites (OrderNotesViewModel
        // observes the profile flow) without them knowing about the resolver.
        assertEquals("emp-7", storedProfile?.employeeId)
        coVerify(exactly = 1) { employeeApi.employeeGetCurrentEmployee() }
    }

    @Test
    fun returnsNullAndDoesNotPersistOnApiError() = runTest {
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns Response.error(
            500,
            "boom".toResponseBody("text/plain".toMediaType()),
        )

        assertNull(resolver().resolve())

        // Nothing written: a transient outage must not poison the profile with
        // a blank id that then looks "resolved" forever.
        assertNull(storedProfile?.employeeId)
        coVerify(exactly = 0) { userProfileStore.updateEmployeeId(any()) }
    }

    @Test
    fun `returns null without calling the API when there is no session at all`() = runTest {
        storedProfile = null

        assertNull(resolver().resolve())

        // No profile row means signed out; GetCurrentEmployee would be a
        // guaranteed 401 fired from every scoped screen of the signed-out shell.
        coVerify(exactly = 0) { employeeApi.employeeGetCurrentEmployee() }
    }

    @Test
    fun `reads through the store every time so a sign-out wipe is authoritative`() = runTest {
        storedProfile = baseProfile.copy(employeeId = "emp-1")
        val resolver = resolver()
        assertEquals("emp-1", resolver.resolve())

        // Sign-out clears UserProfileStore (it is a SessionScopedCache). If the
        // resolver kept its own field it would outlive that wipe and hand the
        // previous cleaner's employee id to the next user of a shared device.
        storedProfile = null
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns employeeResponse("emp-2")

        assertNull(resolver.resolve())

        // And the next user of the device gets THEIR id, not the cached one.
        storedProfile = baseProfile.copy(userId = "user-2", employeeId = null)
        assertEquals("emp-2", resolver.resolve())
    }

    @Test
    fun `concurrent callers share a single fetch`() = runTest {
        coEvery { employeeApi.employeeGetCurrentEmployee() } returns employeeResponse("emp-7")
        val resolver = resolver()

        // The dashboard, the orders list and the invoices list all resolve
        // within a few ms of app start.
        val ids = listOf(
            async { resolver.resolve() },
            async { resolver.resolve() },
            async { resolver.resolve() },
        ).awaitAll()

        assertEquals(listOf("emp-7", "emp-7", "emp-7"), ids)
        coVerify(exactly = 1) { employeeApi.employeeGetCurrentEmployee() }
    }
}
