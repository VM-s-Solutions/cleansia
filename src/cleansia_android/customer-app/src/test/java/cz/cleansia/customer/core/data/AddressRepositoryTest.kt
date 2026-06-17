package cz.cleansia.customer.core.data

import android.content.Context
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.SavedAddressApi
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Behaviour contract for the network-error channel of [AddressRepository].
 *
 * Scope: the DataStore-free branches where a network/HTTP failure (or the
 * coordinate guard) decides the outcome — [AddressRepository.refreshFromServer]
 * and the create path of [AddressRepository.upsert]. Those return before any
 * `context.addressStore` write, so they run on a mocked [Context] without
 * Robolectric (matching the existing OrderRepository/CatalogRepository harness,
 * which is also DataStore-free). The success-write paths and
 * delete/setDefault/rename read the cache via DataStore first and need an
 * instrumented context, so they stay out of this JVM characterization.
 *
 * Characterization -> migrate -> green: the same success/failure outcome and the
 * same single parsed message are preserved across the String?/snackbar-in-repo
 * -> ApiResult<Unit>/snackbar-in-VM migration. The repo no longer touches the
 * snackbar — it carries the parsed message in [ApiResult.Error]; the consuming
 * ViewModel surfaces it, keeping an [ApiError.Network] failure silent.
 */
class AddressRepositoryTest {

    private lateinit var api: SavedAddressApi
    private lateinit var tokenStore: TokenStore
    private lateinit var appContext: Context

    private val networkMessage = "Check your internet connection and try again."
    private val serverMessage = "Server problem. Please try again later."
    private val unknownMessage = "Something went wrong. Please try again."
    private val movePinMessage = "Move the pin to a valid location."

    @Before
    fun setUp() {
        api = mockk()
        tokenStore = mockk()
        appContext = mockk(relaxed = true)

        every { appContext.getString(R.string.error_generic_network) } returns networkMessage
        every { appContext.getString(R.string.error_generic_server) } returns serverMessage
        every { appContext.getString(R.string.error_generic_unknown) } returns unknownMessage
        every { appContext.getString(R.string.error_generic_unauthorized) } returns "unauth"
        every { appContext.getString(R.string.address_picker_move_pin) } returns movePinMessage
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newRepo() = AddressRepository(api, tokenStore, appContext)

    private fun signedIn() {
        every { tokenStore.current() } returns mockk<TokenStore.Tokens>()
    }

    private fun guest() {
        every { tokenStore.current() } returns null
    }

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    private fun addressWithCoords() = UserAddress(
        id = "local-1",
        serverId = null,
        label = "Home",
        street = "Main 1",
        city = "Prague",
        zipCode = "11000",
        latitude = 50.0,
        longitude = 14.0,
    )

    private fun addressWithoutCoords() = addressWithCoords().copy(latitude = null, longitude = null)

    // ── refreshFromServer() ──

    @Test
    fun refreshFromServer_whenGuest_returnsSuccess() = runTest {
        guest()

        val result = newRepo().refreshFromServer()

        assertTrue("guest refresh is a no-op Success but got: $result", result is ApiResult.Success)
    }

    @Test
    fun refreshFromServer_givenHttp500_returnsServerErrorWithParsedMessage() = runTest {
        signedIn()
        coEvery { api.getMine() } returns Response.error(500, errorBody())

        val result = newRepo().refreshFromServer()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        // Empty body + 500 → the (errorBody, code) parser's server fallback string.
        assertEquals(serverMessage, (result as ApiResult.Error).error.message)
        assertTrue(result.error is ApiError.Server)
    }

    @Test
    fun refreshFromServer_whenApiThrows_returnsNetworkErrorSilently() = runTest {
        signedIn()
        coEvery { api.getMine() } throws java.io.IOException("boom")

        val result = newRepo().refreshFromServer()

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue(
            "network failure must carry ApiError.Network so the VM keeps it silent",
            (result as ApiResult.Error).error is ApiError.Network,
        )
        assertEquals(networkMessage, result.error.message)
    }

    // ── upsert() → createOnServer() ──

    @Test
    fun upsert_create_whenCoordinatesMissing_returnsBadRequestMovePinMessage() = runTest {
        signedIn()

        val result = newRepo().upsert(addressWithoutCoords())

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertEquals(movePinMessage, (result as ApiResult.Error).error.message)
        assertTrue(result.error is ApiError.BadRequest)
    }

    @Test
    fun upsert_create_givenHttp400_returnsBadRequestWithParsedMessage() = runTest {
        signedIn()
        coEvery { api.add(any()) } returns Response.error(400, errorBody())

        val result = newRepo().upsert(addressWithCoords(), setAsDefault = false)

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        // Empty body + 400 → the generic-unknown fallback string.
        assertEquals(unknownMessage, (result as ApiResult.Error).error.message)
        assertTrue(result.error is ApiError.BadRequest)
    }

    @Test
    fun upsert_create_whenApiThrows_returnsNetworkErrorSilently() = runTest {
        signedIn()
        coEvery { api.add(any()) } throws java.io.IOException("boom")

        val result = newRepo().upsert(addressWithCoords(), setAsDefault = false)

        assertTrue("expected Error but got: $result", result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        assertEquals(networkMessage, result.error.message)
    }
}
