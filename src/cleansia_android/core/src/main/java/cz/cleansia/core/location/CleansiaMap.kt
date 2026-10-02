package cz.cleansia.core.location

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Home
import androidx.compose.material3.Icon
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shape
import androidx.compose.foundation.shape.GenericShape
import androidx.compose.ui.unit.dp
import com.mapbox.maps.extension.compose.style.BooleanValue
import com.mapbox.maps.extension.compose.style.MapboxStyleComposable
import com.mapbox.maps.extension.compose.style.standard.LightPresetValue
import com.mapbox.maps.extension.compose.style.standard.MapboxStandardStyle
import com.mapbox.maps.extension.compose.style.standard.StandardStyleConfigurationState
import com.mapbox.maps.extension.compose.style.standard.ThemeValue
import kotlin.math.acos
import kotlin.math.cos
import kotlin.math.sin

/**
 * The one map look, on every map in both apps: Mapbox Standard with every point-of-interest and transit
 * label hidden, the faded theme for a muted base, no 3D objects, and the day or night light preset
 * following the app theme. Street and place names stay, so the customer can still find their street.
 *
 * The same rule iOS applies with a muted, flat MapKit configuration that excludes every POI; neither
 * platform can recolour the other's tiles, so parity is no POIs, a muted base and the same pin.
 */
@Composable
@MapboxStyleComposable
fun CleansiaMapStyle(darkTheme: Boolean) {
    val configuration = remember(darkTheme) {
        StandardStyleConfigurationState().apply {
            lightPreset = if (darkTheme) LightPresetValue.NIGHT else LightPresetValue.DAY
            theme = ThemeValue.FADED
            showPointOfInterestLabels = BooleanValue(false)
            showTransitLabels = BooleanValue(false)
            show3dObjects = BooleanValue(false)
        }
    }
    MapboxStandardStyle(standardStyleConfigurationState = configuration)
}

/** The pin's height; its tip is the bottom edge, so a centre pin is lifted by exactly this much. */
val CleansiaMapPinHeight = 50.dp

private val CleansiaMapPinWidth = 40.dp

/** The brand sky ramp iOS `CleansiaMapMarker.tint` uses: sky-600 on light maps, sky-400 on dark. */
private val PinTintLight = Color(0xFF0284C7)
private val PinTintDark = Color(0xFF38BDF8)

/**
 * The Cleansia map pin — a brand-sky teardrop with a white house — on every map surface in both apps,
 * where four differently drawn pins used to disagree. Decorative: the address it marks is always
 * written beside the map. The tip is the bottom-centre of its bounds, so a ViewAnnotation anchored
 * BOTTOM puts it on the coordinate.
 */
@Composable
fun CleansiaMapPin(darkTheme: Boolean, modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .size(width = CleansiaMapPinWidth, height = CleansiaMapPinHeight)
            .shadow(elevation = 6.dp, shape = TeardropShape, clip = false)
            .background(if (darkTheme) PinTintDark else PinTintLight, TeardropShape)
            .border(2.dp, Color.White, TeardropShape),
        contentAlignment = Alignment.TopCenter,
    ) {
        Icon(
            Icons.Filled.Home,
            contentDescription = null,
            tint = Color.White,
            // Centred on the round head: the head's radius is half the width.
            modifier = Modifier
                .padding(top = CleansiaMapPinWidth / 2 - 10.dp)
                .size(20.dp),
        )
    }
}

/** A circle on top whose two tangents meet in a point at the bottom-centre of the bounds. */
private val TeardropShape: Shape = GenericShape { size, _ ->
    val radius = size.width / 2f
    val centerX = size.width / 2f
    val centerY = radius
    val tipY = size.height
    // The angle at the centre between straight down and each tangent point.
    val alpha = acos(radius / (tipY - centerY))
    val startDegrees = Math.toDegrees((Math.PI / 2) - alpha).toFloat()
    val sweepDegrees = -(360f - 2f * Math.toDegrees(alpha.toDouble()).toFloat())
    moveTo(centerX, tipY)
    lineTo(
        centerX + radius * cos((Math.PI / 2) - alpha).toFloat(),
        centerY + radius * sin((Math.PI / 2) - alpha).toFloat(),
    )
    arcTo(
        rect = Rect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
        startAngleDegrees = startDegrees,
        sweepAngleDegrees = sweepDegrees,
        forceMoveTo = false,
    )
    close()
}
