package cz.cleansia.core.auth

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The confirmation sets the account's password to the one it carries, so the confirm screen sends the
 * one just typed at registration only for that same address, and asks for one in every other case.
 */
class RegistrationPasswordTest {

    @Test
    fun `the password typed at registration is held for that address`() {
        val held = RegistrationPassword().apply { hold("jana@cleansia.cz", "Heslo2026") }

        assertEquals("Heslo2026", held.heldFor("jana@cleansia.cz"))
    }

    @Test
    fun `the address matches whatever its letter case or surrounding spaces`() {
        val held = RegistrationPassword().apply { hold(" Jana@Cleansia.cz ", "Heslo2026") }

        assertEquals("Heslo2026", held.heldFor("jana@cleansia.cz"))
    }

    @Test
    fun `nothing is held for another address`() {
        val held = RegistrationPassword().apply { hold("jana@cleansia.cz", "Heslo2026") }

        assertNull(held.heldFor("petr@cleansia.cz"))
    }

    @Test
    fun `nothing is held before a registration`() {
        assertNull(RegistrationPassword().heldFor("jana@cleansia.cz"))
    }

    @Test
    fun `a second registration replaces the first`() {
        val held = RegistrationPassword().apply {
            hold("jana@cleansia.cz", "Heslo2026")
            hold("petr@cleansia.cz", "Jiné2026")
        }

        assertNull(held.heldFor("jana@cleansia.cz"))
        assertEquals("Jiné2026", held.heldFor("petr@cleansia.cz"))
    }

    @Test
    fun `a session wipe forgets it`() = runTest {
        val held = RegistrationPassword().apply { hold("jana@cleansia.cz", "Heslo2026") }

        held.clear()

        assertNull(held.heldFor("jana@cleansia.cz"))
    }
}
