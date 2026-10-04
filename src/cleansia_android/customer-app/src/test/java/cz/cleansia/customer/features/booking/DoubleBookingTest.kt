package cz.cleansia.customer.features.booking

import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.PackageServiceSummary
import cz.cleansia.customer.core.catalog.ServiceListItem
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

    // ── the copy ──

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val placeholders = mapOf(
        "booking_in_your_package" to listOf("%1\$s"),
        "booking_twice_service_title" to emptyList(),
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
