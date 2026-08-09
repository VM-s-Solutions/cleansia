package cz.cleansia.partner.core

import cz.cleansia.partner.features.orders.ProfileSection
import cz.cleansia.partner.features.profile.JobRadius
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

    /**
     * The only refusal `UpdateJobRadius` can return. The client clamps to the same bounds, so this
     * fires only when the two drift — which is exactly when a raw key is least useful.
     */
    @Test
    fun `the job-radius refusal resolves to a sentence naming the bounds`() {
        val value = Regex("<string name=\"error_employee_job_radius_out_of_range\">(.*?)</string>")
            .find(File(resDir, "values/strings.xml").readText())
            ?.groupValues
            ?.get(1)

        assertTrue("employee.job_radius_out_of_range renders raw", value != null)
        listOf(JobRadius.MIN_KM, JobRadius.MAX_KM).forEach { bound ->
            assertTrue(
                "the refusal no longer states the bound $bound: \"$value\"",
                value!!.contains(bound.toString()),
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
