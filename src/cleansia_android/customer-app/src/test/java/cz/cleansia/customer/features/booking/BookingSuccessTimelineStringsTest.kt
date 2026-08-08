package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The assignment step of the post-booking timeline. It stated an absolute time
 * ("Within 1 hour") that no dispatch mechanism has ever been able to keep — an
 * order can reach its cleaning time unclaimed. The step may describe what the
 * platform does; it may not state when the platform will be finished.
 */
class BookingSuccessTimelineStringsTest {

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val assignmentStepKeys = listOf(
        "booking_success_t2_title",
        "booking_success_t2_desc",
    )

    /**
     * Stems of a duration or calendar unit in each of the five shipped locales,
     * matched against whole words. A deadline needs one of these or a digit, so
     * the pair covers "1 hour", "60 minutes" and "do jedné hodiny" alike.
     */
    private val timeUnitStems = listOf(
        "hour", "minute", "day", "week",
        "hodin", "hodín", "minut", "minút", "den", "dní", "dnech", "týden", "týžd",
        "годин", "хвилин", "день", "дні", "тижд",
        "час", "минут", "дня", "дней", "недел",
    )

    private val resDir: File = sequenceOf(
        File("src/main/res"),
        File("customer-app/src/main/res"),
        File("src/cleansia_android/customer-app/src/main/res"),
    ).firstOrNull { it.isDirectory }
        ?: error("customer-app res/ not found from working dir ${File(".").absolutePath}")

    @Test
    fun `the assignment step is translated in all five locales`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assignmentStepKeys.forEach { key ->
                val value = valueOf(xml, key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertTrue("$locale/strings.xml has a blank $key", value!!.isNotBlank())
            }
        }
    }

    @Test
    fun `the four translations are not the English string copied over`() {
        val english = assignmentStepKeys.associateWith { valueOf(stringsXml("values"), it) }
        locales.drop(1).forEach { locale ->
            val xml = stringsXml(locale)
            assignmentStepKeys.forEach { key ->
                assertTrue(
                    "$locale/strings.xml left $key in English",
                    valueOf(xml, key) != english[key],
                )
            }
        }
    }

    @Test
    fun `no locale of the assignment step states an absolute time`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            assignmentStepKeys.forEach { key ->
                val value = valueOf(xml, key)!!

                assertFalse(
                    "$locale/strings.xml quotes a number in $key: $value",
                    value.any { it.isDigit() },
                )

                words(value).forEach { word ->
                    val stem = timeUnitStems.firstOrNull { word.startsWith(it) }
                    assertTrue(
                        "$locale/strings.xml names a time unit ($word) in $key: $value",
                        stem == null,
                    )
                }
            }
        }
    }

    @Test
    fun `the timeline still renders the assignment step`() {
        val screen = File(resDir.parentFile, "java/cz/cleansia/customer/features/booking/BookingSuccessScreen.kt")
        assertTrue("BookingSuccessScreen.kt not found next to res/", screen.isFile)
        val source = screen.readText()

        assignmentStepKeys.forEach { key ->
            assertTrue("the timeline no longer binds $key", source.contains("R.string.$key"))
        }
    }

    private fun words(value: String): List<String> =
        value.lowercase().split(Regex("[^\\p{L}]+")).filter { it.isNotBlank() }

    private fun stringsXml(locale: String): String {
        val file = File(resDir, "$locale/strings.xml")
        assertTrue("missing $locale/strings.xml", file.isFile)
        return file.readText()
    }

    private fun valueOf(xml: String, key: String): String? =
        Regex("<string name=\"$key\"[^>]*>(.*?)</string>", RegexOption.DOT_MATCHES_ALL)
            .find(xml)
            ?.groupValues
            ?.get(1)
}
