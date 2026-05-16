package cz.cleansia.partner.core.database

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import cz.cleansia.partner.core.database.dao.InvoiceDao
import cz.cleansia.partner.core.database.dao.OrderDao
import cz.cleansia.partner.core.database.dao.ProfileDao
import cz.cleansia.partner.core.database.entities.CachedInvoice
import cz.cleansia.partner.core.database.entities.CachedOrder
import cz.cleansia.partner.core.database.entities.CachedProfile

/**
 * Room database for caching data locally for offline support.
 */
@Database(
    entities = [
        CachedOrder::class,
        CachedInvoice::class,
        CachedProfile::class
    ],
    version = 1,
    exportSchema = false
)
abstract class CleansiaDatabase : RoomDatabase() {

    abstract fun orderDao(): OrderDao
    abstract fun invoiceDao(): InvoiceDao
    abstract fun profileDao(): ProfileDao

    companion object {
        private const val DATABASE_NAME = "cleansia_cache.db"

        @Volatile
        private var INSTANCE: CleansiaDatabase? = null

        fun getInstance(context: Context): CleansiaDatabase {
            return INSTANCE ?: synchronized(this) {
                val instance = Room.databaseBuilder(
                    context.applicationContext,
                    CleansiaDatabase::class.java,
                    DATABASE_NAME
                )
                    .fallbackToDestructiveMigration()
                    .build()
                INSTANCE = instance
                instance
            }
        }
    }
}
