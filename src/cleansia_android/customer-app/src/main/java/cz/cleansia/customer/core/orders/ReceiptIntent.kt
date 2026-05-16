package cz.cleansia.customer.core.orders

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import androidx.core.content.FileProvider
import java.io.File

/**
 * Result of attempting to open a downloaded receipt PDF with an external viewer.
 * Allows the caller (the composable) to translate each branch into user-facing
 * feedback without caring about the Intent plumbing.
 */
sealed interface ReceiptOpenResult {
    data object Opened : ReceiptOpenResult
    data object NoViewer : ReceiptOpenResult
    data class Error(val cause: Throwable) : ReceiptOpenResult
}

/**
 * Launch a chooser to view/share the given PDF file. The caller must have
 * previously saved the file via [OrderRepository.downloadReceipt] so the file
 * lives under `cacheDir/receipts/` — our FileProvider config
 * (`res/xml/file_paths.xml`) only grants read access to that subtree.
 *
 * Why `FLAG_ACTIVITY_NEW_TASK`: we typically launch from an application context
 * (Hilt-injected / composable's LocalContext is an activity context in practice,
 * but we keep the flag defensively so this helper is safe to call from either).
 *
 * Why `Intent.createChooser`: lets the user pick between installed PDF viewers.
 * Android silently skips the chooser if only one viewer is installed, so there's
 * no double-tap penalty.
 *
 * Why `context.packageName` over a hardcoded authority: resolves whatever
 * `applicationId` the build config uses — including the `.debug` suffix on
 * debug builds — matching the Manifest's `${applicationId}.fileprovider`.
 */
fun openReceiptPdf(context: Context, file: File): ReceiptOpenResult {
    return try {
        val authority = "${context.packageName}.fileprovider"
        val uri = FileProvider.getUriForFile(context, authority, file)
        val viewIntent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/pdf")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        val chooser = Intent.createChooser(viewIntent, null).apply {
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(chooser)
        ReceiptOpenResult.Opened
    } catch (_: ActivityNotFoundException) {
        ReceiptOpenResult.NoViewer
    } catch (t: Throwable) {
        ReceiptOpenResult.Error(t)
    }
}
