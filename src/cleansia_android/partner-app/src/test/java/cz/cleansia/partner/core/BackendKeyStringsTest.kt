package cz.cleansia.partner.core

import cz.cleansia.partner.features.orders.ProfileSection
import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Two screens render backend keys through `Resources.getIdentifier`, and both
 * degrade to the raw key on a miss — `ApiErrorTranslator.lookupKey` after the
 * validation map, `RegistrationLockScreen.resolveDetail` for every missing
 * profile field. Neither miss breaks the build, so a backend key rename ships a
 * cleaner "order.take.already_cancelled" where a sentence belongs.
 */
class BackendKeyStringsTest {

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("partner-app/src/main/res"),
        File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("partner-app res/ not found from working dir ${File(".").absolutePath}")

    private val declared: Set<String> = Regex("<string name=\"([^\"]+)\"")
        .findAll(File(resDir, "values/strings.xml").readText())
        .map { it.groupValues[1] }
        .toSet()

    @Test
    fun `the take gate's terminal refusals resolve to a sentence`() {
        listOf("order.take.already_cancelled", "order.take.already_completed").forEach { key ->
            val resName = "error_" + key.replace('.', '_').replace('-', '_').lowercase()
            assertTrue(
                "$key renders raw — values/strings.xml declares no <string name=\"$resName\">",
                resName in declared,
            )
        }
    }

    @Test
    fun `every profile field the lock can list resolves to a label`() {
        ProfileSection.entries.flatMap { it.ownedFields() }.forEach { key ->
            val resName = key.replace('.', '_')
            assertTrue(
                "$key renders raw — values/strings.xml declares no <string name=\"$resName\">",
                resName in declared,
            )
        }
    }
}
