package cz.cleansia.partner.features.profile

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.MyLocation
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.mapbox.geojson.Point
import com.mapbox.maps.MapboxExperimental
import com.mapbox.maps.extension.compose.MapboxMap
import com.mapbox.maps.extension.compose.animation.viewport.rememberMapViewportState
import com.mapbox.maps.extension.compose.style.MapStyle
import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.MapStyles
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.isDark
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch

/**
 * Default camera center — Prague Old Town Square. Used as the initial
 * pin position when the cleaner hasn't granted location permission yet
 * or when FusedLocation can't produce a fix.
 */
private val DEFAULT_CENTER = Point.fromLngLat(14.4378, 50.0755)
private const val DEFAULT_ZOOM = 15.0

/**
 * Full-screen Mapbox address picker for the partner Address section.
 * Mirrors the customer-app `AddOnMapPane` pattern (search box → map +
 * center pin → reverse-geocode on idle → confirm card) minus the
 * saved-address / one-off / city-served gating. The picker doesn't
 * persist anything itself — it hands the resolved [GeocodedAddress]
 * back to the launching Address section via [onConfirmed]. The Address
 * section is what calls `UpdateAddressInfo` with the picked coords.
 */
@OptIn(MapboxExperimental::class, FlowPreview::class)
@Composable
fun AddressPickerScreen(
    onBack: () -> Unit,
    onConfirmed: (GeocodedAddress) -> Unit,
    viewModel: AddressPickerViewModel = hiltViewModel(),
) {
    val geocoding = viewModel.reverseGeocodingService
    val locationService = viewModel.locationService
    val snackbar = viewModel.snackbar
    val scope = rememberCoroutineScope()
    val darkTheme = isDark()
    val locationUnavailableMsg = stringResource(R.string.address_picker_location_unavailable)
    val locationDeniedMsg = stringResource(R.string.address_picker_location_denied)

    var resolved by remember { mutableStateOf<GeocodedAddress?>(null) }
    var lookingUp by remember { mutableStateOf(false) }
    var searchQuery by remember { mutableStateOf("") }
    var searchResults by remember { mutableStateOf<List<GeocodedAddress>>(emptyList()) }
    var searching by remember { mutableStateOf(false) }
    var searchFocused by remember { mutableStateOf(false) }

    val viewportState = rememberMapViewportState {
        setCameraOptions {
            center(DEFAULT_CENTER)
            zoom(DEFAULT_ZOOM)
        }
    }

    val locationPermission = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission(),
    ) { granted ->
        if (granted) {
            scope.launch {
                val loc = locationService.getCurrentLocation()
                if (loc != null) {
                    viewportState.setCameraOptions {
                        center(Point.fromLngLat(loc.longitude, loc.latitude)); zoom(DEFAULT_ZOOM)
                    }
                } else {
                    snackbar.showError(locationUnavailableMsg)
                }
            }
        } else {
            snackbar.showError(locationDeniedMsg)
        }
    }

    LaunchedEffect(Unit) {
        // Auto-center on first open if we already have permission.
        if (locationService.hasPermission()) {
            locationService.getCurrentLocation()?.let { loc ->
                viewportState.setCameraOptions {
                    center(Point.fromLngLat(loc.longitude, loc.latitude)); zoom(DEFAULT_ZOOM)
                }
            }
        } else {
            locationPermission.launch(Manifest.permission.ACCESS_FINE_LOCATION)
        }
    }

    // Reverse-geocode whenever the camera idles, debounced 500ms so we
    // don't burn API quota on continuous pans.
    LaunchedEffect(viewportState) {
        val flow = snapshotFlow {
            viewportState.cameraState?.center?.let { it.latitude() to it.longitude() }
        }.filterNotNull().distinctUntilChanged()

        launch { flow.collect { lookingUp = true } }
        flow.debounce(500).collect { (lat, lng) ->
            resolved = geocoding.reverseGeocode(lat, lng)
            lookingUp = false
        }
    }

    // Forward search — debounced 300ms, biased to serviced countries.
    // For partner we assume CZ for now; when the partner-mobile API
    // exposes a ServiceArea provider we can switch to that. The bias
    // keeps Mapbox from returning Argentine addresses in suggestions.
    LaunchedEffect(searchQuery) {
        if (searchQuery.length < 2) {
            searchResults = emptyList()
            searching = false
            return@LaunchedEffect
        }
        searching = true
        delay(300)
        searchResults = geocoding.forwardGeocode(searchQuery, listOf("cz", "sk"))
        searching = false
    }

    Box(modifier = Modifier.fillMaxSize()) {
        MapboxMap(
            modifier = Modifier.fillMaxSize(),
            mapViewportState = viewportState,
            style = { MapStyle(style = if (darkTheme) MapStyles.DARK else MapStyles.LIGHT) },
            scaleBar = {}, // hide default 0–300m overlay
        )

        // Centre pin.
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center,
        ) {
            CenterPin()
        }

        // Top bar: back + search.
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .windowInsetsPadding(WindowInsets.statusBars)
                .padding(start = 12.dp, end = 12.dp, top = 12.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                FloatingCircleButton(
                    icon = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.back),
                    onClick = onBack,
                )
                Spacer(Modifier.width(10.dp))
                SearchField(
                    value = searchQuery,
                    onValueChange = { searchQuery = it },
                    onFocusChanged = { searchFocused = it },
                    onClear = { searchQuery = "" },
                    modifier = Modifier.weight(1f),
                )
            }

            val showDropdown = searchFocused && searchQuery.length >= 2
            AnimatedVisibility(
                visible = showDropdown,
                enter = fadeIn() + slideInVertically { -it / 4 },
                exit = fadeOut() + slideOutVertically { -it / 4 },
            ) {
                val focusManager = LocalFocusManager.current
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp)
                        .shadow(elevation = 12.dp, shape = RoundedCornerShape(16.dp), clip = false)
                        .clip(RoundedCornerShape(16.dp))
                        .background(MaterialTheme.colorScheme.surface),
                ) {
                    when {
                        searching -> SearchStateRow(
                            text = stringResource(R.string.address_picker_searching),
                            showProgress = true,
                        )
                        searchResults.isEmpty() -> SearchStateRow(
                            text = stringResource(R.string.address_picker_no_results),
                            showProgress = false,
                        )
                        else -> searchResults.forEach { result ->
                            SearchResultRow(result = result) {
                                viewportState.setCameraOptions {
                                    center(Point.fromLngLat(result.longitude, result.latitude))
                                    zoom(DEFAULT_ZOOM)
                                }
                                resolved = result
                                searchQuery = ""
                                focusManager.clearFocus()
                            }
                        }
                    }
                }
            }
        }

        // My-location FAB.
        FloatingCircleButton(
            icon = Icons.Outlined.MyLocation,
            contentDescription = stringResource(R.string.address_picker_my_location),
            onClick = {
                if (locationService.hasPermission()) {
                    scope.launch {
                        val loc = locationService.getCurrentLocation()
                        if (loc != null) {
                            viewportState.setCameraOptions {
                                center(Point.fromLngLat(loc.longitude, loc.latitude)); zoom(DEFAULT_ZOOM)
                            }
                        } else {
                            snackbar.showError(locationUnavailableMsg)
                        }
                    }
                } else {
                    locationPermission.launch(Manifest.permission.ACCESS_FINE_LOCATION)
                }
            },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(end = 16.dp, bottom = 220.dp), // sits above the confirm card
        )

        // Bottom confirm card.
        ConfirmCard(
            resolved = resolved,
            lookingUp = lookingUp,
            onConfirm = { resolved?.let(onConfirmed) },
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .navigationBarsPadding(),
        )
    }
}

@Composable
private fun ConfirmCard(
    resolved: GeocodedAddress?,
    lookingUp: Boolean,
    onConfirm: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .shadow(
                elevation = 16.dp,
                shape = RoundedCornerShape(topStart = 20.dp, topEnd = 20.dp),
            )
            .clip(RoundedCornerShape(topStart = 20.dp, topEnd = 20.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                imageVector = Icons.Outlined.Place,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
            Spacer(Modifier.width(8.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = when {
                        lookingUp -> stringResource(R.string.address_picker_looking_up)
                        resolved == null -> stringResource(R.string.address_picker_drag_to_pick)
                        else -> resolved.street.ifBlank { resolved.formatted }
                    },
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                    maxLines = 1,
                )
                if (resolved != null && !lookingUp) {
                    val parts = listOfNotNull(
                        resolved.zipCode.takeIf { it.isNotBlank() },
                        resolved.city.takeIf { it.isNotBlank() },
                        resolved.country.takeIf { it.isNotBlank() },
                    )
                    if (parts.isNotEmpty()) {
                        Text(
                            text = parts.joinToString(" · "),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            maxLines = 1,
                        )
                    }
                }
            }
            if (lookingUp) {
                CircularProgressIndicator(
                    modifier = Modifier.size(20.dp),
                    strokeWidth = 2.dp,
                    color = MaterialTheme.colorScheme.primary,
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.address_picker_confirm),
            onClick = onConfirm,
            enabled = resolved != null && !lookingUp,
        )
    }
}

@Composable
private fun CenterPin() {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Box(
            modifier = Modifier
                .size(28.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary),
            contentAlignment = Alignment.Center,
        ) {
            Box(
                modifier = Modifier
                    .size(10.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.onPrimary),
            )
        }
        Box(
            modifier = Modifier
                .width(2.dp)
                .height(14.dp)
                .background(MaterialTheme.colorScheme.primary),
        )
        Spacer(Modifier.height(24.dp)) // visually offsets so pin tip points at center
    }
}

@Composable
private fun FloatingCircleButton(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier = modifier
            .size(44.dp)
            .shadow(elevation = 8.dp, shape = CircleShape)
            .clip(CircleShape)
            .background(MaterialTheme.colorScheme.surface)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = contentDescription,
            tint = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.size(22.dp),
        )
    }
}

@Composable
private fun SearchField(
    value: String,
    onValueChange: (String) -> Unit,
    onFocusChanged: (Boolean) -> Unit,
    onClear: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier
            .height(44.dp)
            .shadow(elevation = 4.dp, shape = RoundedCornerShape(22.dp))
            .clip(RoundedCornerShape(22.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(horizontal = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = Icons.Outlined.Search,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(8.dp))
        Box(modifier = Modifier.weight(1f)) {
            if (value.isEmpty()) {
                Text(
                    text = stringResource(R.string.address_picker_search_hint),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            BasicTextField(
                value = value,
                onValueChange = onValueChange,
                singleLine = true,
                cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
                textStyle = LocalTextStyle.current.copy(
                    color = MaterialTheme.colorScheme.onSurface,
                ),
                keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                keyboardActions = KeyboardActions.Default,
                modifier = Modifier
                    .fillMaxWidth()
                    .onFocusChanged { onFocusChanged(it.isFocused) },
            )
        }
        if (value.isNotEmpty()) {
            Icon(
                imageVector = Icons.Outlined.Close,
                contentDescription = stringResource(R.string.address_picker_clear),
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier
                    .size(18.dp)
                    .clickable(onClick = onClear),
            )
        }
    }
}

@Composable
private fun SearchResultRow(result: GeocodedAddress, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = Icons.Outlined.Place,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = result.street.ifBlank { result.formatted.substringBefore(",") },
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
            )
            val secondLine = listOfNotNull(
                result.zipCode.takeIf { it.isNotBlank() },
                result.city.takeIf { it.isNotBlank() },
                result.country.takeIf { it.isNotBlank() },
            ).joinToString(" · ")
            if (secondLine.isNotBlank()) {
                Text(
                    text = secondLine,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                )
            }
        }
    }
}

@Composable
private fun SearchStateRow(text: String, showProgress: Boolean) {
    Row(
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (showProgress) {
            CircularProgressIndicator(
                modifier = Modifier.size(16.dp),
                strokeWidth = 2.dp,
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.width(12.dp))
        }
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
