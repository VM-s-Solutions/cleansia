package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import cz.cleansia.customer.api.client.SavedCardApi as GenSavedCardApi
import cz.cleansia.customer.api.model.CreateSavedCardSetupIntentCommand as GenCreateSavedCardSetupIntentCommand
import cz.cleansia.customer.api.model.CreateSavedCardSetupIntentResponse as GenCreateSavedCardSetupIntentResponse
import cz.cleansia.customer.api.model.SavedCardDto as GenSavedCardDto
import java.time.YearMonth
import java.time.ZoneOffset
import retrofit2.Response

/**
 * The card a customer saves as the guarantee for cash bookings: captured through a SetupIntent, never
 * charged at capture. -> /product/business-rules
 */
data class SavedCard(
    val id: String,
    val brand: String,
    val last4: String,
    val expMonth: Int,
    val expYear: Int,
    val currencyCode: String,
)

/** The server's `SavedCard.IsUsableOn`: good through the last day of its expiry month, in UTC. */
fun SavedCard.isUsableIn(month: YearMonth): Boolean =
    expYear > month.year || (expYear == month.year && expMonth >= month.monthValue)

fun List<SavedCard>.usableIn(currencyCode: String, month: YearMonth = YearMonth.now(ZoneOffset.UTC)): SavedCard? =
    firstOrNull { it.currencyCode.equals(currencyCode, ignoreCase = true) && it.isUsableIn(month) }

/** What PaymentSheet needs in setup mode. */
data class SavedCardSetup(
    val savedCardId: String,
    val setupIntentClientSecret: String,
    val stripeCustomerId: String,
    val ephemeralKey: String,
)

class SavedCardApi(
    private val savedCardApi: GenSavedCardApi,
) {
    /**
     * Refuses the page rather than dropping a row: a card missing from this list reads as "no card
     * saved", and the booking would then capture a second card that retires the one the customer holds.
     */
    suspend fun getMine(): Response<List<SavedCard>> =
        savedCardApi.savedCardGetMine().mapWire { cards -> cards.required("SavedCardDto[]").map { it.toAppDto() } }

    suspend fun createSetupIntent(consentAccepted: Boolean, countryId: String?): Response<SavedCardSetup> =
        savedCardApi.savedCardCreateSetupIntent(
            createSavedCardSetupIntentCommand = GenCreateSavedCardSetupIntentCommand(
                consentAccepted = consentAccepted,
                countryId = countryId,
            ),
        ).mapWire { it.toAppDto() }

    suspend fun remove(savedCardId: String): Response<String> =
        savedCardApi.savedCardRemove(savedCardId).mapWire {
            it.required("RemoveSavedCardResponse").savedCardId.required("savedCardId")
        }
}

private fun GenSavedCardDto.toAppDto(): SavedCard =
    SavedCard(
        id = id.required("id"),
        brand = brand.required("brand"),
        last4 = last4.required("last4"),
        expMonth = expMonth.required("expMonth"),
        expYear = expYear.required("expYear"),
        currencyCode = currencyCode.required("currencyCode"),
    )

private fun GenCreateSavedCardSetupIntentResponse?.toAppDto(): SavedCardSetup {
    val setup = required("CreateSavedCardSetupIntentResponse")
    return SavedCardSetup(
        savedCardId = setup.savedCardId.required("savedCardId"),
        setupIntentClientSecret = setup.setupIntentClientSecret.required("setupIntentClientSecret"),
        stripeCustomerId = setup.stripeCustomerId.required("stripeCustomerId"),
        ephemeralKey = setup.ephemeralKey.required("ephemeralKey"),
    )
}
