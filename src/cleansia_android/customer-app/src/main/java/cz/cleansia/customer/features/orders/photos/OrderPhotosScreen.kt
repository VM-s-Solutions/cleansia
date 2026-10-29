package cz.cleansia.customer.features.orders.photos

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.statusBars
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material.icons.outlined.PhotoLibrary
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil3.compose.AsyncImage
import cz.cleansia.customer.R
import cz.cleansia.customer.features.orders.groupOrderPhotos
import cz.cleansia.customer.features.orders.photoPhaseTitle
import androidx.compose.material3.ScrollableTabRow
import cz.cleansia.customer.core.orders.OrderPhotoDto
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Poppins

/**
 * Before/after photo gallery: tabs, a square-thumbnail grid, and a fullscreen pinch-zoom pager.
 * -> /flows/execution-and-completion
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OrderPhotosScreen(
    onBack: () -> Unit,
    viewModel: OrderPhotosViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        stringResource(R.string.order_photos_title),
                        style = MaterialTheme.typography.titleMedium.copy(
                            fontFamily = Poppins,
                            fontWeight = FontWeight.SemiBold,
                        ),
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.common_back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                ),
            )
        },
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when (val s = state) {
                OrderPhotosViewModel.UiState.Loading -> LoadingView()
                OrderPhotosViewModel.UiState.Error -> ErrorView(onRetry = viewModel::refresh)
                is OrderPhotosViewModel.UiState.Loaded -> GalleryContent(s.response, viewModel.initialPhase, viewModel.initialPhotoId)
            }
        }
    }
}

@Composable
private fun LoadingView(modifier: Modifier = Modifier) {
    Box(modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
    }
}

@Composable
private fun ErrorView(onRetry: () -> Unit, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = stringResource(R.string.order_photos_error_title),
            style = MaterialTheme.typography.titleMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.SemiBold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(16.dp))
        CleansiaPrimaryButton(
            text = stringResource(R.string.order_photos_error_retry),
            onClick = onRetry,
        )
    }
}

@Composable
private fun GalleryContent(response: OrderPhotosResponse, initialPhase: Int, initialPhotoId: String?) {
    val groups = remember(response) { groupOrderPhotos(response.photos) }
    var tabIndex by remember { mutableIntStateOf(groups.indexOfFirst { it.phase == initialPhase }.coerceAtLeast(0)) }
    var previewIndex by remember { mutableStateOf(groups[tabIndex].indexOfPhoto(initialPhotoId)) }
    val selectedIndex = tabIndex.coerceIn(groups.indices)
    val current = groups[selectedIndex]
    Column(Modifier.fillMaxSize()) {
        ScrollableTabRow(selectedTabIndex = selectedIndex, edgePadding = 0.dp) {
            groups.forEachIndexed { index, group ->
                Tab(
                    selected = selectedIndex == index,
                    onClick = { tabIndex = index; previewIndex = null },
                    text = { Text(stringResource(R.string.order_photos_phase_count, photoPhaseTitle(group.phase), group.photos.size), maxLines = 1) },
                )
            }
        }
        if (current.photos.isEmpty()) {
            EmptyTabState(isBefore = current.phase == 1)
        } else {
            LazyVerticalGrid(
                columns = GridCells.Fixed(3),
                contentPadding = PaddingValues(8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.fillMaxSize(),
            ) {
                itemsIndexed(current.photos) { index, photo -> GalleryThumb(photo) { previewIndex = index } }
            }
        }
    }
    previewIndex?.takeIf { current.photos.isNotEmpty() }?.let { index ->
        FullscreenPager(photos = current.photos, startIndex = index.coerceIn(current.photos.indices), onClose = { previewIndex = null })
    }
}

@Composable
private fun GalleryThumb(photo: OrderPhotoDto, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .aspectRatio(1f)
            .clip(RoundedCornerShape(10.dp))
            .background(MaterialTheme.colorScheme.surfaceVariant)
            .clickable(onClick = onClick),
    ) {
        AsyncImage(
            model = photo.blobUrl,
            contentDescription = null,
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize(),
        )
    }
}

@Composable
private fun EmptyTabState(isBefore: Boolean) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.PhotoLibrary,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(
                if (isBefore) R.string.order_photos_empty_before
                else R.string.order_photos_empty_after,
            ),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
    }
}


@androidx.compose.ui.tooling.preview.Preview
@Composable
private fun PolishGalleryPreview() {
    val activity = androidx.compose.ui.platform.LocalContext.current as? android.app.Activity
    androidx.compose.runtime.SideEffect { activity?.actionBar?.hide() }
    cz.cleansia.customer.ui.theme.CleansiaTheme {
        androidx.compose.material3.Surface(Modifier.fillMaxSize().windowInsetsPadding(WindowInsets.statusBars)) {
            GalleryContent(
                response = OrderPhotosResponse(
                    photos = listOf(
                        1 to R.drawable.mascot_ready, 2 to R.drawable.mascot_thumbs_up,
                        3 to R.drawable.mascot_leaning, 99 to R.drawable.mascot_spray_and_cloth,
                    ).map { (phase, image) -> OrderPhotoDto(
                        id = "fixture-$phase", photoType = phase,
                        blobUrl = "android.resource://cz.cleansia.customer.debug/$image",
                    ) }, beforePhotoCount = 1, afterPhotoCount = 1,
                ), initialPhase = 1, initialPhotoId = null,
            )
        }
    }
}
