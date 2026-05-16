package cz.cleansia.partner.core.database

import android.content.Context
import cz.cleansia.partner.core.database.dao.InvoiceDao
import cz.cleansia.partner.core.database.dao.OrderDao
import cz.cleansia.partner.core.database.dao.ProfileDao
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideDatabase(@ApplicationContext context: Context): CleansiaDatabase {
        return CleansiaDatabase.getInstance(context)
    }

    @Provides
    @Singleton
    fun provideOrderDao(database: CleansiaDatabase): OrderDao {
        return database.orderDao()
    }

    @Provides
    @Singleton
    fun provideInvoiceDao(database: CleansiaDatabase): InvoiceDao {
        return database.invoiceDao()
    }

    @Provides
    @Singleton
    fun provideProfileDao(database: CleansiaDatabase): ProfileDao {
        return database.profileDao()
    }
}
