package cz.cleansia.core.ui.components

import android.graphics.Color as AndroidColor
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.viewinterop.AndroidView

/**
 * Renders a server-rendered HTML fragment (a legal text) in-app, in the current theme's ink.
 *
 * A `WebView` with everything off: no JavaScript, no file or content access, and every navigation
 * refused — the fragment is the platform's own markdown output, and a link inside it must not turn
 * the sheet into a browser. Styling is injected here rather than sent by the server so the text
 * follows the app's light/dark scheme.
 */
@Composable
fun HtmlContentView(
    html: String,
    modifier: Modifier = Modifier,
) {
    val ink = MaterialTheme.colorScheme.onSurface
    val accent = MaterialTheme.colorScheme.primary
    val document = remember(html, ink, accent) { HtmlDocument.wrap(html, ink, accent) }

    AndroidView(
        modifier = modifier,
        factory = { context ->
            WebView(context).apply {
                settings.javaScriptEnabled = false
                settings.allowFileAccess = false
                settings.allowContentAccess = false
                settings.setSupportZoom(false)
                isHorizontalScrollBarEnabled = false
                setBackgroundColor(AndroidColor.TRANSPARENT)
                webViewClient = object : WebViewClient() {
                    override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean = true
                }
            }
        },
        update = { view -> view.loadDataWithBaseURL(null, document, "text/html", "utf-8", null) },
    )
}

internal object HtmlDocument {

    fun wrap(fragment: String, ink: Color, accent: Color): String = """
        <!doctype html>
        <html><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
          body { margin: 0; padding: 0 4px 16px; color: ${ink.cssHex()}; background: transparent;
                 font-family: sans-serif; font-size: 15px; line-height: 1.55; }
          h1, h2, h3 { font-size: 16px; font-weight: 600; margin: 20px 0 6px; }
          p, li { margin: 0 0 10px; }
          blockquote { margin: 0 0 12px; padding: 8px 12px; border-left: 3px solid ${accent.cssHex()}; opacity: 0.85; }
          a { color: ${accent.cssHex()}; }
        </style></head>
        <body>$fragment</body></html>
    """.trimIndent()

    private fun Color.cssHex(): String = "#%06X".format(toArgb() and 0xFFFFFF)
}
