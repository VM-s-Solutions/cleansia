package cz.cleansia.customer.features.booking

import androidx.lifecycle.ViewModel
import cz.cleansia.customer.core.catalog.CatalogRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject

/**
 * Holder VM for [ServicesStep] — exposes the singleton [CatalogRepository] so
 * the step composable can observe services/packages/loading/loaded flows
 * without reaching into the Application via EntryPointAccessors. Catalog
 * cache state lives in the repo itself.
 */
@HiltViewModel
class ServicesStepViewModel @Inject constructor(
    val catalogRepository: CatalogRepository,
) : ViewModel()
