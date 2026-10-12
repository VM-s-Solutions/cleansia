package cz.cleansia.customer

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * As iOS's `check-local-config.sh` does, the build refuses a Stripe key that is not publishable in every
 * configuration, since a secret key must never ship in a client, and a release build refuses a blank one,
 * which would otherwise ship with card payment silently hidden. There is no Gradle TestKit harness here,
 * so the guards are pinned as source.
 */
class StripeKeyBuildGuardTest {

    @Test
    fun `the key is read once, and every build refuses one that is not publishable`() {
        assertEquals(
            "the key must be read in exactly one place",
            1,
            Regex("""providers\.gradleProperty\("STRIPE_PUBLISHABLE_KEY"\)""").findAll(script).count(),
        )
        val guard = script.indexOf(EVERY_BUILD_GUARD)
        assertTrue("no `$EVERY_BUILD_GUARD` in build.gradle.kts", guard >= 0)
        assertTrue(
            "the guard must sit at the top level, ahead of android {}, so it runs for every build",
            guard < script.indexOf("\nandroid {"),
        )
        assertTrue(
            "the guard must throw",
            script.substring(guard).substringBefore("\n}\n").contains("throw GradleException("),
        )
    }

    @Test
    fun `a release build refuses a blank key before its other checks`() {
        val guard = script.indexOf(RELEASE_GUARD)
        assertTrue("no `$RELEASE_GUARD` in build.gradle.kts", guard >= 0)
        assertTrue(
            "the release guard must throw",
            script.substring(guard).substringBefore("\n    }\n").contains("throw GradleException("),
        )
        val firebase = script.indexOf("val firebaseConfig =")
        assertTrue("build.gradle.kts lost its Firebase release check", firebase >= 0)
        assertTrue("the key must be checked before the Firebase config", guard < firebase)
    }

    private val script: String = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
        .map { File(it, "build.gradle.kts") }
        .firstOrNull { it.isFile && File(it.parentFile, "src/main/res").isDirectory }
        ?.readText()
        ?.replace("\r\n", "\n")
        ?.replace(Regex("""/\*[\s\S]*?\*/|(?<![:"])//[^\n]*"""), "")
        ?: error("customer-app/build.gradle.kts not found from working dir ${File(".").absolutePath}")

    private companion object {
        const val EVERY_BUILD_GUARD =
            "if (stripePublishableKey.isNotBlank() && !stripePublishableKey.startsWith(\"pk_\")) {"
        const val RELEASE_GUARD = "if (wantsRelease && stripePublishableKey.isBlank()) {"
    }
}
