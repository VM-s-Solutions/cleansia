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
 * non-null because the schedule list and edit screens read them directly. We
 * drop wire items missing any of those.
 */
class RecurringBookingApi(
    private val recurringBookingApi: GenRecurringBookingApi,
) {
    suspend fun getMine(): Response<List<RecurringBookingTemplateDto>> {
        val raw = recurringBookingApi.recurringBookingGetMine()
        return raw.mapBody { list -> list?.mapNotNull { it.toAppDto() }.orEmpty() }
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
        rooms = rooms ?: 0,
        bathrooms = bathrooms ?: 0,
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
