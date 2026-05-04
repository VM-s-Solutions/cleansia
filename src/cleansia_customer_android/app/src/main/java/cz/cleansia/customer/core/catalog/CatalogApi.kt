package cz.cleansia.customer.core.catalog

import retrofit2.Response
import retrofit2.http.GET

interface CatalogApi {
    @GET("api/service/GetOverview")
    suspend fun getServices(): Response<List<ServiceListItem>>

    @GET("api/package/GetOverview")
    suspend fun getPackages(): Response<List<PackageListItem>>
}
