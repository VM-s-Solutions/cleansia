package cz.cleansia.customer.features.profile

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * Profile rows reach navigation through a `String` key and a `when (key)` used
 * as a *statement*, which Kotlin does not require to be exhaustive. A row whose
 * key has no branch therefore compiles, renders, and does nothing at all when
 * tapped — which is what the "Privacy" row did for its whole life.
 *
 * Neither end can be made type-safe without reshaping the callback, so this
 * closes the loop from the outside: every key the tab can emit must have a
 * branch, and every branch must belong to a key the tab can emit.
 */
class ProfileRowRoutingTest {

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res/values/strings.xml").isFile }
        ?: error("customer-app module not found from working dir ${File(".").absolutePath}")

    private fun source(path: String): String {
        val file = File(moduleDir, "src/main/java/cz/cleansia/customer/$path")
        assertTrue("${file.absolutePath} not found", file.isFile)
        return file.readText()
    }

    private val renderedKeys: Set<String> by lazy {
        val tab = source("features/profile/ProfileTab.kt")
        val rows = Regex("ProfileRow\\(\\s*\"([^\"]+)\"").findAll(tab).map { it.groupValues[1] }
        val direct = Regex("onRowClick\\(\"([^\"]+)\"\\)").findAll(tab).map { it.groupValues[1] }
        (rows + direct).toSet()
    }

    private val routedKeys: Set<String> by lazy {
        val host = source("navigation/CleansiaNavHost.kt")
        val start = host.indexOf("when (key) {")
        assertTrue("CleansiaNavHost no longer routes profile rows by key", start >= 0)
        var depth = 0
        var end = start
        for (index in host.indexOf('{', start) until host.length) {
            when (host[index]) {
                '{' -> depth++
                '}' -> {
                    depth--
                    if (depth == 0) {
                        end = index
                        break
                    }
                }
            }
        }
        Regex("\"([^\"]+)\"\\s*->").findAll(host.substring(start, end))
            .map { it.groupValues[1] }
            .toSet()
    }

    @Test
    fun `the tab still renders rows and the host still routes them`() {
        assertTrue("no ProfileRow keys found — the parser is stale", renderedKeys.isNotEmpty())
        assertTrue("no routing branches found — the parser is stale", routedKeys.isNotEmpty())
    }

    @Test
    fun `every profile row a user can tap goes somewhere`() {
        val dead = (renderedKeys - routedKeys).sorted()
        if (dead.isNotEmpty()) {
            fail(
                "these profile rows render but have no branch in CleansiaNavHost's " +
                    "when(key), so tapping them silently does nothing: $dead",
            )
        }
    }

    @Test
    fun `no routing branch outlives the row that used it`() {
        val orphans = (routedKeys - renderedKeys).sorted()
        if (orphans.isNotEmpty()) {
            fail("CleansiaNavHost routes keys no profile row emits: $orphans")
        }
    }
}
