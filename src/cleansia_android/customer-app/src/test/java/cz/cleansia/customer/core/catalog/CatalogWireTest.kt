package cz.cleansia.customer.core.catalog

import cz.cleansia.customer.core.network.IntEnumSerializersModule
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import cz.cleansia.core.network.WireContractViolation
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import cz.cleansia.customer.api.client.ExtraApi as GenExtraApi
import cz.cleansia.customer.api.client.PackageApi as GenPackageApi
import cz.cleansia.customer.api.client.ServiceApi as GenServiceApi
import cz.cleansia.customer.api.model.ExtraListItem as GenExtraListItem
import cz.cleansia.customer.api.model.PackageListItem as GenPackageListItem
import cz.cleansia.customer.api.model.ServiceListItem as GenServiceListItem

/**
 * The catalog is a price list, and it refuses the page where the orders list drops the row. The
 * difference is that the catalog *is* the addends: ConfirmStep renders the pre-quote subtotal as a sum
 * over `services.filter { it.id in state.selectedServiceIds }`, and the selection lives in
 * `BookingState` rather than in this list — so a dropped row keeps its id selected, still priced by
 * the server on Create, and silently missing from the figure the customer reads before agreeing to it.
 *
 * Everything here decodes a captured payload over a socket through the generated DTOs and the
 * production `Json`.
 */
class CatalogWireTest {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        serializersModule = IntEnumSerializersModule
    }

    private suspend fun <T> serving(
        body: String,
        code: Int = 200,
        onRequest: (RecordedRequest) -> Unit = {},
        call: suspend (CatalogApi) -> T,
    ): T {
        val server = MockWebServer()
        server.start()
        return try {
            server.enqueue(
                MockResponse()
                    .setResponseCode(code)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body),
            )
            val retrofit = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
                .build()
            val api = CatalogApi(
                retrofit.create(GenServiceApi::class.java),
                retrofit.create(GenPackageApi::class.java),
                retrofit.create(GenExtraApi::class.java),
            )
            call(api).also { onRequest(server.takeRequest()) }
        } finally {
            server.shutdown()
        }
    }

    private suspend fun services(body: String, code: Int = 200) =
        serving(body, code) { it.getServices() }.body()

    private suspend fun packages(body: String, code: Int = 200) =
        serving(body, code) { it.getPackages() }.body()

    private suspend fun extras(body: String, code: Int = 200) =
        serving(body, code) { it.getExtras() }.body()

    /**
     * Services and packages refuse by throwing now, so the assertion is on the field the refusal
     * names rather than on a null body — the whole point of the idiom is that the name survives.
     */
    private suspend fun refuses(field: String, mapping: suspend () -> Any?) {
        val violation = try {
            mapping()
            null
        } catch (v: WireContractViolation) {
            v
        }
        assertNotNull("a missing $field must refuse the mapping", violation)
        assertTrue(
            "the refusal must name $field, but said \"${violation!!.message}\"",
            violation.message!!.startsWith("$field "),
        )
    }

    private suspend fun loadedServices(body: String): List<ServiceListItem> {
        val list = services(body)
        assertNotNull("expected the captured payload to map", list)
        return list!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun serviceDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(SERVICE_SPEC_PROPERTIES, serialNames(GenServiceListItem.serializer().descriptor))
    }

    @Test
    fun packageDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(PACKAGE_SPEC_PROPERTIES, serialNames(GenPackageListItem.serializer().descriptor))
    }

    @Test
    fun extraDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(EXTRA_SPEC_PROPERTIES, serialNames(GenExtraListItem.serializer().descriptor))
    }

    @Test
    fun theRequestsKeepThePathsTheServerBinds() = runTest {
        var path: String? = null
        serving(CAPTURED_SERVICES, onRequest = { path = it.path }) { it.getServices() }
        assertEquals("/api/Service/GetOverview", path)

        serving(CAPTURED_PACKAGES, onRequest = { path = it.path }) { it.getPackages() }
        assertEquals("/api/Package/GetOverview", path)

        serving(CAPTURED_EXTRAS, onRequest = { path = it.path }) { it.getExtras() }
        assertEquals("/api/Extra/GetOverview", path)
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyCatalogPriceArrivesWithItsLiteralValue() = runTest {
        val service = loadedServices(CAPTURED_SERVICES).first()

        assertEquals(2900.00, service.basePrice, 0.0)
        assertEquals(180.00, service.perRoomPrice, 0.0)
        assertEquals(450.00, packages(CAPTURED_PACKAGES)?.first()?.price)
        assertEquals(300.00, extras(CAPTURED_EXTRAS)?.first()?.price)
    }

    @Test
    fun aMissingServicePriceRefusesTheWholePriceListRatherThanOfferingItFree() = runTest {
        listOf("basePrice", "perRoomPrice").forEach { field ->
            refuses(field) { services(servicesWithFirstRow { it - field }) }
        }
    }

    @Test
    fun aMissingPackagePriceRefusesTheWholePriceList() = runTest {
        refuses("price") { packages(packagesWithFirstRow { it - "price" }) }
    }

    @Test
    fun aMissingExtraPriceRefusesTheWholeAddOnList() = runTest {
        assertNull(extras(extrasWithFirstRow { it - "price" }))
    }

    @Test
    fun anExplicitNullPriceRefusesTheListToo() = runTest {
        refuses("basePrice") { services(servicesWithFirstRow { it + ("basePrice" to JsonNull) }) }
    }

    // --- rule 2: booleans follow the money rule ---------------------------------
    //
    // No catalog schema carries a `nullable: false` boolean; the rule is exercised on the quote and
    // order wires (BookingQuoteWireTest, OrderWireTest).

    // --- rule 3: identity is refused, never synthesized --------------------------

    /**
     * The identity ruling inverts here for the same reason the money one does. On the orders list an
     * unidentifiable row is dropped; here dropping it removes an addend from a rendered sum, so an
     * unidentifiable service fails the page rather than quietly shrinking the subtotal.
     */
    @Test
    fun aServiceWithoutAnIdRefusesThePageRatherThanVanishingFromTheSubtotal() = runTest {
        refuses("id") { services(servicesWithFirstRow { it - "id" }) }
    }

    @Test
    fun aPackageWithoutAnIdRefusesThePage() = runTest {
        refuses("id") { packages(packagesWithFirstRow { it - "id" }) }
    }

    @Test
    fun anExtraWithoutItsSlugRefusesThePageBecauseTheSlugIsWhatGetsQuoted() = runTest {
        assertNull(extras(extrasWithFirstRow { it - "slug" }))
    }

    @Test
    fun everySurvivingRowKeepsTheIdTheWireCarried() = runTest {
        assertEquals(listOf("svc-1", "svc-2"), loadedServices(CAPTURED_SERVICES).map { it.id })
    }

    @Test
    fun aServiceWhoseCategoryIsBrokenRefusesThePage() = runTest {
        refuses("displayOrder") {
            services(
                servicesWithFirstRow { row ->
                    row + ("category" to (row["category"]!!.jsonObject - "displayOrder"))
                },
            )
        }
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aTenantWithNoCatalogGetsAnEmptyListRatherThanARefusal() = runTest {
        assertEquals(emptyList<ServiceListItem>(), services("[]"))
        assertEquals(emptyList<PackageListItem>(), packages("[]"))
        assertEquals(emptyList<ExtraListItem>(), extras("[]"))
    }

    /**
     * `[]` and a bodiless 204 are NOT the same answer, and separating them is the ruling T-0602 made
     * per surface. `[]` is a tenant with nothing in its catalog — no row whose price could be wrong,
     * so the picker renders empty. A 204 is the server declining to send a price list at all, and
     * defaulting that to empty is the "nothing is bookable today" failure ADR-0048 §D4 fact 4 was
     * written about: strictly worse than the coercion it looks like, and reported as Success.
     *
     * Extras go the other way on exactly this line, and only because they clear all three of B1's
     * conditions where services and packages fail every one.
     */
    @Test
    fun aBodylessCatalogRefusesWhileABodylessAddOnListDegrades() = runTest {
        refuses("ServiceListItem[]") { services("", code = 204) }
        refuses("PackageListItem[]") { packages("", code = 204) }

        assertEquals(emptyList<ExtraListItem>(), extras("", code = 204))
    }

    @Test
    fun aPackageWithNoIncludedServicesStillPrices() = runTest {
        val pkg = packages(packagesWithFirstRow { it - "includedServices" })?.first()

        assertNull(pkg?.includedServices)
        assertEquals(450.00, pkg?.price)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    @Test
    fun anUndescribedServiceStaysUndescribedRatherThanGettingAPlaceholder() = runTest {
        val service = loadedServices(servicesWithFirstRow { it - "description" }).first()

        assertNull(service.description)
        assertEquals(2900.00, service.basePrice, 0.0)
    }

    @Test
    fun anUntranslatedRowKeepsANullTranslationMap() = runTest {
        val service = loadedServices(servicesWithFirstRow { it - "translations" }).first()

        assertNull(service.translations)
        assertEquals("Standard clean", service.name)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun rowsWithFirst(body: String, transform: (JsonObject) -> JsonObject): String {
        val rows = Json.parseToJsonElement(body).jsonArray.mapIndexed { index, row ->
            if (index == 0) transform(row.jsonObject) else row
        }
        return JsonArray(rows).toString()
    }

    private fun servicesWithFirstRow(transform: (JsonObject) -> JsonObject) =
        rowsWithFirst(CAPTURED_SERVICES, transform)

    private fun packagesWithFirstRow(transform: (JsonObject) -> JsonObject) =
        rowsWithFirst(CAPTURED_PACKAGES, transform)

    private fun extrasWithFirstRow(transform: (JsonObject) -> JsonObject) =
        rowsWithFirst(CAPTURED_EXTRAS, transform)

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {

        /** Every member non-zero and non-default, translations included. */
        val CAPTURED_SERVICES = """
            [
              {
                "id": "svc-1",
                "name": "Standard clean",
                "description": "Rooms and baths",
                "category": {
                  "id": "cat-1",
                  "slug": "home",
                  "name": "Home cleaning",
                  "description": "Homes and flats",
                  "displayOrder": 3,
                  "translations": { "cs": { "name": "Uklid domacnosti", "description": "Byty a domy" } }
                },
                "basePrice": 2900.00,
                "perRoomPrice": 180.00,
                "translations": { "cs": { "name": "Bezny uklid", "description": "Pokoje a koupelny" } }
              },
              {
                "id": "svc-2",
                "name": "Deep clean",
                "description": "Everything",
                "category": {
                  "id": "cat-1",
                  "slug": "home",
                  "name": "Home cleaning",
                  "description": "Homes and flats",
                  "displayOrder": 3,
                  "translations": { "cs": { "name": "Uklid domacnosti", "description": "Byty a domy" } }
                },
                "basePrice": 4200.00,
                "perRoomPrice": 260.00,
                "translations": { "cs": { "name": "Generalni uklid", "description": "Vse" } }
              }
            ]
        """.trimIndent()

        val CAPTURED_PACKAGES = """
            [
              {
                "id": "pkg-1",
                "name": "Move-out bundle",
                "description": "Deep clean plus windows",
                "price": 450.00,
                "translations": { "cs": { "name": "Balicek pri stehovani", "description": "Vse a okna" } },
                "includedServices": [
                  { "name": "Standard clean",
                    "translations": { "cs": { "name": "Bezny uklid", "description": "Pokoje" } } }
                ]
              },
              {
                "id": "pkg-2",
                "name": "Office bundle",
                "description": "Weekly office clean",
                "price": 980.00,
                "translations": { "cs": { "name": "Kancelarsky balicek", "description": "Tydne" } },
                "includedServices": [
                  { "name": "Deep clean",
                    "translations": { "cs": { "name": "Generalni uklid", "description": "Vse" } } }
                ]
              }
            ]
        """.trimIndent()

        val CAPTURED_EXTRAS = """
            [
              {
                "id": "ext-1",
                "slug": "inside-oven",
                "name": "Inside oven",
                "description": "Degrease the oven cavity",
                "price": 300.00,
                "displayOrder": 2,
                "translations": { "cs": { "name": "Vnitrek trouby", "description": "Odmasteni" } }
              },
              {
                "id": "ext-2",
                "slug": "inside-fridge",
                "name": "Inside fridge",
                "description": "Empty and wipe the fridge",
                "price": 240.00,
                "displayOrder": 5,
                "translations": { "cs": { "name": "Vnitrek lednice", "description": "Vytreni" } }
              }
            ]
        """.trimIndent()

        val SERVICE_SPEC_PROPERTIES =
            setOf("id", "name", "description", "category", "basePrice", "perRoomPrice", "translations")

        val PACKAGE_SPEC_PROPERTIES =
            setOf("id", "name", "description", "price", "translations", "includedServices")

        val EXTRA_SPEC_PROPERTIES =
            setOf("id", "slug", "name", "description", "price", "displayOrder", "translations")
    }
}
