package cz.cleansia.partner.data.profile

import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.PUT

/**
 * Hand-written Retrofit interface for the job-radius pair. Hand-written rather than generated
 * because the checked-in OpenAPI spec is refreshed by the owner from a running host and predates
 * both halves of this feature — same precedent as [PeriodPayApi][cz.cleansia.partner.data.payroll.PeriodPayApi].
 * It collapses into the generated client the moment the spec is dumped again.
 *
 * The read is the SAME endpoint the profile already fetches; it is repeated here only because the
 * generated `EmployeeItem` was produced before the server put `jobRadiusKm` on it, so the field is
 * dropped by the generated decoder.
 */
interface JobRadiusApi {

    @GET("api/Employee/GetCurrentEmployee")
    suspend fun getCurrentEmployeeJobRadius(): Response<JobRadiusSnapshot>

    @PUT("api/Employee/UpdateJobRadius")
    suspend fun updateJobRadius(@Body command: UpdateJobRadiusCommand): Response<UpdateJobRadiusResult>
}

/** The two fields of the employee read model this feature needs; the rest is the profile's business. */
@Serializable
data class JobRadiusSnapshot(
    val id: String? = null,
    val jobRadiusKm: Int? = null,
)

/**
 * [radiusKm] `null` asks for the country-wide board back — a choice, not an omission. The app-wide
 * `Json` runs with `explicitNulls = false`, so a cleared radius leaves as an ABSENT member and binds
 * to null on the server, which is the same `SetJobRadius(null)` path. Never substitute `0`: the
 * server takes it as a zero-kilometre radius and rejects it as out of range.
 *
 * [employeeId] is inert — the server always writes the JWT caller's row — and rides along only
 * because every other command on this controller sends it.
 */
@Serializable
data class UpdateJobRadiusCommand(
    val employeeId: String?,
    val radiusKm: Int?,
)

@Serializable
data class UpdateJobRadiusResult(
    val employeeId: String? = null,
    val radiusKm: Int? = null,
)
