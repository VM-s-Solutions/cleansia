package cz.cleansia.customer.core.memberships

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
    fun provideMembershipApi(@AuthRetrofit retrofit: Retrofit): MembershipApi =
        retrofit.create(MembershipApi::class.java)
}
