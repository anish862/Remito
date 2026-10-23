package com.remito.feature.dashboard

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.database.FavouriteEntity
import com.remito.core.database.RateDao
import com.remito.core.network.RatesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

data class DashboardRate(
    val pair: String,
    val rate: String,
    val change24HoursPercent: String,
    val lastUpdated: String,
    val isStale: Boolean,
    val isFavourite: Boolean,
)

sealed interface DashboardUiState {
    data object Loading : DashboardUiState

    data class Content(
        val rates: List<DashboardRate>,
        val stale: Boolean,
        val isRefreshing: Boolean,
        val query: String = "",
        val error: String? = null,
    ) : DashboardUiState

    data class Error(
        val message: String?,
    ) : DashboardUiState
}

@HiltViewModel
class DashboardViewModel
    @Inject
    constructor(
        private val repository: RatesRepository,
        private val dao: RateDao,
        private val savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private val baseCurrency = savedStateHandle["base"] ?: "OMR"
        private val _uiState = MutableStateFlow<DashboardUiState>(DashboardUiState.Loading)
        val uiState: StateFlow<DashboardUiState> = _uiState
        private var allRates: List<DashboardRate> = emptyList()

        init {
            viewModelScope.launch {
                combine(repository.observeRates(baseCurrency), dao.observeFavourites()) { rates, favourites ->
                    val favouriteIds = favourites.map { it.targetId }.toSet()
                    allRates =
                        rates.map {
                            DashboardRate(
                                it.pair,
                                it.rate,
                                it.change24HoursPercent,
                                it.lastUpdated,
                                it.isStale,
                                it.pair in favouriteIds,
                            )
                        }
                }.collect { rates ->
                    val previous = _uiState.value as? DashboardUiState.Content
                    _uiState.value =
                        DashboardUiState.Content(
                            rates = allRates.filter { it.pair.contains(previous?.query.orEmpty(), ignoreCase = true) },
                            stale = allRates.any { it.isStale },
                            isRefreshing = previous?.isRefreshing ?: false,
                            query = savedStateHandle["query"] ?: previous?.query.orEmpty(),
                            error = previous?.error,
                        )
                }
            }
            refresh()
        }

        fun setQuery(query: String) {
            savedStateHandle["query"] = query
            val current = _uiState.value as? DashboardUiState.Content ?: return
            _uiState.value =
                current.copy(
                    query = query,
                    rates = allRates.filter { it.pair.contains(query, ignoreCase = true) },
                )
        }

        fun toggleFavourite(pair: String) {
            viewModelScope.launch {
                if (dao.getFavourite(pair) != null) {
                    dao.removeFavourite(pair)
                } else {
                    dao.upsertFavourite(FavouriteEntity(pair, "corridor"))
                }
            }
        }

        fun refresh() {
            val current = _uiState.value as? DashboardUiState.Content
            _uiState.value = current?.copy(isRefreshing = true, error = null) ?: DashboardUiState.Loading
            viewModelScope.launch {
                try {
                    repository.refreshRates(baseCurrency, listOf("INR", "AED", "USD", "EUR", "GBP"))
                    val latest = _uiState.value as? DashboardUiState.Content
                    if (latest != null) {
                        _uiState.value = latest.copy(isRefreshing = false, error = null)
                    }
                } catch (error: IOException) {
                    repository.markCachedRatesStale()
                    val latest = _uiState.value as? DashboardUiState.Content
                    _uiState.value =
                        latest?.copy(isRefreshing = false, stale = true, error = error.message)
                            ?: DashboardUiState.Error(error.message)
                }
            }
        }
    }
