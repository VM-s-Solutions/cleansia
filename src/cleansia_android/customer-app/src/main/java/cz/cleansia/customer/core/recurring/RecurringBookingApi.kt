package cz.cleansia.customer.core.recurring

import cz.cleansia.customer.api.client.RecurringBookingApi as GenRecurringBookingApi
import cz.cleansia.customer.api.model.CreateRecurringBookingCommand as GenCreateRecurringBookingCommand
import cz.cleansia.customer.api.model.DeleteRecurringBookingCommand as GenDeleteRecurringBookingCommand
import cz.cleansia.customer.api.model.RecurringBookingTemplateDto as GenRecurringBookingTemplateDto
import cz.cleansia.customer.api.model.SetRecurringBookingActiveCommand as GenSetRecurringBookingActiveCommand
import cz.cleansia.customer.api.model.UpdateRecurringBookingCommand as GenUpdateRecurringBookingCommand
import kotlinx.datetime.Instant
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenRecurringBookingApi]. UserId is
 * enriched server-side from the JWT NameIdentifier claim — never sent on the
 * wire.
 *
 * The hand-written [RecurringBookingTemplateDto] keeps id / frequency /
 * dayOfWeek / timeOfDay / savedAddressId / paymentType / startsOn / isActive
 * non-null because the schedule list and edit screens read them directly. A
 * wire item missing any of those refuses the list.
 */
class RecurringBookingApi(
    private val recurringBookingApi: GenRecurringBookingApi,
) {
    /**
     * The body is refused rather than defaulted to empty, and it fails ADR-0048 amendment B1's first
     * and third conditions for the same reason a dropped row does: an empty list is this screen
     * stating, as a fact, that the customer has no standing instruction to charge — while the server
     * keeps materialising orders from the one it did not manage to send. The only screen that can
     * pause or delete it would be showing its empty state. B1 needs all three conditions; the second
     * one holding (nothing sums these) does not carry it.
     */
    suspend fun getMine(): Response<List<RecurringBookingTemplateDto>> {
        val raw = recurringBookingApi.recurringBookingGetMine()
        return raw.mapWire { list -> list.required("RecurringBookingTemplateDto[]").map { it.toAppDto() } }
    }

    suspend fun create(body: CreateRecurringBookingRequest): Response<RecurringBookingTemplateDto> {
        val raw = recurringBookingApi.recurringBookingCreate(
            createRecurringBookingCommand = GenCreateRecurringBookingCommand(
                frequency = body.frequency,
                dayOfWeek = body.dayOfWeek,
                timeOfDay = body.timeOfDay,
                rooms = body.rooms,
                bathrooms = body.bathrooms,
                savedAddressId = body.savedAddressId,
                selectedServiceIds = body.selectedServiceIds,
                selectedPackageIds = body.selectedPackageIds,
                paymentType = body.paymentType,
                startsOn = Instant.parse(body.startsOn),
                endsOn = body.endsOn?.let { Instant.parse(it) },
            ),
        )
        return raw.mapWire { it.required("RecurringBookingTemplateDto").toAppDto() }
    }

    suspend fun update(body: UpdateRecurringBookingRequest): Response<RecurringBookingTemplateDto> {
        val raw = recurringBookingApi.recurringBookingUpdate(
            updateRecurringBookingCommand = GenUpdateRecurringBookingCommand(
                templateId = body.templateId,
                frequency = body.frequency,
                dayOfWeek = body.dayOfWeek,
                timeOfDay = body.timeOfDay,
                rooms = body.rooms,
                bathrooms = body.bathrooms,
                savedAddressId = body.savedAddressId,
                selectedServiceIds = body.selectedServiceIds,
                selectedPackageIds = body.selectedPackageIds,
                paymentType = body.paymentType,
                startsOn = Instant.parse(body.startsOn),
                endsOn = body.endsOn?.let { Instant.parse(it) },
            ),
        )
        return raw.mapWire { it.required("RecurringBookingTemplateDto").toAppDto() }
    }

    suspend fun setActive(body: SetRecurringBookingActiveRequest): Response<Unit> =
        recurringBookingApi.recurringBookingSetActive(
            setRecurringBookingActiveCommand = GenSetRecurringBookingActiveCommand(
                templateId = body.templateId,
                isActive = body.isActive,
            ),
        )

    suspend fun delete(body: DeleteRecurringBookingRequest): Response<Unit> =
        recurringBookingApi.recurringBookingDelete(
            deleteRecurringBookingCommand = GenDeleteRecurringBookingCommand(templateId = body.templateId),
        )
}

/**
 * Refuses the list rather than dropping the row, and the reason is not arithmetic: a template is a
 * standing instruction to charge. A silently absent one is a schedule that keeps materialising orders
 * while the only screen that can pause or delete it says it does not exist — the customer is billed
 * for a booking whose off-switch has been hidden. That is a different failure from a shorter history,
 * and it is why this list refuses where the orders list drops.
 *
 * `rooms` and `bathrooms` are refused with the rest because `CreateRecurringViewModel` copies them
 * straight into the edit form, so a coerced zero is not merely displayed — the next Update writes it
 * back and the client's invention becomes the server's record.
 */
private fun GenRecurringBookingTemplateDto.toAppDto(): RecurringBookingTemplateDto =
    RecurringBookingTemplateDto(
        id = id.required("id"),
        frequency = frequency.required("frequency"),
        dayOfWeek = dayOfWeek.required("dayOfWeek"),
        timeOfDay = timeOfDay.required("timeOfDay"),
        rooms = rooms.required("rooms"),
        bathrooms = bathrooms.required("bathrooms"),
        savedAddressId = savedAddressId.required("savedAddressId"),
        addressLine = addressLine,
        selectedServiceIds = selectedServiceIds.orEmpty(),
        selectedPackageIds = selectedPackageIds.orEmpty(),
        paymentType = paymentType.required("paymentType"),
        startsOn = startsOn.required("startsOn").toString(),
        endsOn = endsOn?.toString(),
        lastMaterializedFor = lastMaterializedFor?.toString(),
        isActive = isActive.required("isActive"),
    )
