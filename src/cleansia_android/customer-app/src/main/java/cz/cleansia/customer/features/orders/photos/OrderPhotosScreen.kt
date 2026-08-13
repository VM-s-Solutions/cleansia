package cz.cleansia.customer.features.orders.photos

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
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
import cz.cleansia.customer.core.orders.OrderPhotoDto
import cz.cleansia.customer.core.orders.OrderPhotosResponse
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.theme.Poppins

/**
 * Gallery screen for an order's before/after photos.
 *
 * Structure:
 *   - Top bar with back button + title.
 *   - Loading / Error states as standalone centered blocks.
 *   - Loaded state: a TabRow (Before / After), then a 3-column lazy grid of
 *     square thumbnails. Tapping a thumb opens a fullscreen pager with pinch-
 *     zoom (see [FullscreenPager]).
 *
 * Notes on photoType bucketing: the backend serializes `PhotoType` as an int
 * where `1 = Before` / `2 = After`. Any photo with a null/unknown photoType
 * lands in the Before bucket — this is a pragmatic default for stale or test
 * photos so they remain reachable instead of being silently dropped.
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
                is OrderPhotosViewModel.UiState.Loaded -> GalleryContent(response = s.response)
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
private fun GalleryContent(response: OrderPhotosResponse) {
    // Backend enum is numeric — 1 = Before, 2 = After. Everything else (including
    // null) falls into the Before bucket so it stays visible.
    val before = remember(response) {
        response.photos.filter { it.photoType != 2 }
    }
    val after = remember(response) {
        response.photos.filter { it.photoType == 2 }
    }

    var tabIndex by remember { mutableIntStateOf(0) }
    var previewIndex by remember { mutableStateOf<Int?>(null) }

    Column(Modifier.fillMaxSize()) {
        TabRow(selectedTabIndex = tabIndex) {
            Tab(
                selected = tabIndex == 0,
                onClick = { tabIndex = 0; previewIndex = null },
                text = {
                    Text(
                        "${stringResource(R.string.order_photos_tab_before)} (${before.size})",
                    )
                },
            )
            Tab(
                selected = tabIndex == 1,
                onClick = { tabIndex = 1; previewIndex = null },
                text = {
                    Text(
                        "${stringResource(R.string.order_photos_tab_after)} (${after.size})",
                    )
                },
            )
        }

        val currentTab = if (tabIndex == 0) before else after
        if (currentTab.isEmpty()) {
            EmptyTabState(isBefore = tabIndex == 0)
        } else {
            LazyVerticalGrid(
                columns = GridCells.Fixed(3),
                contentPadding = PaddingValues(8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.fillMaxSize(),
            ) {
                itemsIndexed(currentTab) { index, photo ->
                    GalleryThumb(
                        photo = photo,
                        onClick = { previewIndex = index },
                    )
                }
            }
        }
    }

    val idx = previewIndex
    val photos = if (tabIndex == 0) before else after
    if (idx != null && photos.isNotEmpty()) {
        FullscreenPager(
            photos = photos,
            startIndex = idx,
            onClose = { previewIndex = null },
        )
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

