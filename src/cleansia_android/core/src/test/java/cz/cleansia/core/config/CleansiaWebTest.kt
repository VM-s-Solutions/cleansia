package cz.cleansia.core.config

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class CleansiaWebTest {
    @Test
    fun `referral links keep the existing raw code semantics`() {
        listOf("ABC123", "", "a/b?c=d&x#y", "český kód").forEach { code ->
            assertEquals("${CleansiaWeb.ORIGIN}/r/$code", CleansiaWeb.referralLink(code))
        }
    }

    @Test
    fun `rewards and home share through the helper bound to the single web origin`() {
        val androidRoot = generateSequence(File(".").absoluteFile) { it.parentFile }
            .first { File(it, "customer-app/src/main/java/cz/cleansia/customer").isDirectory }
        val customer = File(androidRoot, "customer-app/src/main/java/cz/cleansia/customer")
        val rewards = code(File(customer, "features/rewards/RewardsTab.kt"))
        val home = code(File(customer, "features/home/HomeTab.kt"))
        val config = code(File(androidRoot, "core/src/main/java/cz/cleansia/core/config/CleansiaWeb.kt"))
        val share = rewards.substringAfter("internal fun shareReferralOrFallback(")
        assertTrue(share.contains("val landingUrl = CleansiaWeb.referralLink(code)"))
        assertTrue(rewards.contains("shareReferralOrFallback(context, code, onShareUnavailable)"))
        assertTrue(home.contains("cz.cleansia.customer.features.rewards.shareReferralOrFallback("))
        assertTrue(config.contains("fun referralLink(code: String): String = \"\$ORIGIN/r/\$code\""))
    }

    private fun code(file: File) = file.readText().replace(Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*"""), "")
}
