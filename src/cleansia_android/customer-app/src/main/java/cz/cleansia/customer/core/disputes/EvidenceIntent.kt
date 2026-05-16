package cz.cleansia.customer.core.disputes

import android.content.Context
import cz.cleansia.customer.core.orders.ReceiptOpenResult
import cz.cleansia.customer.core.orders.openReceiptPdf
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File

/**
 * Download a SAS-signed evidence blob to `{cacheDir}/dispute-evidence/{evidenceId}.{ext}`
 * then hand it to [openReceiptPdf] (which fires `Intent.ACTION_VIEW` via the
 * shared [androidx.core.content.FileProvider]).
 *
 * Returns the same [ReceiptOpenResult] sealed type (Opened / NoViewer / Error)
 * so the UI layer can react with the same snackbar messages used for receipt
 * PDFs. The screen layer is responsible for translating the result into a
 * snackbar — keeps this helper Compose-free.
 *
 * Network call uses OkHttp directly — no auth header needed; the SAS URL
 * carries its own access grant. 1h TTL means stale opens fall through to a
 * 403 from Azure, which manifests as [ReceiptOpenResult.Error] to the caller.
 *
 * Threading: forces the IO dispatcher internally via [withContext] so callers
 * (VM coroutine scopes) don't have to remember to switch off Main. Compose
 * composables MUST NOT call this directly — route through a VM coroutine.
 *
 * Cache strategy: if the file already exists with non-zero length we skip the
 * download. Doesn't validate freshness vs the SAS — the same evidenceId always
 * points at the same blob, so a cache hit is always correct.
 */
suspend fun openEvidencePdfFromUrl(
    context: Context,
    evidenceId: String,
    fileName: String,
    blobUrl: String,
    httpClient: OkHttpClient = OkHttpClient(),
): ReceiptOpenResult = withContext(Dispatchers.IO) {
    try {
        val dir = File(context.cacheDir, "dispute-evidence").apply { mkdirs() }
        val ext = fileName.substringAfterLast('.', "pdf")
        val file = File(dir, "$evidenceId.$ext")
        if (!file.exists() || file.length() == 0L) {
            val request = Request.Builder().url(blobUrl).build()
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    return@withContext ReceiptOpenResult.Error(
                        IllegalStateException("HTTP ${response.code}"),
                    )
                }
                val body = response.body
                    ?: return@withContext ReceiptOpenResult.Error(
                        IllegalStateException("Empty body"),
                    )
                body.byteStream().use { input ->
                    file.outputStream().use { output -> input.copyTo(output) }
                }
            }
        }
        // Hand off to the FileProvider-backed Intent launcher. Note the
        // launcher itself touches Activity APIs (startActivity) so the
        // Context passed in must be capable of starting an activity — in
        // practice we receive the composable's LocalContext, which is the
        // MainActivity, so this is fine.
        openReceiptPdf(context, file)
    } catch (t: Throwable) {
        ReceiptOpenResult.Error(t)
    }
}
