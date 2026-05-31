package cz.cleansia.customer.features.addresses
import cz.cleansia.core.snackbar.SnackbarController

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.tween
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import cz.cleansia.customer.ui.theme.isDark
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.LocationOn
import androidx.compose.material.icons.outlined.MoreVert
import androidx.compose.material.icons.outlined.MyLocation
import androidx.compose.material.icons.outlined.Place
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import com.mapbox.geojson.Point
import com.mapbox.maps.MapboxExperimental
import com.mapbox.maps.extension.compose.MapboxMap
import com.mapbox.maps.extension.compose.animation.viewport.rememberMapViewportState
import com.mapbox.maps.extension.compose.style.MapStyle
import cz.cleansia.customer.R
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.MapStyles
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Poppins
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch

private enum class ManagerPane { List, AddOnMap, ReviewNew }

/** Prague default centre until we have permission / a selection. */
private val DEFAULT_CENTER = Point.fromLngLat(14.4378, 50.0755)
private const val DEFAULT_ZOOM = 15.0

/**
 * Full-screen Address Manager — shared UX from home top-bar, booking, and profile.
 *
 * Three panes, animated between each other:
 *  1. List  — saved addresses + "Add address" action
 *  2. AddOnMap — Mapbox picker with a top search overlay (Wolt/Bolt style)
 *  3. ReviewNew — confirm the picked address + Save/Default toggles
 *
 * [onAddressSelected] fires whenever the user taps a saved address (or confirms
 * a freshly added one). Callers that don't care about selection (e.g. profile
 * screen) can ignore it; the manager persists everything via the repository.
 */
@OptIn(MapboxExperimental::class)
@Composable
fun AddressManagerScreen(
    onBack: () -> Unit = {},
    onAddressSelected: (UserAddress) -> Unit = {},
    onMapActiveChanged: (Boolean) -> Unit = {},
    /** True when hosted inside a bottom sheet (sheet is positioned below status bar).
     *  False for full-screen navigation destinations that need to pad for the inset. */
    isInSheet: Boolean = false,
    viewModel: AddressManagerViewModel = androidx.hilt.navigation.compose.hiltViewModel(),
) {
    val context = LocalContext.current
    val repo = viewModel.addressRepository
    val locationService = viewModel.locationService
    val geocoding = viewModel.reverseGeocodingService
    val serviceArea = viewModel.serviceAreaProvider
    val snackbar = viewModel.snackbar
    val scope = rememberCoroutineScope()

    val addresses by repo.addresses.collectAsState(initial = emptyList())
    val selectedId by repo.selectedId.collectAsState(initial = null)

    var pane by remember { mutableStateOf(ManagerPane.List) }
    var pendingPicked by remember { mutableStateOf<GeocodedAddress?>(null) }

    // Tell the host (AddressManagerSheet) whenever we switch in/out of the map pane
    // so it can disable its vertical drag gesture — the map needs full control of
    // vertical pans while it's on screen.
    LaunchedEffect(pane) {
        onMapActiveChanged(pane == ManagerPane.AddOnMap)
    }

    when (pane) {
        ManagerPane.List -> ListPane(
            addresses = addresses,
            selectedId = selectedId,
            onBack = onBack,
            onAdd = { pane = ManagerPane.AddOnMap },
            onSelect = { address ->
                scope.launch { repo.setSelected(address.id) }
                onAddressSelected(address)
                onBack()
            },
            onSetDefault = { id -> scope.launch { repo.setDefault(id) } },
            onDelete = { id -> scope.launch { repo.delete(id) } },
            onRename = { id, label -> scope.launch { repo.rename(id, label) } },
            isInSheet = isInSheet,
        )
        ManagerPane.AddOnMap -> AddOnMapPane(
            geocoding = geocoding,
            serviceArea = serviceArea,
            locationService = locationService,
            snackbar = snackbar,
            onBack = { pane = ManagerPane.List },
            isInSheet = isInSheet,
            onConfirm = { picked ->
                pendingPicked = picked
                pane = ManagerPane.ReviewNew
            },
        )
        ManagerPane.ReviewNew -> {
            val picked = pendingPicked
            if (picked == null) {
                pane = ManagerPane.AddOnMap
            } else {
                ReviewPane(
                    picked = picked,
                    serviceArea = serviceArea,
                    onBack = { pane = ManagerPane.AddOnMap },
                    isInSheet = isInSheet,
                    onConfirm = { label, save, setDefault ->
                        if (save) {
                            val newAddress = picked.toUserAddress(label = label, isDefault = setDefault)
                            scope.launch {
                                repo.upsert(newAddress)
                                repo.setSelected(newAddress.id)
                                onAddressSelected(newAddress)
                                onBack()
                            }
                        } else {
                            // One-off use: don't persist, just return a transient selection.
                            val transient = picked.toUserAddress(label = "One-off", isDefault = false)
                            onAddressSelected(transient)
                            onBack()
                        }
                    },
                )
            }
        }
    }
}

/* ─────────────────────────  PANE 1 — List  ───────────────────────── */

@Composable
private fun ListPane(
    addresses: List<UserAddress>,
    selectedId: String?,
    onBack: () -> Unit,
    onAdd: () -> Unit,
    onSelect: (UserAddress) -> Unit,
    onSetDefault: (String) -> Unit,
    onDelete: (String) -> Unit,
    onRename: (String, String) -> Unit,
    isInSheet: Boolean,
) {
    var renaming by remember { mutableStateOf<UserAddress?>(null) }
    var deleting by remember { mutableStateOf<UserAddress?>(null) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        // ── Header ──
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .then(if (isInSheet) Modifier else Modifier.windowInsetsPadding(WindowInsets.statusBars))
                .padding(start = 8.dp, end = 8.dp, top = 4.dp, bottom = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back))
            }
            Text(
                stringResource(R.string.address_manager_title),
                style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp),
        ) {
            Spacer(Modifier.height(4.dp))

            if (addresses.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 40.dp),
                    contentAlignment = Alignment.Center,
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Icon(
                            Icons.Outlined.LocationOn,
                            null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(48.dp),
                        )
                        Spacer(Modifier.height(12.dp))
                        Text(
                            stringResource(R.string.address_manager_empty),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            } else {
                addresses.forEach { address ->
                    SavedAddressRow(
                        address = address,
                        isSelected = address.id == selectedId,
                        onClick = { onSelect(address) },
                        onSetDefault = { onSetDefault(address.id) },
                        onRename = { renaming = address },
                        onDelete = { deleting = address },
                    )
                    Spacer(Modifier.height(10.dp))
                }
            }

            Spacer(Modifier.height(12.dp))

            // ── Add address button ──
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(14.dp))
                    .clickable(onClick = onAdd)
                    .border(1.dp, MaterialTheme.colorScheme.primary, RoundedCornerShape(14.dp))
                    .padding(14.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(Icons.Outlined.Add, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(20.dp))
                Spacer(Modifier.width(10.dp))
                Text(
                    stringResource(R.string.address_manager_add),
                    style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.primary,
                )
            }

            Spacer(Modifier.height(32.dp))
        }
    }

    renaming?.let { target ->
        RenameDialog(
            initialLabel = target.label,
            onDismiss = { renaming = null },
            onConfirm = { newLabel ->
                onRename(target.id, newLabel)
                renaming = null
            },
        )
    }

    deleting?.let { target ->
        CleansiaDialog(
            onDismiss = { deleting = null },
            title = stringResource(R.string.address_manager_delete_title),
            message = stringResource(R.string.address_manager_delete_body, target.oneLine),
            destructive = true,
            confirmLabel = stringResource(R.string.common_delete),
            onConfirm = {
                onDelete(target.id)
                deleting = null
            },
            dismissLabel = stringResource(R.string.common_cancel),
        )
    }
}

@Composable
private fun SavedAddressRow(
    address: UserAddress,
    isSelected: Boolean,
    onClick: () -> Unit,
    onSetDefault: () -> Unit,
    onRename: () -> Unit,
    onDelete: () -> Unit,
) {
    var menuOpen by remember { mutableStateOf(false) }
    val shape = RoundedCornerShape(14.dp)

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(shape)
            .clickable(onClick = onClick)
            .background(
                color = if (isSelected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f)
                else MaterialTheme.colorScheme.surface,
                shape = shape,
            )
            .border(
                width = if (isSelected) 2.dp else 1.dp,
                color = if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                shape = shape,
            )
            .padding(start = 14.dp, end = 4.dp, top = 14.dp, bottom = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(40.dp)
                .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.6f), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.LocationOn,
                null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(20.dp),
            )
        }
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    address.label,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                if (address.isDefault) {
                    Spacer(Modifier.width(6.dp))
                    Box(
                        Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.12f))
                            .padding(horizontal = 6.dp, vertical = 2.dp),
                    ) {
                        Text(
                            stringResource(R.string.booking_address_default),
                            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }
                }
            }
            Text(
                address.oneLine,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                maxLines = 1,
            )
        }

        Box {
            IconButton(onClick = { menuOpen = true }) {
                Icon(Icons.Outlined.MoreVert, contentDescription = stringResource(R.string.address_manager_options))
            }
            DropdownMenu(
                expanded = menuOpen,
                onDismissRequest = { menuOpen = false },
            ) {
                if (!address.isDefault) {
                    DropdownMenuItem(
                        text = { Text(stringResource(R.string.address_manager_set_default)) },
                        onClick = { menuOpen = false; onSetDefault() },
                        leadingIcon = { Icon(Icons.Outlined.Star, null) },
                    )
                }
                DropdownMenuItem(
                    text = { Text(stringResource(R.string.address_manager_rename)) },
                    onClick = { menuOpen = false; onRename() },
                    leadingIcon = { Icon(Icons.Outlined.Edit, null) },
                )
                DropdownMenuItem(
                    text = {
                        Text(
                            stringResource(R.string.common_delete),
                            color = MaterialTheme.colorScheme.error,
                        )
                    },
                    onClick = { menuOpen = false; onDelete() },
                    leadingIcon = {
                        Icon(Icons.Outlined.Delete, null, tint = MaterialTheme.colorScheme.error)
                    },
                )
            }
        }
    }
}

@Composable
private fun RenameDialog(
    initialLabel: String,
    onDismiss: () -> Unit,
    onConfirm: (String) -> Unit,
) {
    var value by remember { mutableStateOf(initialLabel) }
    CleansiaDialog(
        onDismiss = onDismiss,
        title = stringResource(R.string.address_manager_rename_title),
        confirmLabel = stringResource(R.string.common_save),
        onConfirm = { if (value.isNotBlank()) onConfirm(value.trim()) },
        confirmEnabled = value.isNotBlank(),
        dismissLabel = stringResource(R.string.common_cancel),
        content = { LabelTextField(value = value, onValueChange = { value = it }) },
    )
}

/* ─────────────────────────  PANE 2 — Add on map  ───────────────────────── */

@OptIn(MapboxExperimental::class, FlowPreview::class)
@Composable
private fun AddOnMapPane(
    geocoding: ReverseGeocodingService,
    serviceArea: cz.cleansia.core.servicearea.ServiceAreaProvider,
    locationService: LocationService,
    snackbar: cz.cleansia.core.snackbar.SnackbarController,
    onBack: () -> Unit,
    isInSheet: Boolean,
    onConfirm: (GeocodedAddress) -> Unit,
) {
    val scope = rememberCoroutineScope()
    val darkTheme = isDark()
    // Pre-resolve location-failure strings — we can't call stringResource()
    // inside the coroutine that runs after the permission callback returns.
    val locationUnavailableMsg = stringResource(R.string.address_picker_my_location_unavailable)
    val locationDeniedMsg = stringResource(R.string.address_picker_my_location_denied)

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
                    // Permission granted but FusedLocation returned no fix —
                    // typically GPS off, emulator without mock location set,
                    // or first-launch with no cached location.
                    snackbar.showError(locationUnavailableMsg)
                }
            }
        } else {
            // Permission explicitly denied. Tell the user what's missing — the
            // button no longer no-ops silently.
            snackbar.showError(locationDeniedMsg)
        }
    }

    LaunchedEffect(Unit) {
        // Initial camera placement — best-effort, no snackbar feedback so we
        // don't yell at the user about location on every map open. The
        // explicit "My Location" button below reports failures.
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

    // Camera-idle reverse geocoding (debounced).
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

    // Pre-fetch the served countries once so the first search after
    // composition doesn't have to wait on a cold network call. The provider
    // caches in-memory; subsequent calls are no-ops.
    LaunchedEffect(Unit) { serviceArea.loadCountries() }

    // Search — debounced forward geocoding, biased to served countries so
    // the suggestion list can't include addresses we wouldn't be able to
    // service (e.g. addresses in Argentina silently slipping into Czech
    // bookings — see planning/active/service-areas.md).
    LaunchedEffect(searchQuery) {
        if (searchQuery.length < 2) {
            searchResults = emptyList()
            searching = false
            return@LaunchedEffect
        }
        searching = true
        delay(300)
        val isoCodes = serviceArea.servicedCountryIsoCodes()
        searchResults = geocoding.forwardGeocode(searchQuery, isoCodes)
        searching = false
    }

    Box(modifier = Modifier.fillMaxSize()) {
        // ── Map ──
        MapboxMap(
            modifier = Modifier.fillMaxSize(),
            mapViewportState = viewportState,
            style = { MapStyle(style = if (darkTheme) MapStyles.DARK else MapStyles.LIGHT) },
            scaleBar = {}, // hide Mapbox's default 0–300m scale bar overlay
        )

        // ── Centre pin ──
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center,
        ) {
            CenterPin()
        }

        // ── Top: back button + search bar overlay ──
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .then(if (isInSheet) Modifier else Modifier.windowInsetsPadding(WindowInsets.statusBars))
                .padding(start = 12.dp, end = 12.dp, top = 16.dp, bottom = 8.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                FloatingCircleButton(
                    icon = Icons.AutoMirrored.Outlined.ArrowBack,
                    contentDescription = stringResource(R.string.common_back),
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

            // Search results dropdown — only while typing + has focus.
            val showDropdown = searchFocused && searchQuery.length >= 2
            AnimatedVisibility(
                visible = showDropdown,
                enter = fadeIn() + slideInVertically { -it / 4 },
                exit = fadeOut() + slideOutVertically { -it / 4 },
            ) {
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
                        else -> LazyColumn {
                            items(searchResults.size) { idx ->
                                val r = searchResults[idx]
                                SearchResultRow(
                                    result = r,
                                    onClick = {
                                        // Move map to the chosen result.
                                        viewportState.setCameraOptions {
                                            center(Point.fromLngLat(r.longitude, r.latitude))
                                            zoom(DEFAULT_ZOOM)
                                        }
                                        resolved = r
                                        searchQuery = ""
                                        searchFocused = false
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }

        // ── Bottom: address card + actions ──
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .align(Alignment.BottomCenter)
                .shadow(elevation = 24.dp, shape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp), clip = false)
                .clip(RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp))
                .background(MaterialTheme.colorScheme.surface)
                .navigationBarsPadding()
                .padding(20.dp),
        ) {
            Text(
                text = stringResource(R.string.address_picker_title),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(
                    Icons.Outlined.LocationOn,
                    null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(22.dp),
                )
                Spacer(Modifier.width(8.dp))
                Column(modifier = Modifier.weight(1f)) {
                    val addr = resolved
                    when {
                        lookingUp -> Text(
                            stringResource(R.string.address_picker_looking_up),
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        addr != null -> {
                            Text(
                                addr.street.ifBlank { stringResource(R.string.address_picker_unnamed) },
                                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                                color = MaterialTheme.colorScheme.onSurface,
                                maxLines = 1,
                            )
                            Text(
                                listOfNotNull(
                                    addr.zipCode.takeIf { it.isNotBlank() },
                                    addr.city.takeIf { it.isNotBlank() },
                                ).joinToString(" "),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                maxLines = 1,
                            )
                        }
                        else -> Text(
                            stringResource(R.string.address_picker_move_pin),
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                if (lookingUp) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(18.dp),
                        strokeWidth = 2.dp,
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
            }

            Spacer(Modifier.height(14.dp))

            // "Use current location" (promoted — full button)
            OutlineActionButton(
                icon = Icons.Outlined.MyLocation,
                label = stringResource(R.string.address_picker_my_location),
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
            )

            Spacer(Modifier.height(10.dp))

            CleansiaPrimaryButton(
                text = stringResource(R.string.address_picker_confirm),
                onClick = { resolved?.let(onConfirm) },
                enabled = resolved != null && !lookingUp,
            )
        }
    }
}

/* ─────────────────────────  PANE 3 — Review new  ───────────────────────── */

@Composable
private fun ReviewPane(
    picked: GeocodedAddress,
    serviceArea: cz.cleansia.core.servicearea.ServiceAreaProvider,
    onBack: () -> Unit,
    isInSheet: Boolean,
    onConfirm: (label: String, save: Boolean, setDefault: Boolean) -> Unit,
) {
    var label by remember { mutableStateOf(picked.city.ifBlank { "Saved" }) }
    var save by remember { mutableStateOf(true) }
    var setDefault by remember { mutableStateOf(false) }

    // City-not-serviced inline validator. State sequence:
    //  - null  → still resolving (or required input missing)
    //  - true  → city is served, Confirm enabled
    //  - false → backend doesn't serve this city, show warning + disable Confirm
    var cityServiced by remember(picked.countryIsoCode, picked.city) { mutableStateOf<Boolean?>(null) }
    LaunchedEffect(picked.countryIsoCode, picked.city) {
        if (picked.countryIsoCode.isBlank() || picked.city.isBlank()) {
            cityServiced = null
            return@LaunchedEffect
        }
        val countries = serviceArea.loadCountries()
        val match = countries.firstOrNull {
            it.isoCode?.lowercase() == picked.countryIsoCode.lowercase()
        }
        cityServiced = match?.id?.let { serviceArea.isCityServiced(it, picked.city) } ?: false
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
            .imePadding(),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .then(if (isInSheet) Modifier else Modifier.windowInsetsPadding(WindowInsets.statusBars))
                .padding(start = 8.dp, end = 8.dp, top = 4.dp, bottom = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back))
            }
            Text(
                stringResource(R.string.address_manager_review_title),
                style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onBackground,
            )
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 8.dp),
        ) {
            // Address card
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(14.dp))
                    .background(MaterialTheme.colorScheme.surface)
                    .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(14.dp))
                    .padding(16.dp),
            ) {
                Text(
                    picked.street.ifBlank { picked.formatted },
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onSurface,
                )
                val sub = listOfNotNull(
                    picked.zipCode.takeIf { it.isNotBlank() },
                    picked.city.takeIf { it.isNotBlank() },
                    picked.country.takeIf { it.isNotBlank() },
                ).joinToString(" · ")
                if (sub.isNotBlank()) {
                    Spacer(Modifier.height(2.dp))
                    Text(sub, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }

            Spacer(Modifier.height(20.dp))

            // Label field (used as the saved address title)
            Text(
                stringResource(R.string.address_manager_label_hint),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            LabelTextField(value = label, onValueChange = { label = it })

            Spacer(Modifier.height(20.dp))

            // Save toggle — always visible
            SwitchRow(
                label = stringResource(R.string.booking_save_address),
                checked = save,
                onCheckedChange = { save = it },
            )

            // Default toggle — animated in/out based on the save state
            AnimatedVisibility(
                visible = save,
                enter = fadeIn(animationSpec = tween(220)) +
                    expandVertically(animationSpec = tween(220)),
                exit = fadeOut(animationSpec = tween(180)) +
                    shrinkVertically(animationSpec = tween(180)),
            ) {
                Column {
                    Spacer(Modifier.height(8.dp))
                    SwitchRow(
                        label = stringResource(R.string.booking_set_as_default),
                        checked = setDefault,
                        onCheckedChange = { setDefault = it },
                    )
                }
            }

            // City-not-serviced banner — only renders once the lookup completed
            // AND said "no". Soft check; backend re-validates on submit so a
            // race condition where data changed after this resolved is still
            // safe.
            if (cityServiced == false) {
                Spacer(Modifier.height(20.dp))
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(MaterialTheme.colorScheme.errorContainer)
                        .padding(horizontal = 14.dp, vertical = 12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    Icon(
                        Icons.Outlined.Info,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onErrorContainer,
                    )
                    Text(
                        text = stringResource(R.string.address_manager_city_not_serviced),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onErrorContainer,
                    )
                }
            }

            Spacer(Modifier.height(32.dp))

            CleansiaPrimaryButton(
                text = stringResource(R.string.address_manager_confirm),
                onClick = { onConfirm(label.trim().ifBlank { "Saved" }, save, setDefault) },
                // Block confirm while the lookup is still pending OR explicitly
                // failed. Null = still resolving — better to make the user wait
                // a moment than let them save a city we'll reject server-side.
                enabled = cityServiced == true,
            )

            Spacer(Modifier.height(20.dp))
        }
    }
}

/* ─────────────────────────  shared bits  ───────────────────────── */

@Composable
private fun SearchField(
    value: String,
    onValueChange: (String) -> Unit,
    onFocusChanged: (Boolean) -> Unit,
    onClear: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier = modifier
            .height(48.dp)
            .shadow(elevation = 8.dp, shape = RoundedCornerShape(24.dp), clip = false)
            .clip(RoundedCornerShape(24.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(horizontal = 14.dp),
        contentAlignment = Alignment.CenterStart,
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                Icons.Outlined.Search,
                null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Box(modifier = Modifier.weight(1f)) {
                if (value.isEmpty()) {
                    Text(
                        stringResource(R.string.address_picker_search_hint),
                        style = LocalTextStyle.current,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                BasicTextField(
                    value = value,
                    onValueChange = onValueChange,
                    singleLine = true,
                    textStyle = LocalTextStyle.current.copy(color = MaterialTheme.colorScheme.onSurface),
                    cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    modifier = Modifier
                        .fillMaxWidth()
                        .onFocusChangedCompat(onFocusChanged),
                )
            }
            if (value.isNotEmpty()) {
                Spacer(Modifier.width(6.dp))
                IconButton(
                    onClick = onClear,
                    modifier = Modifier.size(24.dp),
                ) {
                    Icon(
                        Icons.Outlined.Close,
                        null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(16.dp),
                    )
                }
            }
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
            Icons.Outlined.LocationOn,
            null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                result.street.ifBlank { result.formatted },
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                maxLines = 1,
            )
            val sub = listOfNotNull(
                result.zipCode.takeIf { it.isNotBlank() },
                result.city.takeIf { it.isNotBlank() },
            ).joinToString(" · ")
            if (sub.isNotBlank()) {
                Text(sub, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1)
            }
        }
    }
}

@Composable
private fun SearchStateRow(text: String, showProgress: Boolean) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (showProgress) {
            CircularProgressIndicator(
                modifier = Modifier.size(16.dp),
                strokeWidth = 2.dp,
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(Modifier.width(10.dp))
        }
        Text(text, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
private fun CenterPin() {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = Modifier.padding(bottom = 40.dp),
    ) {
        Box(
            modifier = Modifier
                .shadow(elevation = 12.dp, shape = CircleShape, clip = false)
                .size(44.dp)
                .clip(CircleShape)
                .background(MaterialTheme.colorScheme.primary)
                .border(3.dp, Color.White, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                Icons.Outlined.Place,
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(24.dp),
            )
        }
        Box(modifier = Modifier.height(4.dp).width(2.dp).background(Color.Black.copy(alpha = 0.2f)))
        Box(
            modifier = Modifier
                .size(width = 10.dp, height = 4.dp)
                .clip(RoundedCornerShape(50))
                .background(Color.Black.copy(alpha = 0.15f)),
        )
    }
}

@Composable
private fun FloatingCircleButton(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
) {
    Box(
        modifier = Modifier
            .shadow(elevation = 8.dp, shape = CircleShape, clip = false)
            .size(44.dp)
            .clip(CircleShape)
            .clickable(onClick = onClick)
            .background(MaterialTheme.colorScheme.surface),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            icon,
            contentDescription = contentDescription,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(22.dp),
        )
    }
}

@Composable
private fun OutlineActionButton(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .height(48.dp)
            .clip(RoundedCornerShape(24.dp))
            .clickable(onClick = onClick)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(24.dp)),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(8.dp))
        Text(
            label,
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/** Bordered single-line text field — used for the address label + rename dialog. */
@Composable
private fun LabelTextField(
    value: String,
    onValueChange: (String) -> Unit,
) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(48.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(12.dp))
            .padding(horizontal = 14.dp),
        contentAlignment = Alignment.CenterStart,
    ) {
        BasicTextField(
            value = value,
            onValueChange = onValueChange,
            singleLine = true,
            textStyle = LocalTextStyle.current.copy(color = MaterialTheme.colorScheme.onSurface),
            cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
            modifier = Modifier.fillMaxWidth(),
        )
    }
}

/** Labelled switch row — full-width tap target, switch on the right. */
@Composable
private fun SwitchRow(
    label: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .clickable { onCheckedChange(!checked) }
            .padding(vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Medium),
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        Switch(
            checked = checked,
            onCheckedChange = onCheckedChange,
            colors = SwitchDefaults.colors(
                checkedThumbColor = MaterialTheme.colorScheme.onPrimary,
                checkedTrackColor = MaterialTheme.colorScheme.primary,
                uncheckedThumbColor = MaterialTheme.colorScheme.outline,
                uncheckedTrackColor = MaterialTheme.colorScheme.surfaceVariant,
            ),
        )
    }
}

private fun Modifier.onFocusChangedCompat(listener: (Boolean) -> Unit): Modifier =
    onFocusChanged { state -> listener(state.isFocused) }

/** Map a geocoding result into a persistable user-owned address. */
private fun GeocodedAddress.toUserAddress(label: String, isDefault: Boolean): UserAddress =
    UserAddress(
        id = "addr-${System.currentTimeMillis()}",
        label = label,
        street = street.ifBlank { formatted.substringBefore(",").trim() },
        city = city,
        zipCode = zipCode,
        country = country,
        countryIsoCode = countryIsoCode,
        latitude = latitude,
        longitude = longitude,
        isDefault = isDefault,
    )
