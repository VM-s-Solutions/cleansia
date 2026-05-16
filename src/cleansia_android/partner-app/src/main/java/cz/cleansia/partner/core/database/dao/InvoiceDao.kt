package cz.cleansia.partner.core.database.dao

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import cz.cleansia.partner.core.database.entities.CachedInvoice
import kotlinx.coroutines.flow.Flow

@Dao
interface InvoiceDao {

    @Query("SELECT * FROM cached_invoices ORDER BY generatedAt DESC")
    fun getAllInvoices(): Flow<List<CachedInvoice>>

    @Query("SELECT * FROM cached_invoices ORDER BY generatedAt DESC LIMIT :limit OFFSET :offset")
    suspend fun getInvoicesPaged(limit: Int, offset: Int): List<CachedInvoice>

    @Query("SELECT * FROM cached_invoices WHERE id = :invoiceId")
    suspend fun getInvoiceById(invoiceId: String): CachedInvoice?

    @Query("SELECT * FROM cached_invoices WHERE id = :invoiceId")
    fun getInvoiceByIdFlow(invoiceId: String): Flow<CachedInvoice?>

    @Query("SELECT * FROM cached_invoices WHERE status = :statusValue ORDER BY generatedAt DESC")
    fun getInvoicesByStatus(statusValue: Int): Flow<List<CachedInvoice>>

    @Query("""
        SELECT * FROM cached_invoices
        WHERE invoiceNumber LIKE '%' || :searchTerm || '%'
           OR payPeriodLabel LIKE '%' || :searchTerm || '%'
        ORDER BY generatedAt DESC
    """)
    fun searchInvoices(searchTerm: String): Flow<List<CachedInvoice>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertInvoices(invoices: List<CachedInvoice>)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertInvoice(invoice: CachedInvoice)

    @Query("DELETE FROM cached_invoices WHERE id = :invoiceId")
    suspend fun deleteInvoice(invoiceId: String)

    @Query("DELETE FROM cached_invoices")
    suspend fun deleteAllInvoices()

    @Query("SELECT COUNT(*) FROM cached_invoices")
    suspend fun getInvoiceCount(): Int

    @Query("SELECT MAX(cachedAt) FROM cached_invoices")
    suspend fun getLastCacheTime(): Long?

    /**
     * Delete invoices cached before a certain time (for cache invalidation)
     */
    @Query("DELETE FROM cached_invoices WHERE cachedAt < :timestamp")
    suspend fun deleteOldInvoices(timestamp: Long)
}
