package cz.cleansia.core.serviceareas

import cz.cleansia.core.servicearea.ServicedCity
import java.text.Normalizer

/**
 * Whether a customer's address is in one of the serviced cities the server would accept.
 *
 * **A port of the server's `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, and it has to stay one.**
 * The server is the authority: it runs this rule inside `CreateOrder` and refuses the booking. This
 * copy exists only so the customer is told at address-selection time instead of at payment, which is
 * where they used to find out.
 *
 * **The danger of a copy is being STRICTER than the server, not looser.** A client that refuses a city
 * the server would accept tells a paying customer we do not serve them when we do. So the rule is
 * mirrored exactly, and `CityNameMatchTest` pins the same table of cases the C# tests pin — if the two
 * ever disagree, one of those suites goes red. → /product/business-rules#serviced-city
 */
object CityNameMatch {

    /**
     * The okresy: the rural districts AROUND a city, written exactly like a part of it (`Praha-západ`,
     * `Brno-venkov`). Dropping their dashed part would serve the countryside on a row for the city.
     */
    private val okresRings = setOf(
        "praha" to "zapad",
        "praha" to "vychod",
        "brno" to "venkov",
        "plzen" to "sever",
        "plzen" to "jih",
    )

    /** A trailing district: 1–2 digits (`Praha 8`) or a Roman numeral (`Pardubice II`). */
    private val trailingDistrict = Regex("""^(.+) (\p{Nd}{1,2}|[ivx]{1,6})$""")

    private val dashes = Regex("[\u2010-\u2015\u2212]")

    /** The server's `\s`, spelled out: `\s` is ASCII on the JVM, and Android's ICU refuses the flag that widens it. */
    private const val WHITESPACE = """[\t\n\x0B\f\r\x85\p{Z}]"""
    private val whitespaceRun = Regex("$WHITESPACE+")
    private val edgeWhitespace = Regex("^$WHITESPACE+|$WHITESPACE+\$")
    private val nonSpacingMarks = Regex("""\p{Mn}+""")
    private val postcodeDigits = Regex("[0-9]{5}")

    /**
     * A row with a ZIP prefix answers by the address's postcode when that is a valid one, even against
     * the name, and by the name only when it is not; a row without one answers by the name.
     */
    fun matches(city: ServicedCity, customerCity: String?, customerPostcode: String?): Boolean {
        val prefix = city.zipPrefix?.replace(edgeWhitespace, "").orEmpty()
        val postcode = customerPostcode?.replace(whitespaceRun, "")?.takeIf(postcodeDigits::matches)
        if (prefix.isNotEmpty() && postcode != null) return postcode.startsWith(prefix)
        return matches(city.name, customerCity)
    }

    /** The name rule. The district and the dashed part are dropped from the CUSTOMER's string only. */
    fun matches(servicedCityName: String?, customerCity: String?): Boolean {
        val row = fold(servicedCityName)
        val city = fold(customerCity)
        if (row.isEmpty() || city.isEmpty()) return false
        return listOfNotNull(city, withoutDashedPart(city))
            .any { row == it || row == withoutTrailingDistrict(it) }
    }

    /** True when ANY serviced city matches — the question a screen actually asks. */
    fun isServiced(servicedCities: List<ServicedCity>, customerCity: String?, customerPostcode: String?): Boolean =
        servicedCities.any { matches(it, customerCity, customerPostcode) }

    /** Split at the FIRST dash: `Ostrava 3 - Ostrava-Jih` is in Ostrava. */
    private fun withoutDashedPart(city: String): String? {
        if ('-' !in city) return null
        val head = city.substringBefore('-').trim(' ')
        val part = city.substringAfter('-').trim(' ')
        if (head.isEmpty() || part.isEmpty() || (head to part.substringBefore(' ')) in okresRings) return null
        return head
    }

    private fun withoutTrailingDistrict(value: String): String =
        trailingDistrict.find(value)?.groupValues?.get(1) ?: value

    private fun fold(value: String?): String {
        val trimmed = value?.replace(edgeWhitespace, "")
        if (trimmed.isNullOrEmpty()) return ""
        val decomposed = Normalizer.normalize(trimmed, Normalizer.Form.NFD)
        val stripped = Normalizer.normalize(nonSpacingMarks.replace(decomposed, ""), Normalizer.Form.NFC)
        return whitespaceRun.replace(dashes.replace(stripped.lowercase(), "-"), " ")
    }
}
