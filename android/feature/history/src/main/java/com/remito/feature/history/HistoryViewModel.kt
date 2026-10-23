package com.remito.feature.history

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.database.RateDao
import com.remito.core.database.RateHistoryEntity
import com.remito.core.network.RatesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.IOException
import java.math.BigDecimal
import java.math.RoundingMode
import javax.inject.Inject

enum class HistoryRange(
    val value: String,
    val label: Int,
    val periodLabel: Int,
) {
    ONE_DAY("1D", R.string.range_1d, R.string.period_today),
    ONE_WEEK("1W", R.string.range_1w, R.string.period_this_week),
    ONE_MONTH("1M", R.string.range_1m, R.string.period_this_month),
    SIX_MONTHS("6M", R.string.range_6m, R.string.period_six_months),
    ONE_YEAR("1Y", R.string.range_1y, R.string.period_this_year),
}

sealed interface HistoryUiState {
    data object Loading : HistoryUiState

    data class Content(
        val pair: String,
        val range: HistoryRange,
        val points: List<RateHistoryEntity>,
        val minimum: String,
        val maximum: String,
        val open: BigDecimal,
        val close: BigDecimal,
        val changePercent: BigDecimal,
        val error: String? = null,
    ) : HistoryUiState

    data class Error(
        val message: String?,
    ) : HistoryUiState
}

@HiltViewModel
class HistoryViewModel
    @Inject
    constructor(
        private val repository: RatesRepository,
        private val dao: RateDao,
        private val savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private val pair = savedStateHandle["pair"] ?: "OMR-INR"
        private val _uiState = MutableStateFlow<HistoryUiState>(HistoryUiState.Loading)
        val uiState: StateFlow<HistoryUiState> = _uiState
        private var range =
            HistoryRange.entries.firstOrNull { it.value == savedStateHandle["range"] }
                ?: HistoryRange.ONE_DAY
        private var historyObserver: Job? = null

        init {
            observeAndRefresh()
        }

        fun selectRange(selected: HistoryRange) {
            if (selected == range) return
            range = selected
            savedStateHandle["range"] = selected.value
            observeAndRefresh()
        }

        fun refresh() {
            val requestedRange = range
            viewModelScope.launch {
                try {
                    val response = repository.refreshHistory(pair, requestedRange.value)
                    if (range != requestedRange) return@launch
                    val current = _uiState.value as? HistoryUiState.Content
                    if (current != null) {
                        _uiState.value =
                            current.copy(
                                minimum = response.minimum.toPlainString(),
                                maximum = response.maximum.toPlainString(),
                                open = response.open,
                                close = response.close,
                                changePercent = changePercent(response.open, response.close),
                                error = null,
                            )
                    }
                } catch (error: IOException) {
                    if (range != requestedRange) return@launch
                    val current = _uiState.value as? HistoryUiState.Content
                    _uiState.value =
                        current?.copy(error = error.message)
                            ?: HistoryUiState.Error(error.message)
                }
            }
        }

        private fun observeAndRefresh() {
            historyObserver?.cancel()
            val selectedRange = range
            historyObserver =
                viewModelScope.launch {
                    dao.observeHistory(pair, selectedRange.value).collect { points ->
                        val rates = points.map { BigDecimal(it.rate) }
                        _uiState.value =
                            if (rates.isEmpty()) {
                                HistoryUiState.Loading
                            } else {
                                HistoryUiState.Content(
                                    pair = pair,
                                    range = selectedRange,
                                    points = points,
                                    minimum = rates.minOrNull()?.toPlainString().orEmpty(),
                                    maximum = rates.maxOrNull()?.toPlainString().orEmpty(),
                                    open = rates.first(),
                                    close = rates.last(),
                                    changePercent = changePercent(rates.first(), rates.last()),
                                )
                            }
                    }
                }
            refresh()
        }

        private fun changePercent(
            open: BigDecimal,
            close: BigDecimal,
        ): BigDecimal =
            if (open.signum() == 0) {
                BigDecimal.ZERO
            } else {
                close.subtract(open).multiply(BigDecimal("100")).divide(open, 2, RoundingMode.HALF_EVEN)
            }
    }
