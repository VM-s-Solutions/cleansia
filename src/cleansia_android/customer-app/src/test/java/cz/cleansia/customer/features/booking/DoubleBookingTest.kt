package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.PackageServiceSummary
import cz.cleansia.customer.core.catalog.ServiceListItem
import java.io.File
import org.junit.Assert.assertEquals
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
        assertNull(doubleBookingOfPackage("pkg-old", setOf("svc-oven"), listOf(unnamed)))
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
        val twice = doubleBookingOfPackage("pkg-kitchen", setOf("svc-fridge", "svc-windows"), packages)

        assertEquals(DoubleBooking.Package(kitchen, listOf(kitchen.includedServices!![1])), twice)
    }

    @Test
    fun `adding a package none of whose services is chosen books nothing twice`() {
        assertNull(doubleBookingOfPackage("pkg-kitchen", setOf("svc-windows"), packages))
    }

    // ── the copy ──

    private val locales = listOf("values", "values-cs", "values-sk", "values-uk", "values-ru")

    private val placeholders = mapOf(
        "booking_in_your_package" to listOf("%1\$s"),
        "booking_twice_service_title" to emptyList(),
        "booking_twice_service_message" to listOf("%1\$s", "%2\$s"),
        "booking_twice_service_confirm" to emptyList(),
        "booking_twice_package_title" to emptyList(),
        "booking_twice_package_message" to listOf("%1\$s", "%2\$s"),
        "booking_twice_package_confirm" to emptyList(),
    )

    @Test
    fun `the marker and both confirms are written in all five locales with their names`() {
        val resDir = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/res") }
            .firstOrNull { it.isDirectory }
            ?: error("customer-app res not found from ${File(".").absolutePath}")
        locales.forEach { locale ->
            val xml = File(resDir, "$locale/strings.xml").readText()
            placeholders.forEach { (key, names) ->
                val value = Regex("""<string name="$key">(.*?)</string>""").find(xml)?.groupValues?.get(1)
                assertTrue("$locale/$key is missing or blank", value?.isNotBlank() == true)
                names.forEach { assertTrue("$locale/$key lost $it", value!!.contains(it)) }
            }
        }
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
