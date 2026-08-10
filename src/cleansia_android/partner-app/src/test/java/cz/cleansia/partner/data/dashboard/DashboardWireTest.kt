package cz.cleansia.partner.data.dashboard

import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.model.AvailableJobPreviewDto
import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto
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
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory

/**
 * The dashboard is the partner app's money front page, and no dashboard schema declares a `required`
 * array, so the generator types every property optional-with-null regardless of `nullable: false` and
 * the whole contract lands in the mapper. Everything here decodes a captured server payload over a
 * socket through the generated DTOs and the production `Json`; a mocked API hands back Kotlin objects
 * and could never notice a renamed field.
 */
class DashboardWireTest {

    private val json = Json { ignoreUnknownKeys = true; isLenient = true; explicitNulls = false }

    private fun repo(server: MockWebServer) = DashboardRepositoryImpl(
        Retrofit.Builder()
            .baseUrl(server.url("/"))
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
            .create(DashboardApi::class.java),
        json,
    )

    private suspend fun <T> serving(
        stats: String = CAPTURED_STATS,
        preview: String = CAPTURED_PREVIEW,
        onRequest: (RecordedRequest) -> Unit = {},
        block: suspend (DashboardRepositoryImpl) -> T,
    ): T {
        val server = MockWebServer()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse {
                onRequest(request)
                val body = when {
                    request.path.orEmpty().startsWith("/api/Dashboard/GetStats") -> stats
                    request.path.orEmpty().startsWith("/api/Dashboard/GetAvailableJobsPreview") -> preview
                    else -> return MockResponse().setResponseCode(404)
                }
                return MockResponse()
                    .setResponseCode(200)
                    .setHeader("Content-Type", "application/json")
                    .setBody(body)
            }
        }
        server.start()
        return try {
            block(repo(server))
        } finally {
            server.shutdown()
        }
    }

    private suspend fun loadedStats(body: String): DashboardStats {
        val result = serving(stats = body) { it.getStats(EMPLOYEE_ID) }
        assertTrue("expected the captured payload to map; got $result", result is ApiResult.Success)
        return (result as ApiResult.Success).data
    }

    private suspend fun assertStatsMappingFails(field: String, body: String) {
        val result = serving(stats = body) { it.getStats(EMPLOYEE_ID) }
        assertTrue(
            "a missing $field must fail the mapping rather than read as a default; got $result",
            result is ApiResult.Error,
        )
    }

    /**
     * The preview leg has no entry point of its own — [DashboardRepositoryImpl.refresh] owns it and
     * deliberately swallows its failures so a broken hero cannot take the dashboard down. A 200 that
     * leaves the cached preview null can therefore only be the mapper refusing the body.
     */
    private suspend fun previewSnapshot(body: String): DashboardSnapshot = serving(preview = body) {
        it.refresh(employeeId = null, force = true)
        it.snapshot.value
    }

    private suspend fun loadedPreview(body: String): AvailableJobsPreview {
        val snapshot = previewSnapshot(body)
        assertNotNull("expected the captured payload to map; got $snapshot", snapshot.availableJobsPreview)
        return snapshot.availableJobsPreview!!
    }

    // --- the field-name contract ------------------------------------------------

    @Test
    fun statsDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(STATS_SPEC_PROPERTIES, serialNames(DashboardStatsDto.serializer().descriptor))
    }

    @Test
    fun previewDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(
            PREVIEW_SPEC_PROPERTIES,
            serialNames(AvailableJobsPreviewResponse.serializer().descriptor),
        )
    }

    @Test
    fun previewJobDtoSerialNamesAreExactlyTheSpecProperties() {
        assertEquals(
            PREVIEW_JOB_SPEC_PROPERTIES,
            serialNames(AvailableJobPreviewDto.serializer().descriptor),
        )
    }

    @Test
    fun theRequestsKeepTheQueryNamesTheServerBinds() = runTest {
        val paths = mutableListOf<String>()
        serving(onRequest = { paths += it.path.orEmpty() }) {
            it.getStats(EMPLOYEE_ID)
            it.refresh(employeeId = null, force = true)
        }

        assertEquals(
            listOf(
                "/api/Dashboard/GetStats?EmployeeId=$EMPLOYEE_ID",
                "/api/Dashboard/GetStats",
                "/api/Dashboard/GetAvailableJobsPreview?Limit=5",
            ),
            paths,
        )
    }

    // --- rule 1: money is never coerced -----------------------------------------

    @Test
    fun everyStatsMoneyFieldArrivesWithItsLiteralValue() = runTest {
        val stats = loadedStats(CAPTURED_STATS)

        assertEquals(640.25, stats.todayEarnings, 0.0)
        assertEquals(4820.75, stats.weekEarnings, 0.0)
        assertEquals(18240.40, stats.lastMonthEarnings, 0.0)
        assertEquals(9315.60, stats.currentPeriodEarnings, 0.0)
    }

    @Test
    fun everyStatsCountArrivesWithItsLiteralValue() = runTest {
        val stats = loadedStats(CAPTURED_STATS)

        assertEquals(7, stats.availableOrdersCount)
        assertEquals(3, stats.myActiveOrdersCount)
        assertEquals(12, stats.thisMonthCompletedOrders)
        assertEquals(9, stats.lastMonthCompletedOrders)
        assertEquals(2, stats.todayCompletedCount)
        assertEquals(6, stats.weekCompletedCount)
        assertEquals(23, stats.ratingCount)
    }

    @Test
    fun aMissingStatsMoneyKeyFailsTheMappingRatherThanReadingZero() = runTest {
        STATS_REQUIRED_MONEY.forEach { field ->
            assertStatsMappingFails(field, withoutKey(CAPTURED_STATS, field))
        }
    }

    /**
     * A count is money's twin here: "0 jobs done this week" beside a real week total is as false a
     * sentence as a zeroed total, and the two are rendered in the same breath on both surfaces.
     */
    @Test
    fun aMissingStatsCountKeyFailsTheMappingRatherThanReadingZero() = runTest {
        STATS_REQUIRED_COUNTS.forEach { field ->
            assertStatsMappingFails(field, withoutKey(CAPTURED_STATS, field))
        }
    }

    @Test
    fun anExplicitNullStatsMoneyKeyFailsTheMappingToo() = runTest {
        assertStatsMappingFails("weekEarnings", withKey(CAPTURED_STATS, "weekEarnings", JsonNull))
    }

    @Test
    fun theHeroTotalAndCountArriveWithTheirLiteralValues() = runTest {
        val preview = loadedPreview(CAPTURED_PREVIEW)

        assertEquals(12480.75, preview.totalPotentialEarnings, 0.0)
        assertEquals(7, preview.totalAvailableCount)
    }

    @Test
    fun aMissingHeroTotalLeavesTheHeroAbsentRatherThanShowingZeroPotential() = runTest {
        val snapshot = previewSnapshot(withoutKey(CAPTURED_PREVIEW, "totalPotentialEarnings"))

        assertNull(snapshot.availableJobsPreview)
    }

    @Test
    fun aMissingHeroCountLeavesTheHeroAbsentRatherThanClaimingNoWorkIsAvailable() = runTest {
        val snapshot = previewSnapshot(withoutKey(CAPTURED_PREVIEW, "totalAvailableCount"))

        assertNull(snapshot.availableJobsPreview)
    }

    @Test
    fun aBrokenHeroDoesNotTakeTheRestOfTheDashboardDown() = runTest {
        val snapshot = previewSnapshot(withoutKey(CAPTURED_PREVIEW, "totalPotentialEarnings"))

        assertNull(snapshot.availableJobsPreview)
        assertEquals(9315.60, snapshot.stats?.currentPeriodEarnings)
        assertTrue(snapshot.loaded)
    }

    // --- rule 2: booleans follow the money rule ---------------------------------
    //
    // Neither dashboard schema carries a `nullable: false` boolean; the rule is exercised against this
    // app's payroll wires in PeriodPayWireTest and InvoicesWireTest.

    // --- rule 3: identity is refused, never synthesized --------------------------

    @Test
    fun aPreviewJobWithoutAnIdIsDroppedAndTheHeroTotalSurvives() = runTest {
        val preview = loadedPreview(previewWithFirstJob { it - "id" })

        assertEquals(listOf("job-2"), preview.jobs.map { it.id })
        assertEquals(12480.75, preview.totalPotentialEarnings, 0.0)
        assertEquals(7, preview.totalAvailableCount)
    }

    @Test
    fun aPreviewJobWithAnExplicitNullIdIsDropped() = runTest {
        val preview = loadedPreview(previewWithFirstJob { it + ("id" to JsonNull) })

        assertEquals(listOf("job-2"), preview.jobs.map { it.id })
    }

    @Test
    fun everySurvivingPreviewJobKeepsTheIdTheWireCarried() = runTest {
        val preview = loadedPreview(CAPTURED_PREVIEW)

        assertEquals(listOf("job-1", "job-2"), preview.jobs.map { it.id })
        assertEquals(1850.00, preview.jobs.first().totalPrice, 0.0)
    }

    @Test
    fun aPreviewJobWithoutItsPriceStillFailsTheWholePreview() = runTest {
        val snapshot = previewSnapshot(previewWithFirstJob { it - "totalPrice" })

        assertNull(snapshot.availableJobsPreview)
    }

    // --- rule 4: collections do default -----------------------------------------

    @Test
    fun aPreviewWithNoJobsKeyIsAnEmptyRenderableState() = runTest {
        val preview = loadedPreview(withoutKey(CAPTURED_PREVIEW, "jobs"))

        assertEquals(emptyList<AvailableJobPreview>(), preview.jobs)
        assertEquals(12480.75, preview.totalPotentialEarnings, 0.0)
    }

    @Test
    fun aPreviewWithAnExplicitNullJobsListIsAnEmptyRenderableState() = runTest {
        val preview = loadedPreview(withKey(CAPTURED_PREVIEW, "jobs", JsonNull))

        assertEquals(emptyList<AvailableJobPreview>(), preview.jobs)
    }

    // --- rule 5: nullable-by-design fields stay nullable ---------------------------

    @Test
    fun nullableByDesignStatsFieldsStayNullRatherThanBecomingPlaceholders() = runTest {
        val stats = loadedStats(
            STATS_NULLABLE_FIELDS.fold(CAPTURED_STATS) { body, key -> withoutKey(body, key) },
        )

        assertNull(stats.currentPayPeriodStart)
        assertNull(stats.currentPayPeriodEnd)
        assertNull(stats.nextPayoutDate)
        assertNull(stats.averageRating)
        assertNull(stats.latestInvoiceStatus)
        assertNull(stats.currencyCode)
        assertEquals(9315.60, stats.currentPeriodEarnings, 0.0)
    }

    @Test
    fun anUnratedCleanerKeepsANullAverageBesideARealRatingCount() = runTest {
        val stats = loadedStats(withKey(CAPTURED_STATS, "averageRating", JsonNull))

        assertNull(stats.averageRating)
        assertEquals(23, stats.ratingCount)
    }

    @Test
    fun aPreviewJobKeepsItsNullableByDesignFieldsNull() = runTest {
        val preview = loadedPreview(
            previewWithFirstJob {
                PREVIEW_JOB_NULLABLE_FIELDS.fold(it) { job, key -> job - key }
            },
        )

        val job = preview.jobs.first()
        assertNull(job.displayOrderNumber)
        assertNull(job.customerAddressApproximate)
        assertNull(job.cleaningDateTime)
        assertEquals(1850.00, job.totalPrice, 0.0)
    }

    // --- payload plumbing ---------------------------------------------------------

    private fun mutating(body: String, transform: (JsonObject) -> JsonObject): String =
        transform(Json.parseToJsonElement(body).jsonObject).toString()

    private fun withoutKey(body: String, key: String): String = mutating(body) { it - key }

    private fun withKey(body: String, key: String, value: JsonElement): String =
        mutating(body) { it + (key to value) }

    private fun previewWithFirstJob(transform: (JsonObject) -> JsonObject): String =
        mutating(CAPTURED_PREVIEW) { root ->
            val jobs = root["jobs"]!!.jsonArray.mapIndexed { index, job ->
                if (index == 0) transform(job.jsonObject) else job
            }
            root + ("jobs" to JsonArray(jobs))
        }

    private operator fun JsonObject.minus(key: String) =
        JsonObject(toMutableMap().apply { remove(key) })

    private operator fun JsonObject.plus(entry: Pair<String, JsonElement>) =
        JsonObject(toMutableMap().apply { put(entry.first, entry.second) })

    @OptIn(ExperimentalSerializationApi::class)
    private fun serialNames(descriptor: SerialDescriptor): Set<String> =
        (0 until descriptor.elementsCount).map { descriptor.getElementName(it) }.toSet()

    private companion object {
        const val EMPLOYEE_ID = "emp-7"

        /**
         * Every member non-zero and non-default — a payload that leans on defaults cannot tell a mapped
         * field from a forgotten one.
         */
        val CAPTURED_STATS = """
            {
              "availableOrdersCount": 7,
              "myActiveOrdersCount": 3,
              "thisMonthCompletedOrders": 12,
              "lastMonthCompletedOrders": 9,
              "todayEarnings": 640.25,
              "todayCompletedCount": 2,
              "weekEarnings": 4820.75,
              "weekCompletedCount": 6,
              "lastMonthEarnings": 18240.40,
              "currentPeriodEarnings": 9315.60,
              "currentPayPeriodStart": "2026-08-01T00:00:00Z",
              "currentPayPeriodEnd": "2026-08-15T23:59:59Z",
              "nextPayoutDate": "2026-08-20T00:00:00Z",
              "averageRating": 4.7,
              "ratingCount": 23,
              "latestInvoiceStatus": "Approved",
              "currencyCode": "CZK"
            }
        """.trimIndent()

        val CAPTURED_PREVIEW = """
            {
              "jobs": [
                {
                  "id": "job-1",
                  "displayOrderNumber": "ORD-2026-0101",
                  "customerAddressApproximate": "Praha 4, 140 xx",
                  "cleaningDateTime": "2026-08-12T08:00:00Z",
                  "totalPrice": 1850.00
                },
                {
                  "id": "job-2",
                  "displayOrderNumber": "ORD-2026-0102",
                  "customerAddressApproximate": "Brno-stred, 602 xx",
                  "cleaningDateTime": "2026-08-13T10:30:00Z",
                  "totalPrice": 2310.50
                }
              ],
              "totalPotentialEarnings": 12480.75,
              "totalAvailableCount": 7
            }
        """.trimIndent()

        val STATS_SPEC_PROPERTIES = setOf(
            "availableOrdersCount",
            "myActiveOrdersCount",
            "thisMonthCompletedOrders",
            "lastMonthCompletedOrders",
            "todayEarnings",
            "todayCompletedCount",
            "weekEarnings",
            "weekCompletedCount",
            "lastMonthEarnings",
            "currentPeriodEarnings",
            "currentPayPeriodStart",
            "currentPayPeriodEnd",
            "nextPayoutDate",
            "averageRating",
            "ratingCount",
            "latestInvoiceStatus",
            "currencyCode",
        )

        val PREVIEW_SPEC_PROPERTIES = setOf("jobs", "totalPotentialEarnings", "totalAvailableCount")

        val PREVIEW_JOB_SPEC_PROPERTIES = setOf(
            "id",
            "displayOrderNumber",
            "customerAddressApproximate",
            "cleaningDateTime",
            "totalPrice",
        )

        val STATS_REQUIRED_MONEY = listOf(
            "todayEarnings",
            "weekEarnings",
            "lastMonthEarnings",
            "currentPeriodEarnings",
        )

        val STATS_REQUIRED_COUNTS = listOf(
            "availableOrdersCount",
            "myActiveOrdersCount",
            "thisMonthCompletedOrders",
            "lastMonthCompletedOrders",
            "todayCompletedCount",
            "weekCompletedCount",
            "ratingCount",
        )

        val STATS_NULLABLE_FIELDS = listOf(
            "currentPayPeriodStart",
            "currentPayPeriodEnd",
            "nextPayoutDate",
            "averageRating",
            "latestInvoiceStatus",
            "currencyCode",
        )

        val PREVIEW_JOB_NULLABLE_FIELDS = listOf(
            "displayOrderNumber",
            "customerAddressApproximate",
            "cleaningDateTime",
        )
    }
}
