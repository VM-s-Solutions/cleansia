package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A reserved offer quotes what one seat of the job pays this cleaner, as the board and the job's header
 * do, and nothing when the server has no pay to quote. There is no Compose harness in this module, so
 * the call site is pinned as source.
 */
class PendingOffersCallSiteTest {

    private val screen: String = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/orders/PendingOffersScreen.kt") }
        .first { it.isFile }
        .readText()
        .replace("\r\n", "\n")

    @Test
    fun `a reserved offer shows the cleaner's pay, and nothing when there is none`() {
        assertTrue(
            "the offer card no longer prints only a pay above zero, in the offer's currency",
            Regex(
                """offer\.estimatedCleanerPay\?\.takeIf \{ it > 0\.0 \}\?\.let \{ pay ->\s+Text\(\s+""" +
                    """text = formatOrderPrice\(pay, offer\.currencyCode\),""",
            ).containsMatchIn(screen),
        )
    }
}
