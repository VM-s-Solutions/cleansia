package cz.cleansia.customer.core.memberships

import cz.cleansia.customer.api.client.MembershipApi as GenMembershipApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object MembershipModule {
    @Provides
    @Singleton
    fun provideGenMembershipApi(@AuthRetrofit retrofit: Retrofit): GenMembershipApi =
        retrofit.create(GenMembershipApi::class.java)

    @Provides
    @Singleton
    fun provideMembershipApi(genMembershipApi: GenMembershipApi): MembershipApi =
        MembershipApi(genMembershipApi)
}
