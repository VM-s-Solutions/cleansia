package cz.cleansia.core.location

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Every map in both apps draws the one Cleansia look — no third-party POIs, the brand pin — and keeps
 * the Mapbox wordmark and attribution its terms require. Mapbox rendering is not unit-testable, so this
 * holds the call sites instead: a new map, or an old one reverted, fails here rather than on a device.
 */
class CleansiaMapUsageTest {

    private val androidRoot: File = sequenceOf(File(".."), File("."), File("src/cleansia_android"))
        .map { it.absoluteFile.normalize() }
        .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
        ?: error("cleansia_android not found from ${File(".").absolutePath}")

    private val mapFiles: List<File> by lazy {
        listOf("customer-app", "partner-app").flatMap { app ->
            File(androidRoot, "$app/src/main/java").walkTopDown()
                .filter { it.isFile && it.extension == "kt" && it.readText().contains("MapboxMap(") }
                .toList()
        }
    }

    @Test
    fun `the four map surfaces are still found`() {
        assertEquals(
            setOf("AddressManagerScreen.kt", "OrderDetailMap.kt", "AddressPickerScreen.kt", "OrderDetailScreen.kt"),
            mapFiles.map { it.name }.toSet(),
        )
    }

    @Test
    fun `every map draws the Cleansia style and pin`() {
        mapFiles.forEach { file ->
            val source = file.readText()
            assertTrue("${file.name} does not draw CleansiaMapStyle", source.contains("CleansiaMapStyle("))
            assertTrue("${file.name} does not draw CleansiaMapPin", source.contains("CleansiaMapPin("))
            assertTrue("${file.name} names a stock Mapbox style", !Regex("mapbox://styles/").containsMatchIn(source))
        }
    }

    @Test
    fun `no map hides the Mapbox wordmark or attribution`() {
        mapFiles.forEach { file ->
            val flat = file.readText().replace(Regex("\\s+"), " ")
            assertTrue("${file.name} hides the Mapbox wordmark", !flat.contains("logo = {}"))
            assertTrue("${file.name} hides the Mapbox attribution", !flat.contains("attribution = {}"))
        }
    }
}
