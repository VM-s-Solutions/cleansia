package cz.cleansia.customer.features.booking

import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.PackageServiceSummary
import cz.cleansia.customer.core.catalog.ServiceListItem
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.compositeOver
import androidx.compose.ui.graphics.luminance
import cz.cleansia.customer.ui.theme.DarkColors
import cz.cleansia.customer.ui.theme.LightColors
import cz.cleansia.customer.ui.theme.Sky100
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A package and a service it includes book that service twice, and the rule keeps both
 * (→ /product/business-rules#charging-a-package-and-a-service-together). The list marks such a service
 * and a tap that adds one asks first; what decides both is here.
 */
class DoubleBookingTest {

    private val windows = service("svc-windows")
    private val oven = service("svc-oven")
    private val fridge = service("svc-fridge")
    private val kitchen = pkg("pkg-kitchen", "svc-oven", "svc-fridge")
    private val spring = pkg("pkg-spring", "svc-windows", "svc-oven")
    private val packages = listOf(kitchen, spring)
    private val services = listOf(windows, oven, fridge)

    @Test
    fun `a service is marked with every chosen package that includes it`() {
        assertEquals(listOf(kitchen, spring), packages.selectedIncluding("svc-oven", setOf("pkg-kitchen", "pkg-spring")))
        assertEquals(listOf(kitchen), packages.selectedIncluding("svc-fridge", setOf("pkg-kitchen", "pkg-spring")))
    }

    @Test
    fun `a package that is not chosen marks nothing`() {
        assertEquals(emptyList<PackageListItem>(), packages.selectedIncluding("svc-oven", emptySet()))
        assertEquals(emptyList<PackageListItem>(), packages.selectedIncluding("svc-windows", setOf("pkg-kitchen")))
    }

    @Test
    fun `an included row without its service id marks nothing`() {
        val unnamed = PackageListItem(
            id = "pkg-old",
            name = "Old",
            price = 1.0,
            includedServices = listOf(PackageServiceSummary(name = "Oven")),
        )

        assertEquals(emptyList<PackageListItem>(), listOf(unnamed).selectedIncluding("svc-oven", setOf("pkg-old")))
        assertNull(doubleBookingOfPackage("pkg-old", setOf("svc-oven"), emptySet(), listOf(unnamed)))
    }

    @Test
    fun `adding a service a chosen package includes names the service and the package`() {
        val twice = doubleBookingOfService("svc-fridge", setOf("pkg-kitchen"), services, packages)

        assertEquals(DoubleBooking.Service(fridge, listOf(kitchen)), twice)
    }

    @Test
    fun `adding a service no chosen package includes books nothing twice`() {
        assertNull(doubleBookingOfService("svc-windows", setOf("pkg-kitchen"), services, packages))
        assertNull(doubleBookingOfService("svc-fridge", emptySet(), services, packages))
    }

    @Test
    fun `adding a package names only the chosen services it includes`() {
        val twice = doubleBookingOfPackage("pkg-kitchen", setOf("svc-fridge", "svc-windows"), emptySet(), packages)

        assertEquals(DoubleBooking.Package(kitchen, listOf(kitchen.includedServices!![1])), twice)
    }

    @Test
    fun `adding a package none of whose services is chosen books nothing twice`() {
        assertNull(doubleBookingOfPackage("pkg-kitchen", setOf("svc-windows"), emptySet(), packages))
    }

    /** R5: one chosen package including the service books it twice; two or more, and "twice" is false. */
    @Test
    fun `adding a service two chosen packages include says once more, one says twice`() {
        val many = doubleBookingOfService("svc-oven", setOf("pkg-kitchen", "pkg-spring"), services, packages)
        val one = doubleBookingOfService("svc-fridge", setOf("pkg-kitchen", "pkg-spring"), services, packages)

        assertEquals(DoubleBooking.Service(oven, listOf(kitchen, spring)), many)
        assertEquals(R.string.booking_twice_service_message_many, many!!.messageRes)
        assertEquals(R.string.booking_twice_service_message, one!!.messageRes)
    }

    /** S1: beside one chosen package the confirm's title and the row's marker say "package"; beside two or more, "packages". */
    @Test
    fun `the title and the marker name one package, and packages for two or more`() {
        val one = doubleBookingOfService("svc-fridge", setOf("pkg-kitchen", "pkg-spring"), services, packages)!!
        val many = doubleBookingOfService("svc-oven", setOf("pkg-kitchen", "pkg-spring"), services, packages)!!

        assertEquals(R.string.booking_twice_service_title, one.titleRes)
        assertEquals(R.string.booking_twice_service_title_many, many.titleRes)
        assertEquals(R.string.booking_in_your_package, one.packages.inPackageMarkerRes)
        assertEquals(R.string.booking_in_your_packages, many.packages.inPackageMarkerRes)
    }

    /** R5: two chosen packages that include the same service book it again, so the second one asks too. */
    @Test
    fun `adding a package that shares a service with a chosen package names that service`() {
        val again = doubleBookingOfPackage("pkg-spring", emptySet(), setOf("pkg-kitchen"), packages)

        assertEquals(DoubleBooking.Package(spring, listOf(spring.includedServices!![1])), again)
    }

    @Test
    fun `adding a package names what is chosen on its own and through a package, in its own order`() {
        val again = doubleBookingOfPackage("pkg-spring", setOf("svc-windows"), setOf("pkg-kitchen"), packages)

        assertEquals(DoubleBooking.Package(spring, spring.includedServices!!), again)
    }

    @Test
    fun `adding a package no chosen package or service overlaps books nothing again`() {
        val glass = pkg("pkg-glass", "svc-windows")

        assertNull(doubleBookingOfPackage("pkg-kitchen", setOf("svc-windows"), setOf("pkg-glass"), packages + glass))
    }

    // ── the covered look (T1) ──

    /**
     * The badge's ink keeps 4.5:1 over the badge, in both themes, on a covered row and on one that is
     * also picked; the brand primary would not (about 3.1:1). Measured from the schemes the app draws
     * with, composited the way the rows lay them: the row's fill over the card, the badge's over the row.
     */
    @Test
    fun `the badge reads at AA over a covered or picked row in both themes`() {
        listOf(false to LightColors, true to DarkColors).forEach { (dark, scheme) ->
            val covered = scheme.primary.copy(alpha = inPackageRowAlpha(dark)).compositeOver(scheme.surface)
            val picked = if (dark) scheme.primary.copy(alpha = 0.18f).compositeOver(scheme.surface) else Sky100
            listOf("covered" to covered, "picked" to picked).forEach { (row, fill) ->
                val badge = scheme.primary.copy(alpha = inPackageBadgeAlpha(dark)).compositeOver(fill)
                val theme = if (dark) "dark" else "light"
                val ink = contrast(scheme.onPrimaryContainer, badge)
                assertTrue("$theme $row badge ink is $ink:1", ink >= 4.5)
            }
        }
    }

    /**
     * The rows' secondary text (description, per-room price) and the "from" price keep 4.5:1 on every row
     * a service list draws, in both themes (U-5): a plain card, a covered row, a picked one in the booking
     * sheet (Sky100, or 18 % of the primary over the page or the card) and in the recurring form (6 %).
     * The theme's slate-400 measured 4.2:1 on a dark covered row, and the sky-600 primary 3.6:1 as text
     * on a light picked one.
     */
    @Test
    fun `the secondary text and the from price read at AA on every service row in both themes`() {
        listOf(false to LightColors, true to DarkColors).forEach { (dark, scheme) ->
            val theme = if (dark) "dark" else "light"
            val rows = listOf(
                Triple("plain", false, scheme.surface),
                Triple("covered", true, scheme.primary.copy(alpha = inPackageRowAlpha(dark)).compositeOver(scheme.surface)),
                Triple(
                    "picked on the page",
                    true,
                    if (dark) scheme.primary.copy(alpha = 0.18f).compositeOver(scheme.background) else Sky100,
                ),
                Triple(
                    "picked on a card",
                    true,
                    if (dark) scheme.primary.copy(alpha = 0.18f).compositeOver(scheme.surface) else Sky100,
                ),
                Triple("picked in the recurring form", true, scheme.primary.copy(alpha = 0.06f).compositeOver(scheme.background)),
            )
            rows.forEach { (row, tinted, fill) ->
                val secondary = contrast(rowSecondaryText(scheme, dark, tinted), fill)
                assertTrue("$theme $row secondary text is $secondary:1", secondary >= 4.5)
                val price = contrast(fromPriceInk(scheme, dark), fill)
                assertTrue("$theme $row from price is $price:1", price >= 4.5)
            }
        }
    }

    /** Only the text changes: the primary stays the brand colour of the fills, borders and the tick. */
    @Test
    fun `the service rows draw their text in the text-safe inks`() {
        val booking = source("features/booking/ServicesStep.kt").substringAfter("private fun ServiceRow(").substringBefore("fun fromPriceInk(")
        assertEquals(2, Regex(Regex.escape("color = rowSecondaryText(tinted = selected || covered)")).findAll(booking).count())
        assertTrue(booking.contains("color = fromPriceInk(MaterialTheme.colorScheme, isDark())"))
        val recurring = source("features/recurring/CreateRecurringScreen.kt").substringAfter("private fun ServiceCard(").substringBefore("private fun selectableCardModifier(")
        assertTrue(recurring.contains("color = rowSecondaryText(tinted = selected || inPackages.isNotEmpty())"))
    }

    @Test
    fun `a covered row takes more of the primary on a dark card, and its badge more still`() {
        assertEquals(0.08f, inPackageRowAlpha(dark = false))
        assertEquals(0.16f, inPackageRowAlpha(dark = true))
        assertEquals(0.14f, inPackageBadgeAlpha(dark = false))
        assertEquals(0.24f, inPackageBadgeAlpha(dark = true))
        assertEquals(0.6f, IN_PACKAGE_BORDER_ALPHA)
    }

    /** There is no Compose harness in this module, so the rows and the badge are read as source. */
    @Test
    fun `both service lists draw a covered row until it is picked, and the badge with its check`() {
        val booking = source("features/booking/ServicesStep.kt")
        val recurring = source("features/recurring/CreateRecurringScreen.kt")
        listOf(booking, recurring).forEach { screen ->
            assertTrue(Regex("""selected -> [^>]+ covered -> inPackageRowFill\(\)""").containsMatchIn(screen))
            assertTrue(Regex("""selected -> BorderStroke\(2\.dp, [^)]+\) covered -> inPackageRowBorder\(\)""").containsMatchIn(screen))
        }
        assertTrue(booking.contains("val covered = inPackages.isNotEmpty()"))
        assertTrue(recurring.contains("selectableCardModifier(selected = selected, covered = inPackages.isNotEmpty(), onClick = onClick)"))

        val marker = source("features/booking/DoubleBooking.kt").substringAfter("fun InPackageMarker(").substringBefore("fun DoubleBookingDialog(")
        assertTrue(marker.contains("Icons.Filled.CheckCircle"))
        assertTrue(marker.contains("RoundedCornerShape(12.dp)"))
        assertTrue(marker.contains("labelMedium.copy(fontWeight = FontWeight.SemiBold)"))
        assertTrue(marker.contains("tint = MaterialTheme.colorScheme.onPrimaryContainer"))
        assertTrue(marker.contains("color = MaterialTheme.colorScheme.onPrimaryContainer"))
        assertTrue(source("features/booking/DoubleBooking.kt").contains("BorderStroke(1.5.dp, MaterialTheme.colorScheme.primary.copy(alpha = IN_PACKAGE_BORDER_ALPHA))"))
    }

    private fun contrast(a: Color, b: Color): Double {
        val (light, dark) = listOf(a.luminance(), b.luminance()).sortedDescending()
        return (light + 0.05) / (dark + 0.05)
    }

    private fun source(path: String): String =
        File(resDir.parentFile, "java/cz/cleansia/customer/$path").readText().replace(Regex("\\s+"), " ")

    // ── the copy ──

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val placeholders = mapOf(
        "booking_in_your_package" to listOf("%1\$s"),
        "booking_in_your_packages" to listOf("%1\$s"),
        "booking_twice_service_title" to emptyList(),
        "booking_twice_service_title_many" to emptyList(),
        "booking_twice_service_message" to listOf("%1\$s", "%2\$s"),
        "booking_twice_service_message_many" to listOf("%1\$s", "%2\$s"),
        "booking_twice_service_confirm" to emptyList(),
        "booking_twice_package_title" to emptyList(),
        "booking_twice_package_message" to listOf("%1\$s", "%2\$s"),
        "booking_twice_package_confirm" to emptyList(),
    )

    private val resDir: File = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "src/main/res") }
        .firstOrNull { it.isDirectory }
        ?: error("customer-app res not found from ${File(".").absolutePath}")

    @Test
    fun `the marker and both confirms are written in all five locales with their names`() {
        locales.forEach { locale ->
            val xml = File(resDir, "$locale/strings.xml").readText()
            placeholders.forEach { (key, names) ->
                val value = Regex("""<string name="$key">(.*?)</string>""").find(xml)?.groupValues?.get(1)
                assertTrue("$locale/$key is missing or blank", value?.isNotBlank() == true)
                names.forEach { assertTrue("$locale/$key lost $it", value!!.contains(it)) }
            }
        }
    }

    /** "Twice" is false for a third booking of a service, and for a package that may overlap another package. */
    @Test
    fun `the once-more messages never say twice`() {
        val twice = mapOf(
            "values" to "twice",
            "values-cs" to "dvakrát",
            "values-sk" to "dvakrát",
            "values-uk" to "двічі",
            "values-ru" to "дважды",
        )
        locales.forEach { locale ->
            val xml = File(resDir, "$locale/strings.xml").readText()
            listOf("booking_twice_service_message_many", "booking_twice_package_message").forEach { key ->
                val value = Regex("""<string name="$key">(.*?)</string>""").find(xml)?.groupValues?.get(1)
                assertTrue("$locale/$key is missing", value != null)
                assertFalse("$locale/$key says twice: $value", value!!.contains(twice.getValue(locale), ignoreCase = true))
            }
        }
    }

    /** Owner ruling 2026-10-04: Slovak calls a package "balík", as the web does. Czech keeps "balíček". */
    @Test
    fun `Slovak says balik, never balicek, in every module`() {
        val android = resDir.canonicalFile.parentFile.parentFile.parentFile.parentFile
        val found = listOf("core", "partner-app", "customer-app").flatMap { module ->
            val dir = File(android, "$module/src/main/res/values-sk")
            assertTrue("$module has no values-sk", dir.isDirectory)
            dir.listFiles { file -> file.extension == "xml" }.orEmpty().flatMap { file ->
                file.readLines().filter { it.contains("balíč", ignoreCase = true) }.map { "$module/${file.name}: ${it.trim()}" }
            }
        }
        assertEquals(emptyList<String>(), found)
    }

    private fun service(id: String) = ServiceListItem(
        id = id,
        name = id,
        basePrice = 10.0,
        perRoomPrice = 0.0,
        category = CategoryDto(id = "c-1", slug = "home", name = "Home"),
    )

    private fun pkg(id: String, vararg serviceIds: String) = PackageListItem(
        id = id,
        name = id,
        price = 20.0,
        includedServices = serviceIds.map { PackageServiceSummary(name = it, serviceId = it) },
    )
}
