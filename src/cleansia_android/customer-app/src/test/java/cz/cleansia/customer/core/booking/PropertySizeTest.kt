package cz.cleansia.customer.core.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * Every basket validator refuses a home above `BookingPolicy.MaxRooms` / `MaxBathrooms`
 * (`order.size_exceeds_maximum`), so a stepper that goes one further offers a booking that cannot be
 * made. Read from the policy itself, so the two cannot drift apart unnoticed.
 */
class PropertySizeTest {

    private val solutionDir: File = generateSequence(File(".").absoluteFile) { it.parentFile }
        .firstOrNull { File(it, "Cleansia.Api.sln").isFile }
        ?: error("Cleansia.Api.sln not found above ${File(".").absolutePath}")

    private fun policyInt(name: String): Int {
        val source = File(solutionDir, "Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs").readText()
        return Regex("public\\s+const\\s+int\\s+$name\\s*=\\s*(\\d+)\\s*;").find(source)?.groupValues?.get(1)?.toInt()
            ?: error("BookingPolicy.$name not found — the parser needs updating")
    }

    @Test
    fun `the room cap is the server's`() {
        assertEquals(policyInt("MaxRooms"), PropertySize.MAX_ROOMS)
    }

    @Test
    fun `the bathroom cap is the server's`() {
        assertEquals(policyInt("MaxBathrooms"), PropertySize.MAX_BATHROOMS)
    }
}
