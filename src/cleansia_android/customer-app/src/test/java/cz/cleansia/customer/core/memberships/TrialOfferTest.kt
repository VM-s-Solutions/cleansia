package cz.cleansia.customer.core.memberships

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * One free trial per account: the server sends a plan's own trial days to a customer who has never had
 * one and 0 to anyone who has. A surface that offered a trial on the plan alone would promise a
 * returning customer free days and then bill them on subscribe. There is no Compose harness here, so
 * the screens' use of [trialDaysOn] is pinned by source.
 */
class TrialOfferTest {

    private fun plan(code: String, billingInterval: Int, trialDays: Int) = MembershipPlanDto(
        code = code,
        name = code,
        price = 199.0,
        monthlyEquivalentPrice = 199.0,
        billingInterval = billingInterval,
        discountPercentage = 5.0,
        freeCancellationWindowHours = 4,
        allowsExpressUpgrade = true,
        trialPeriodDays = trialDays,
        savingsPercentVsMonthly = 0.0,
        currencyCode = "CZK",
    )

    private val eligible = GetMyMembershipResponse(hasMembership = false, trialEligible = true)
    private val hadTrial = GetMyMembershipResponse(hasMembership = false, trialEligible = false)

    private val monthly = plan("plus_monthly", billingInterval = 1, trialDays = 14)
    private val yearly = plan("plus_yearly", billingInterval = 2, trialDays = 30)

    private val moduleDir: File = sequenceOf(
        File("."),
        File("customer-app"),
        File("src/cleansia_android/customer-app"),
    ).firstOrNull { File(it, "src/main/res").isDirectory }
        ?: error("customer-app not found from working dir ${File(".").absolutePath}")

    private val sourceRoot = File(moduleDir, "src/main/java/cz/cleansia/customer")

    /** The DTO that declares the plan's days and [trialDaysOn], and the wire mapper that fills them. */
    private val planDaysOwners = setOf("core/memberships/MembershipDtos.kt", "core/memberships/MembershipApi.kt")

    @Test
    fun `a customer who never had a trial is offered the plan's own days`() {
        assertEquals(14, eligible.trialDaysOn(monthly))
        assertEquals(30, eligible.trialDaysOn(yearly))
    }

    @Test
    fun `a customer who has had their trial is offered none`() {
        assertEquals(0, hadTrial.trialDaysOn(monthly))
    }

    @Test
    fun `a plan without a trial offers none`() {
        assertEquals(0, eligible.trialDaysOn(monthly.copy(trialPeriodDays = 0)))
    }

    @Test
    fun `eligibility not read yet offers none`() {
        assertEquals(0, (null as GetMyMembershipResponse?).trialDaysOn(monthly))
    }

    @Test
    fun `no plan offers none`() {
        assertEquals(0, eligible.trialDaysOn(null))
    }

    @Test
    fun `a surface without a plan picker offers the monthly plan's trial`() {
        assertEquals(14, eligible.headlineTrialDays(listOf(yearly, monthly)))
    }

    @Test
    fun `with no monthly plan the headline trial is the first plan's`() {
        assertEquals(30, eligible.headlineTrialDays(listOf(yearly)))
    }

    @Test
    fun `no plans and no eligibility offer no headline trial`() {
        assertEquals(0, eligible.headlineTrialDays(emptyList()))
        assertEquals(0, hadTrial.headlineTrialDays(listOf(monthly, yearly)))
    }

    @Test
    fun `the subscribe screen prices, labels and discloses only the trial this customer gets`() {
        val subscribe = source("features/membership/SubscribePlusScreen.kt")
        listOf(
            "val trialDays = current.trialDaysOn(selectedPlan)",
            "ctaLabel = if (trialDays > 0)",
            "disclosure = buildDisclosure(selectedPlan, trialDays)",
            "if (plan == null || trialDays <= 0) return stringResource(R.string.membership_disclosure)",
        ).forEach { binding -> assertTrue("SubscribePlusScreen lost `$binding`", subscribe.contains(binding)) }
        assertTrue(
            "the post-trial price is the one that will be charged, so it must not be struck through",
            !subscribe.contains("TextDecoration.LineThrough"),
        )
    }

    @Test
    fun `no surface reads a plan's trial days past the eligibility check`() {
        val sources = sourceRoot.walk().filter { it.isFile && it.extension == "kt" }.toList()
        assertTrue(
            "the sweep found no screens under ${sourceRoot.absolutePath}",
            sources.any { it.name == "SubscribePlusScreen.kt" },
        )
        val readers = sources
            .map { it.relativeTo(sourceRoot).invariantSeparatorsPath to it }
            .filter { (path, file) -> path !in planDaysOwners && file.readText().contains("trialPeriodDays") }
            .map { (path, _) -> path }
        assertEquals(emptyList<String>(), readers)
    }

    @Test
    fun `the home Plus slide and the profile Plus card offer the trial their view model resolved`() {
        val home = source("features/home/HomeTab.kt")
        listOf(
            "val plusTrialDays by viewModel.plusTrialDays.collectAsStateWithLifecycle()",
            "if (!isPlus) viewModel.refreshPlusPlans()",
            "plusTrialDays = plusTrialDays",
        ).forEach { binding -> assertTrue("HomeTab lost `$binding`", home.contains(binding)) }

        val card = source("features/membership/MembershipManagementCard.kt")
        listOf(
            "val offeredTrialDays by viewModel.offeredTrialDays.collectAsStateWithLifecycle()",
            "trialDays = offeredTrialDays",
        ).forEach { binding -> assertTrue("MembershipManagementCard lost `$binding`", card.contains(binding)) }
    }

    private fun source(path: String): String =
        File(sourceRoot, path)
            .also { assertTrue("$path not found at ${it.absolutePath}", it.isFile) }
            .readText()
            .replace(Regex("\\s+"), " ")
}
