package cz.cleansia.partner.navigation

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * kotlinx.serialization's own R8 rule keeps a serializable object's `INSTANCE` and `serializer()` only while code reads
 * `INSTANCE`. A route no code navigates to (`NavRoute.Invoices`, `NavRoute.Profile`) is reached only through
 * `serializer(KType)`, so R8 removed both and every minified build crashed at start with "Serializer for class ... is
 * not found". The partner's rule is conditioned on the class alone. No CI job runs R8, so this test is the only guard.
 */
class RouteKeepRuleTest {

    @Test
    fun `a serializable object keeps its INSTANCE and serializer whether or not code reads INSTANCE`() {
        val rules = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
            .map { File(it, "proguard-rules.pro") }
            .firstOrNull { it.isFile }
            ?.readLines()
            ?.filterNot { it.trimStart().startsWith("#") }
            ?.joinToString(" ")
            ?.replace(Regex("""\s+"""), " ")
            ?: error("partner-app proguard-rules.pro not found from working dir ${File(".").absolutePath}")
        val objectRule = "-if @kotlinx.serialization.Serializable class ** -keepclassmembers class <1> { " +
            "public static <1> INSTANCE; kotlinx.serialization.KSerializer serializer(...); }"
        assertTrue("proguard-rules.pro lost the object rule conditioned on the class alone", rules.contains(objectRule))
    }
}
