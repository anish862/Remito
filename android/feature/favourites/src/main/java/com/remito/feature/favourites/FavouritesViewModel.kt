package com.remito.feature.favourites

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.database.CachedRateEntity
import com.remito.core.network.EngagementRepository
import com.remito.core.network.RatesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

data class FavouritesUiState(
    val rates: List<CachedRateEntity> = emptyList(),
    val isLoading: Boolean = true,
    val error: String? = null,
)

@HiltViewModel
class FavouritesViewModel
    @Inject
    constructor(
        private val engagementRepository: EngagementRepository,
        private val ratesRepository: RatesRepository,
    ) : ViewModel() {
        private val _uiState = MutableStateFlow(FavouritesUiState())
        val uiState: StateFlow<FavouritesUiState> = _uiState

        init {
            viewModelScope.launch {
                combine(
                    engagementRepository.observeFavourites(),
                    ratesRepository.observeRates("OMR"),
                ) { favourites, rates ->
                    val codes = favourites.filter { it.type == "currency" }.map { it.targetId }.toSet()
                    rates.filter { it.pair.substringAfter('/') in codes }
                }.collect { rates ->
                    _uiState.value = _uiState.value.copy(rates = rates, isLoading = false)
                }
            }
            refresh()
        }

        fun refresh() {
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(error = null, isLoading = _uiState.value.rates.isEmpty())
                try {
                    val favourites = engagementRepository.refreshFavourites()
                    val currencies =
                        favourites
                            .filter { it.type.name == "CURRENCY" }
                            .mapNotNull { it.targetCode }
                            .distinct()
                            .filter { it != "OMR" }
                    if (currencies.isNotEmpty()) ratesRepository.refreshRates("OMR", currencies)
                    _uiState.value = _uiState.value.copy(isLoading = false)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message ?: "Could not refresh favourites.",
                        )
                }
            }
        }

        fun removeCurrency(code: String) {
            viewModelScope.launch {
                try {
                    engagementRepository.toggleCurrencyFavourite(code)
                    _uiState.value = _uiState.value.copy(error = null)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value = _uiState.value.copy(error = error.message ?: "Could not update favourites.")
                }
            }
        }
    }
