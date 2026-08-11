package cz.cleansia.customer.core.recurring

import cz.cleansia.customer.api.client.RecurringBookingApi as GenRecurringBookingApi
import cz.cleansia.customer.api.model.CreateRecurringBookingCommand as GenCreateRecurringBookingCommand
import cz.cleansia.customer.api.model.DeleteRecurringBookingCommand as GenDeleteRecurringBookingCommand
import cz.cleansia.customer.api.model.RecurringBookingTemplateDto as GenRecurringBookingTemplateDto
import cz.cleansia.customer.api.model.SetRecurringBookingActiveCommand as GenSetRecurringBookingActiveCommand
import cz.cleansia.customer.api.model.UpdateRecurringBookingCommand as GenUpdateRecurringBookingCommand
import kotlinx.datetime.Instant
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
    suspend fun getMine(): Response<List<RecurringBookingTemplateDto>> {
        val raw = recurringBookingApi.recurringBookingGetMine()
        return raw.mapBody page@{ list -> list.orEmpty().map { it.toAppDto() ?: return@page null } }
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
        return raw.mapBody { it?.toAppDto() }
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
        return raw.mapBody { it?.toAppDto() }
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

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

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
private fun GenRecurringBookingTemplateDto.toAppDto(): RecurringBookingTemplateDto? {
    val id = id ?: return null
    val frequency = frequency ?: return null
    val dayOfWeek = dayOfWeek ?: return null
    val timeOfDay = timeOfDay ?: return null
    val savedAddressId = savedAddressId ?: return null
    val paymentType = paymentType ?: return null
    val startsOn = startsOn?.toString() ?: return null
    val isActive = isActive ?: return null
    return RecurringBookingTemplateDto(
        id = id,
        frequency = frequency,
        dayOfWeek = dayOfWeek,
        timeOfDay = timeOfDay,
        rooms = rooms ?: return null,
        bathrooms = bathrooms ?: return null,
        savedAddressId = savedAddressId,
        addressLine = addressLine,
        selectedServiceIds = selectedServiceIds.orEmpty(),
        selectedPackageIds = selectedPackageIds.orEmpty(),
        paymentType = paymentType,
        startsOn = startsOn,
        endsOn = endsOn?.toString(),
        lastMaterializedFor = lastMaterializedFor?.toString(),
        isActive = isActive,
    )
}
