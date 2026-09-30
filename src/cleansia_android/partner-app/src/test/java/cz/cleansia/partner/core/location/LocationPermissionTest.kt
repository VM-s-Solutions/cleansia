package cz.cleansia.partner.core.location

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The partner app reads the device location for two things only, both in the foreground: the rough
 * distance to a job on the feed, and where the address picker's map opens before the cleaner moves
 * the pin. Neither needs a precise fix, so the app holds approximate location and nothing more.
 *
 * Android silently denies a request for a permission the manifest does not declare, so a screen
 * still asking for the fine one would read as "the cleaner said no" on every device.
 */
class LocationPermissionTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).firstOrNull { File(it, "src/main/AndroidManifest.xml").isFile }
        ?: error("partner-app not found from working dir ${File(".").absolutePath}")

    private val declared: List<String> = Regex("<uses-permission\\s+android:name=\"android\\.permission\\.([A-Z_]+)\"")
        .findAll(File(moduleDir, "src/main/AndroidManifest.xml").readText())
        .map { it.groupValues[1] }
        .toList()

    @Test
    fun `the manifest declares approximate location only`() {
        assertTrue("ACCESS_COARSE_LOCATION" in declared)
        assertTrue("the partner app declares precise location again", "ACCESS_FINE_LOCATION" !in declared)
    }

    @Test
    fun `no screen asks for precise location`() {
        val requests = File(moduleDir, "src/main/java").walkTopDown()
            .filter { it.isFile && it.extension == "kt" }
            .filter { it.readText().contains("ACCESS_FINE_LOCATION") }
            .map { it.name }
            .toList()
        assertEquals(emptyList<String>(), requests)
    }
}
