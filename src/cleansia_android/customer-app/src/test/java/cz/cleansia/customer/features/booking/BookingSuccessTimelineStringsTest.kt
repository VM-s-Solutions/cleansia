package cz.cleansia.customer.features.booking

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
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

    /** The confirmation's approved copy, word for word; Czech binds its one-letter words with a no-break space. */
    private val confirmationCopy = mapOf(
        "booking_success_finding" to listOf(
            "No cleaner has taken it yet.", "Zatím ji nepřevzal žádný uklízeč.", "Zatiaľ ju neprevzal žiadny upratovač.",
            "Поки що жоден клінер його не взяв.", "Пока ни один клинер его не взял.",
        ),
        "booking_success_finding_sub" to listOf(
            "We'll let you know as soon as one does.", "Dáme vám vědět, jakmile ji někdo převezme.",
            "Dáme vám vedieť, hneď ako ju niekto prevezme.", "Повідомимо вас, щойно хтось його візьме.",
            "Сообщим вам, как только кто-то его возьмёт.",
        ),
        "booking_success_free_until" to listOf(
            "Free cancellation until %1\$s", "Zrušení zdarma do %1\$s", "Zrušenie zadarmo do %1\$s",
            "Безкоштовне скасування до %1\$s", "Бесплатная отмена до %1\$s",
        ),
        "booking_success_free_while" to listOf(
            "Free to cancel while no cleaner has taken it.", "Zrušení zdarma, dokud ji nepřevezme žádný uklízeč.",
            "Zrušenie zadarmo, kým ju neprevezme žiadny upratovač.", "Скасування безкоштовне, поки жоден клінер його не взяв.",
            "Отмена бесплатна, пока ни один клинер его не взял.",
        ),
        "booking_success_no_cleaner_card" to listOf(
            "If no cleaner can take it, we cancel it and refund you in full, plus credit.",
            "Když ji nikdo nepřevezme, zrušíme ji, vrátíme vám vše a přidáme kredit.",
            "Keď ju nikto neprevezme, zrušíme ju, vrátime vám všetko a pridáme kredit.",
            "Якщо його ніхто не візьме, ми скасуємо його, повернемо всю суму й нарахуємо кредит.",
            "Если его никто не возьмёт, мы отменим его, вернём всю сумму и начислим кредит.",
        ),
        "booking_success_no_cleaner_cash" to listOf(
            "If no cleaner can take it, we cancel it free of charge and give you credit.",
            "Když ji nikdo nepřevezme, zrušíme ji zdarma a přidáme vám kredit.",
            "Keď ju nikto neprevezme, zrušíme ju zadarmo a pridáme vám kredit.",
            "Якщо його ніхто не візьме, ми безкоштовно скасуємо його й нарахуємо вам кредит.",
            "Если его никто не возьмёт, мы бесплатно отменим его и начислим вам кредит.",
        ),
        "booking_success_cash_on_day" to listOf(
            "Please pay in cash to the cleaner on the day.", "Zaplaťte v hotovosti uklízeči v den úklidu.",
            "Zaplaťte v hotovosti upratovačovi v deň upratovania.", "Сплатіть готівкою клінеру в день прибирання.",
            "Оплатите наличными клинеру в день уборки.",
        ),
    )

    private val crewCopy = mapOf(
        "values" to listOf("1 cleaner", "2 cleaners", "3 cleaners", "5 cleaners"),
        "values-cs" to listOf("1 uklízeč", "2 uklízeči", "3 uklízeči", "5 uklízečů"),
        "values-sk" to listOf("1 upratovač", "2 upratovači", "3 upratovači", "5 upratovačov"),
        "values-uk" to listOf("1 клінер", "2 клінери", "3 клінери", "5 клінерів"),
        "values-ru" to listOf("1 клинер", "2 клинера", "3 клинера", "5 клинеров"),
    )
    private val crewNeededCopy = mapOf(
        "values" to listOf("It needs 1 cleaner.", "It needs 2 cleaners.", "It needs 3 cleaners.", "It needs 5 cleaners."),
        "values-cs" to listOf("Potřebuje 1 uklízeče.", "Potřebuje 2 uklízeče.", "Potřebuje 3 uklízeče.", "Potřebuje 5 uklízečů."),
        "values-sk" to listOf("Potrebuje 1 upratovača.", "Potrebuje 2 upratovačov.", "Potrebuje 3 upratovačov.", "Potrebuje 5 upratovačov."),
        "values-uk" to listOf("Потрібен 1 клінер.", "Потрібно 2 клінери.", "Потрібно 3 клінери.", "Потрібно 5 клінерів."),
        "values-ru" to listOf("Нужен 1 клинер.", "Нужно 2 клинера.", "Нужно 3 клинера.", "Нужно 5 клинеров."),
    )

    /** CLDR's integer plural category, as Android's ICU picks it, for the counts a crew can have. */
    private fun category(locale: String, n: Int): String = when (locale) {
        "values" -> if (n == 1) "one" else "other"
        "values-cs", "values-sk" -> when (n) { 1 -> "one"; in 2..4 -> "few"; else -> "other" }
        else -> when {
            n % 10 == 1 && n % 100 != 11 -> "one"
            n % 10 in 2..4 && n % 100 !in 12..14 -> "few"
            else -> "many"
        }
    }

    private val removedKeys = listOf(
        "booking_success_confirmation_code", "booking_success_subtitle", "booking_success_whats_next",
        "booking_success_arrival_label", "booking_success_address_label", "booking_success_progress", "booking_success_title",
    )

    @Test
    fun `the confirmation's new copy reads as written in all five locales`() {
        confirmationCopy.forEach { (key, expected) ->
            locales.forEachIndexed { i, locale ->
                val value = valueOf(stringsXml(locale), key)
                assertNotNull("$locale/strings.xml is missing $key", value)
                assertEquals("$locale/strings.xml $key", expected[i], plain(value!!))
            }
        }
    }

    @Test
    fun `the crew line and its spoken sentence take each locale's plural`() {
        listOf("booking_success_crew" to crewCopy, "booking_success_crew_needed" to crewNeededCopy).forEach { (key, copy) ->
            locales.forEach { locale ->
                val items = pluralsOf(stringsXml(locale), key)
                assertTrue("$locale/strings.xml is missing <plurals name=\"$key\">", items.isNotEmpty())
                listOf(1, 2, 3, 5).forEachIndexed { i, n ->
                    val item = items[category(locale, n)] ?: items["other"]
                    assertNotNull("$locale $key has no item for $n (${category(locale, n)})", item)
                    assertEquals("$locale $key for $n", copy.getValue(locale)[i], plain(item!!).replace("%1\$d", n.toString()))
                }
                assertTrue("$locale $key has no `other`", "other" in items)
            }
        }
    }

    @Test
    fun `Czech binds its one-letter words to the next one`() {
        val xml = stringsXml("values-cs")
        (confirmationCopy.keys).forEach { key ->
            val value = valueOf(xml, key)!!.replace("\\u00A0", "\u00A0")
            val loose = Regex("(^|[\\s\u00A0])([vkszouaiVKSZOUAI]) ").find(value)
            assertNull("values-cs $key leaves a one-letter word at a line end: \"${loose?.value}\"", loose)
        }
    }

    @Test
    fun `the round-3 confirmation's copy is gone from every locale`() {
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            removedKeys.forEach { key -> assertNull("$locale/strings.xml still carries $key", valueOf(xml, key)) }
        }
    }

    @Test
    fun `no confirmation copy offers a reschedule`() {
        val stems = listOf("reschedul", "přeplán", "přesun", "preplán", "presun", "перенес", "перенос")
        locales.forEach { locale ->
            val xml = stringsXml(locale)
            (confirmationCopy.keys + "booking_success_crew" + "booking_success_crew_needed").forEach { key ->
                val text = (valueOf(xml, key) ?: pluralsOf(xml, key).values.joinToString(" ")).lowercase()
                stems.forEach { stem -> assertFalse("$locale $key mentions $stem", stem in text) }
            }
        }
    }

    private fun plain(value: String) = value.replace("\\'", "'").replace("\\u00A0", " ").replace('\u00A0', ' ')

    private fun pluralsOf(xml: String, key: String): Map<String, String> {
        val block = Regex("<plurals name=\"$key\"[^>]*>(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL).find(xml)?.groupValues?.get(1)
            ?: return emptyMap()
        return Regex("<item quantity=\"(\\w+)\">(.*?)</item>", RegexOption.DOT_MATCHES_ALL).findAll(block)
            .associate { it.groupValues[1] to it.groupValues[2] }
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
