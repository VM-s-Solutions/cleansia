package cz.cleansia.core.ui

import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent

/** Starts [intent]; when no app on the device handles it, copies [value] instead and returns false. */
fun Context.openOrCopy(intent: Intent, value: String): Boolean = try {
    startActivity(intent)
    true
} catch (_: ActivityNotFoundException) {
    (getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager)
        ?.setPrimaryClip(ClipData.newPlainText(value, value))
    false
}
