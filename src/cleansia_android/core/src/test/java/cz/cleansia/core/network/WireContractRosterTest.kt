package cz.cleansia.core.network

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test

/**
 * ADR-0048 §D3 pins each repository with a wire test and §D7 states the cost: that roster was a
 * **written-down list**, so a repository added tomorrow with a coercing mapper was caught by nothing.
 * Here the roster is derived from the tree on every run instead — a new source that maps a generated
 * model and has no wire test beside it fails this, without anyone remembering to widen a list.
 *
 * It lives in `:core` and walks both apps because two copies of one rule is the divergence ADR-0048
 * §D5 rejects for `WireContract` itself, and because a third app would otherwise join the tree
 * ungoverned.
 */
class WireContractRosterTest {

    private data class DataLayer(val path: String, val generatedApi: String)

    private val dataLayers = listOf(
        DataLayer("customer-app/src/main/java/cz/cleansia/customer/core", "cz.cleansia.customer.api"),
        DataLayer("partner-app/src/main/java/cz/cleansia/partner/data", "cz.cleansia.partner.api"),
        // Partner's app-shared clients (consent, push) live under `core/` rather than `data/`, and
        // the roster is only closed if it follows them there.
        DataLayer("partner-app/src/main/java/cz/cleansia/partner/core", "cz.cleansia.partner.api"),
    )

    /**
     * Named, with the reason, rather than missed by a predicate: each calls a generated endpoint but
     * maps no response field into an app type, so there is no field-name contract for a wire test to
     * hold. A file added to this list is a decision visible in the diff; a file the predicate simply
     * failed to see is not, which is how the two service-area sources went unwatched until a sweep
     * found a live defect in them.
     */
    private val notMappers = setOf(
        // Registers a device token; reads only whether the call succeeded.
        "notifications/DeviceApiClient.kt",
        // Hilt graph — builds the clients, and its one body read is the token-refresh plumbing
        // pinned by RefreshClientClassificationTest.
        "network/NetworkModule.kt",
        // Reads one id off an already-refused ApiResult; owns no mapper.
        "auth/EmployeeIdResolver.kt",
        // Files an account-deletion request; the endpoint answers 200 with no body, so the client
        // returns ApiResult<Unit> and there is no field-name contract to pin. -> /decisions/adr-0052
        "gdpr/GdprDeletionClient.kt",
    )

    private val androidRoot: File =
        generateSequence(File(".").absoluteFile) { it.parentFile }
            .firstOrNull { File(it, "customer-app").isDirectory && File(it, "partner-app").isDirectory }
            ?: error("cleansia_android root not found from ${File(".").absolutePath}")

    /**
     * A source is on the roster when it names a generated **response**-side type and turns a wire
     * body into a value. `*Command` / `*Request` are the request side, which the generator types
     * optional for a different reason (T-0441) and which no mapper reads.
     *
     * The `client` package counts as well as `model`, and that is not belt-and-braces: Kotlin infers
     * the DTO type off the client method, so a file can map `dto.id` without ever importing the
     * model. Both service-area sources are written exactly that way, and keying on `model` alone
     * left them off the roster while they defaulted an unanswered read to "we serve nowhere".
     */
    private fun rosterSources(layer: DataLayer): List<File> {
        val root = File(androidRoot, layer.path)
        assertTrue("data layer not found: ${layer.path}", root.isDirectory)
        return root.walkTopDown()
            .filter { it.isFile && it.extension == "kt" }
            .filter { file ->
                val text = file.readText()
                readsAGeneratedResponseModel(text, layer.generatedApi) &&
                    producesAnAppValue(text) &&
                    notMappers.none { file.invariantSeparatorsPath.endsWith(it) }
            }
            .sortedBy { it.invariantSeparatorsPath }
            .toList()
    }

    private fun readsAGeneratedResponseModel(text: String, apiPackage: String): Boolean =
        Regex(Regex.escape(apiPackage) + "\\.(?:model|client)\\.([A-Za-z0-9_]+)")
            .findAll(text)
            .map { it.groupValues[1] }
            .any { !it.endsWith("Command") && !it.endsWith("Request") }

    private fun producesAnAppValue(text: String): Boolean =
        MAPPING_TERMS.any { it in text }

    private fun relative(file: File): String =
        file.invariantSeparatorsPath.removePrefix(androidRoot.invariantSeparatorsPath + "/")

    @Test
    fun `every source that maps a generated model is pinned by a wire test in its package`() {
        val unpinned = dataLayers.flatMap { layer ->
            rosterSources(layer).filterNot { source ->
                val testPackage = File(
                    source.invariantSeparatorsPath
                        .replace("/src/main/", "/src/test/")
                        .substringBeforeLast('/'),
                )
                testPackage.isDirectory &&
                    testPackage.listFiles().orEmpty().any { it.name.endsWith("WireTest.kt") }
            }
        }

        if (unpinned.isNotEmpty()) {
            fail(
                "these sources map a generated model with no wire test in their package — decode a " +
                    "captured payload, assert a missing non-nullable key fails the mapping, and " +
                    "assert the @SerialName set equals the spec's property set (ADR-0048 §D3): " +
                    unpinned.joinToString(", ") { relative(it) },
            )
        }
    }

    /**
     * Rule 1 of §D1, and the only limb of the deviating form that is decidable without the spec: a
     * supplied zero is never a fallback. `?: false` and `?: ""` need the schema's nullability to
     * judge, which is what the per-repository wire tests the assertion above forces into existence
     * are for.
     */
    @Test
    fun `no mapper coerces a missing number to zero`() {
        val coercions = dataLayers.flatMap { layer ->
            rosterSources(layer).flatMap { source ->
                source.readLines().withIndex()
                    .filter { (_, line) -> ZERO_COERCION.containsMatchIn(line) }
                    .map { (index, line) -> "${relative(source)}:${index + 1} — ${line.trim()}" }
            }
        }

        if (coercions.isNotEmpty()) {
            fail(
                "a null in a field the spec marks non-nullable is a renamed or broken wire field, " +
                    "never a zero — refuse it with required(\"field\") instead: " +
                    coercions.joinToString("; "),
            )
        }
    }

    /**
     * The two assertions above pass vacuously if the walk finds nothing, which is exactly what a
     * moved source root or a renamed package would do to them.
     */
    @Test
    fun `the roster is derived from sources that actually exist`() {
        dataLayers.forEach { layer ->
            assertTrue(
                "no wire-mapping source found under ${layer.path} — the walk, not the tree, is broken",
                rosterSources(layer).isNotEmpty(),
            )
        }
        assertTrue(
            "the roster collapsed to ${dataLayers.sumOf { rosterSources(it).size }} sources",
            dataLayers.sumOf { rosterSources(it).size } >= MIN_ROSTER_SOURCES,
        )
    }

    private companion object {
        val MAPPING_TERMS = listOf(
            "retrofit2.Response",
            "safeApiCall",
            "networkCall",
            "mapWire",
            ".required(",
            "toAppDto",
            "toDomain",
            // The tell for a file that reads the wire without naming a mapper at all.
            ".body()",
        )

        val ZERO_COERCION = Regex("""\?:\s*0(\.0+)?[fFlLdD]?\b|orZero\(\)""")

        const val MIN_ROSTER_SOURCES = 25
    }
}
