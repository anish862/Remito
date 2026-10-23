package com.remito.feature.convert

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.common.UserSettings
import com.remito.core.network.ConversionQuoteDto
import com.remito.core.network.RatesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

sealed interface ConvertUiState {
    data class Content(
        val amount: String = "",
        val from: String = "OMR",
        val to: String = "INR",
        val currencies: List<String> = listOf("OMR", "INR", "AED", "USD", "EUR", "GBP"),
        val result: ConversionQuoteDto? = null,
        val isLoading: Boolean = false,
        val isOffline: Boolean = false,
        val error: String? = null,
        val errorResource: Int? = null,
    ) : ConvertUiState
}

@HiltViewModel
class ConvertViewModel
    @Inject
    constructor(
        private val repository: RatesRepository,
        userSettings: UserSettings,
        private val savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private companion object {
            const val DEBOUNCE_MILLIS = 300L
        }

        private val _uiState =
            MutableStateFlow(
                ConvertUiState.Content(
                    amount = savedStateHandle["amount"] ?: "1",
                    from = savedStateHandle["from"] ?: userSettings.defaultCorridor().substringBefore('/'),
                    to = savedStateHandle["to"] ?: userSettings.defaultCorridor().substringAfter('/'),
                ),
            )
        val uiState: StateFlow<ConvertUiState> = _uiState
        private var conversionJob: Job? = null

        init {
            calculate()
        }

        fun setAmount(amount: String) {
            savedStateHandle["amount"] = amount
            _uiState.value = _uiState.value.copy(amount = amount)
            calculate()
        }

        fun selectFrom(currency: String) {
            savedStateHandle["from"] = currency
            _uiState.value = _uiState.value.copy(from = currency)
            calculate()
        }

        fun selectTo(currency: String) {
            savedStateHandle["to"] = currency
            _uiState.value = _uiState.value.copy(to = currency)
            calculate()
        }

        fun swap() {
            val current = _uiState.value
            savedStateHandle["from"] = current.to
            savedStateHandle["to"] = current.from
            _uiState.value = current.copy(from = current.to, to = current.from)
            calculate()
        }

        fun retry() = calculate()

        private fun calculate() {
            conversionJob?.cancel()
            val current = _uiState.value
            val amount = current.amount.toBigDecimalOrNull()
            if (amount == null || amount.signum() <= 0 || current.from == current.to) {
                _uiState.value =
                    current.copy(
                        result = null,
                        isLoading = false,
                        isOffline = false,
                        error = null,
                        errorResource = null,
                    )
                return
            }
            conversionJob =
                viewModelScope.launch {
                    delay(DEBOUNCE_MILLIS)
                    _uiState.value = _uiState.value.copy(isLoading = true, error = null, errorResource = null)
                    try {
                        val quote = repository.convert(amount, current.from, current.to)
                        _uiState.value =
                            _uiState.value.copy(
                                result = quote,
                                isLoading = false,
                                isOffline = quote.isStale,
                            )
                    } catch (error: IOException) {
                        val cached = repository.cachedConversionQuote(amount, current.from, current.to)
                        _uiState.value =
                            if (cached != null) {
                                _uiState.value.copy(result = cached, isLoading = false, isOffline = true, error = null)
                            } else {
                                _uiState.value.copy(
                                    result = null,
                                    isLoading = false,
                                    isOffline = false,
                                    error = error.message,
                                    errorResource = if (error.message == null) R.string.unexpected_error else null,
                                )
                            }
                    } catch (error: NumberFormatException) {
                        _uiState.value =
                            _uiState.value.copy(
                                isLoading = false,
                                error = error.message,
                                errorResource = if (error.message == null) R.string.unexpected_error else null,
                            )
                    }
                }
        }
    }
