package cz.cleansia.partner.core.network

import android.content.Context
import android.content.res.Resources
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.R
import io.mockk.every
import io.mockk.mockk
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test

/**
 * The 401 arms. [ApiError.AuthRejected] resolves the backend key so a cleaner
 * who is not an employee reads why, while [ApiError.Unauthorized] — the session
 * layer's verdict — keeps its generic line untouched.
 */
class ApiErrorTranslatorTest {

    private lateinit var context: Context
    private lateinit var resources: Resources
    private lateinit var translator: ApiErrorTranslator

    private val packageName = "cz.cleansia.partner"

    private val genericUnauthorized = "Session expired. Please login again."
    private val mappedInsufficient = "Your account is not set up as a cleaner yet."

    @Before
    fun setUp() {
        context = mockk(relaxed = true)
        resources = mockk(relaxed = true)

        every { context.packageName } returns packageName
        every { context.resources } returns resources
        every { context.getString(R.string.error_unauthorized) } returns genericUnauthorized
        every { resources.getIdentifier(any(), "string", packageName) } returns 0

        translator = ApiErrorTranslator(context)
    }

    @Test
    fun translate_givenAuthRejected_resolvesTheBusinessKey() {
        val mappedResId = 51
        every {
            resources.getIdentifier("error_auth_insufficient_privileges", "string", packageName)
        } returns mappedResId
        every { context.getString(mappedResId) } returns mappedInsufficient

        val message = translator.translate(ApiError.AuthRejected("auth.insufficient_privileges"))

        assertEquals(mappedInsufficient, message)
    }

    @Test
    fun translate_givenAuthRejectedWithUnmappedKey_returnsGenericAndNeverTheRawKey() {
        // translateBadRequest shows unmapped keys raw; this arm must not — it
        // renders on the sign-in screen.
        val message = translator.translate(ApiError.AuthRejected("auth.brand_new_key"))

        assertEquals(genericUnauthorized, message)
    }

    @Test
    fun translate_givenUnauthorized_stillReturnsTheSessionLine() {
        assertEquals(genericUnauthorized, translator.translate(ApiError.Unauthorized))
    }
}
