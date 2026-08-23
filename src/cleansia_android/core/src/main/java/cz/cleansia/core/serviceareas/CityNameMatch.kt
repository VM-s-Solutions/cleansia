package cz.cleansia.core.serviceareas

import java.text.Normalizer

/**
 * Whether a customer's city is one of the serviced cities the server would accept.
 *
 * **A port of the server's `Cleansia.Core.Domain.ServiceAreas.CityNameMatch`, and it has to stay one.**
 * The server is the authority: it runs this rule inside `CreateOrder` and refuses the booking. This
 * copy exists only so the customer is told at address-selection time instead of at payment, which is
 * where they used to find out.
 *
 * **The danger of a copy is being STRICTER than the server, not looser.** A client that refuses a city
 * the server would accept tells a paying customer we do not serve them when we do. So the rule is
 * mirrored exactly, and `CityNameMatchTest` pins the same table of cases the C# tests pin — if the two
 * ever disagree, one of those suites goes red.
 *
 * Being LOOSER is survivable: the customer proceeds and the server refuses, which is the behaviour
 * that shipped before this existed.
 */
object CityNameMatch {

    /**
     * A trailing 1–2 digit district, optionally followed by a quarter after a dash — `Praha 8`,
     * `Praha 4 - Chodov`.
     *
     * A dash with NO leading number is deliberately not matched: `Praha-západ` and `Brno-venkov` share
     * that shape and are *okresy*, the rural rings around those cities rather than parts of them.
     */
    private val districtSuffix = Regex("""^(?<base>\S.*?)\s+\d{1,2}(\s*[-–—]\s*\S.*)?$""")

    private val whitespaceRun = Regex("""\s+""")
    private val nonSpacingMarks = Regex("""\p{Mn}+""")

    fun matches(servicedCityName: String?, customerCity: String?): Boolean {
        val row = fold(servicedCityName)
        val city = fold(customerCity)
        if (row.isEmpty() || city.isEmpty()) return false
        return row == city || row == stripDistrict(city)
    }

    /** True when ANY serviced city matches — the question a screen actually asks. */
    fun isServiced(servicedCityNames: List<String>, customerCity: String?): Boolean =
        servicedCityNames.any { matches(it, customerCity) }

    private fun fold(value: String?): String {
        if (value.isNullOrBlank()) return ""
        val decomposed = Normalizer.normalize(value.trim(), Normalizer.Form.NFD)
        val stripped = nonSpacingMarks.replace(decomposed, "")
        return whitespaceRun.replace(
            Normalizer.normalize(stripped, Normalizer.Form.NFC).lowercase(),
            " ",
        )
    }

    private fun stripDistrict(folded: String): String =
        districtSuffix.find(folded)?.groups?.get("base")?.value ?: folded
}
