package com.remito.feature.compare

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.network.ExchangeOfferDto
import com.remito.core.network.RedirectResponseDto
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.launch
import java.io.IOException
import java.math.BigDecimal
import javax.inject.Inject

enum class CompareSort {
    BEST_AMOUNT,
    BEST_RATE,
    HAS_APP,
}

sealed interface CompareUiState {
    data class Content(
        val from: String = "OMR",
        val to: String = "INR",
        val amount: String = "100",
        val currencies: List<String> = listOf("OMR", "INR", "AED", "USD", "EUR", "GBP"),
        val offers: List<ExchangeOfferDto> = emptyList(),
        val visibleOffers: List<ExchangeOfferDto> = emptyList(),
        val search: String = "",
        val sort: CompareSort = CompareSort.BEST_AMOUNT,
        val isLoading: Boolean = true,
        val error: String? = null,
        val disclaimer: String = "",
        val selectedDetails: ExchangeOfferDto? = null,
        val confirmationOffer: ExchangeOfferDto? = null,
        val isRedirecting: Boolean = false,
        val redirectError: String? = null,
    ) : CompareUiState {
        val isAmountValid: Boolean get() = validAmount(amount) != null
        val hasValidQuery: Boolean get() = isAmountValid && from != to
        val allOffersStale: Boolean get() = offers.isNotEmpty() && offers.all(ExchangeOfferDto::isStale)
    }
}

sealed interface CompareEffect {
    data class OpenRedirect(
        val response: RedirectResponseDto,
        val hasApp: Boolean,
    ) : CompareEffect
}

fun interface RedirectAnalytics {
    fun track(event: RedirectEvent)
}

enum class RedirectEvent {
    STARTED,
    RETURNED,
}

@HiltViewModel
@Suppress("TooManyFunctions")
class CompareViewModel
    @Inject
    constructor(
        private val repository: ComparisonRepository,
        private val analytics: RedirectAnalytics,
        private val savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private val _uiState =
            MutableStateFlow(
                CompareUiState.Content(
                    from = savedStateHandle["from"] ?: "OMR",
                    to = savedStateHandle["to"] ?: "INR",
                    amount = savedStateHandle["amount"] ?: "100",
                ),
            )
        val uiState: StateFlow<CompareUiState> = _uiState

        private val _effects = MutableSharedFlow<CompareEffect>(extraBufferCapacity = 1)
        val effects: SharedFlow<CompareEffect> = _effects
        private var cacheJob: Job? = null
        private var comparisonJob: Job? = null
        private var awaitingReturn = false

        init {
            observeCurrentCache()
            refresh()
        }

        fun setFrom(currency: String) {
            updateCorridor(from = currency)
        }

        fun setTo(currency: String) {
            updateCorridor(to = currency)
        }

        fun setAmount(amount: String) {
            val current = content() ?: return
            _uiState.value =
                current.withOffers {
                    copy(
                        amount = amount,
                        offers = emptyList(),
                        isLoading = validAmount(amount) != null && from != to,
                        error = null,
                    )
                }
            savedStateValue("amount", amount)
            observeCurrentCache()
            refresh(debounce = true)
        }

        fun setSearch(search: String) {
            val current = content() ?: return
            _uiState.value = current.withOffers { copy(search = search) }
        }

        fun setSort(sort: CompareSort) {
            val current = content() ?: return
            _uiState.value = current.withOffers { copy(sort = sort) }
        }

        fun showDetails(offer: ExchangeOfferDto) {
            content()?.let { _uiState.value = it.copy(selectedDetails = offer) }
        }

        fun dismissDetails() {
            content()?.let { _uiState.value = it.copy(selectedDetails = null) }
        }

        fun requestRedirect(offer: ExchangeOfferDto) {
            val current = content() ?: return
            if (validAmount(current.amount) == null || current.isRedirecting) return
            _uiState.value = current.copy(confirmationOffer = offer, redirectError = null)
        }

        fun dismissConfirmation() {
            content()?.let {
                if (!it.isRedirecting) _uiState.value = it.copy(confirmationOffer = null)
            }
        }

        @Suppress("ReturnCount")
        fun confirmRedirect() {
            val current = content() ?: return
            val offer = current.confirmationOffer ?: return
            val amount = validAmount(current.amount) ?: return
            if (current.isRedirecting) return
            _uiState.value = current.copy(isRedirecting = true, redirectError = null)
            viewModelScope.launch {
                try {
                    val response = repository.redirect(offer, current.from, current.to, amount)
                    analytics.track(RedirectEvent.STARTED)
                    _uiState.value =
                        (content() ?: current).copy(
                            confirmationOffer = null,
                            isRedirecting = false,
                        )
                    _effects.emit(CompareEffect.OpenRedirect(response, offer.hasApp))
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        (content() ?: current).copy(
                            isRedirecting = false,
                            redirectError = error.message,
                        )
                }
            }
        }

        fun onDestinationLaunched(
            success: Boolean,
            failureMessage: String,
        ) {
            if (success) {
                awaitingReturn = true
            } else {
                content()?.let {
                    _uiState.value = it.copy(redirectError = failureMessage)
                }
            }
        }

        fun onAppResumed() {
            if (awaitingReturn) {
                awaitingReturn = false
                analytics.track(RedirectEvent.RETURNED)
            }
        }

        fun refresh() = refresh(debounce = false)

        private fun updateCorridor(
            from: String? = null,
            to: String? = null,
        ) {
            val current = content() ?: return
            val nextFrom = from ?: current.from
            val nextTo = to ?: current.to
            savedStateValue("from", nextFrom)
            savedStateValue("to", nextTo)
            _uiState.value =
                current.withOffers {
                    copy(
                        from = nextFrom,
                        to = nextTo,
                        offers = emptyList(),
                        isLoading = nextFrom != nextTo && validAmount(current.amount) != null,
                        error = null,
                        selectedDetails = null,
                        confirmationOffer = null,
                    )
                }
            observeCurrentCache()
            refresh(debounce = true)
        }

        private fun observeCurrentCache() {
            val current = content() ?: return
            val amount =
                validAmount(current.amount)
                    ?: run {
                        cacheJob?.cancel()
                        cacheJob = null
                        return
                    }
            cacheJob?.cancel()
            cacheJob =
                viewModelScope.launch {
                    repository
                        .observeCached("${current.from}-${current.to}", amount.toPlainString())
                        .collect { cached ->
                            val latest = content() ?: return@collect
                            if (latest.from == current.from &&
                                latest.to == current.to &&
                                validAmount(latest.amount) == amount
                            ) {
                                _uiState.value =
                                    latest.withOffers {
                                        copy(
                                            offers = cached,
                                            isLoading = latest.isLoading && cached.isEmpty(),
                                        )
                                    }
                            }
                        }
                }
        }

        private fun refresh(debounce: Boolean = false) {
            val current = content() ?: return
            comparisonJob?.cancel()
            comparisonJob =
                viewModelScope.launch {
                    if (debounce) delay(COMPARISON_DEBOUNCE_MILLIS)
                    val latest = content() ?: return@launch
                    val amount = validAmount(latest.amount)
                    if (amount == null || latest.from == latest.to) {
                        _uiState.value = latest.withOffers { copy(isLoading = false, error = null) }
                        return@launch
                    }
                    _uiState.value = latest.copy(isLoading = true, error = null)
                    try {
                        val response = repository.refresh(latest.from, latest.to, amount)
                        val unchanged = content() ?: return@launch
                        if (unchanged.from != latest.from ||
                            unchanged.to != latest.to ||
                            unchanged.amount != latest.amount
                        ) {
                            return@launch
                        }
                        _uiState.value =
                            unchanged.withOffers {
                                copy(
                                    isLoading = false,
                                    error = null,
                                    disclaimer = response.disclaimer,
                                )
                            }
                    } catch (error: CancellationException) {
                        throw error
                    } catch (error: IOException) {
                        repository.markCachedStale("${latest.from}-${latest.to}", amount.toPlainString())
                        val unchanged = content() ?: return@launch
                        _uiState.value = unchanged.copy(isLoading = false, error = error.message)
                    }
                }
        }

        private fun content() = _uiState.value as? CompareUiState.Content

        private fun savedStateValue(
            key: String,
            value: String,
        ) {
            savedStateHandle[key] = value
        }

        private companion object {
            const val COMPARISON_DEBOUNCE_MILLIS = 300L
        }
    }

private fun validAmount(amount: String): BigDecimal? = amount.toBigDecimalOrNull()?.takeIf { it.signum() > 0 }

private typealias ContentUpdate = CompareUiState.Content.() -> CompareUiState.Content

private fun CompareUiState.Content.withOffers(transform: ContentUpdate): CompareUiState.Content {
    val updated = transform()
    val visible =
        updated.offers
            .filter { offer -> offer.name.contains(updated.search.trim(), ignoreCase = true) }
            .filter { offer -> updated.sort != CompareSort.HAS_APP || offer.hasApp }
            .sortedWith(
                compareByDescending<ExchangeOfferDto> { it.isSponsored }
                    .thenComparator { left, right ->
                        when (updated.sort) {
                            CompareSort.BEST_AMOUNT -> compareBestAmounts(left, right)
                            CompareSort.BEST_RATE -> right.rate.compareTo(left.rate)
                            CompareSort.HAS_APP -> compareBestAmounts(left, right)
                        }
                    },
            )
    return updated.copy(visibleOffers = visible)
}

private fun compareBestAmounts(
    left: ExchangeOfferDto,
    right: ExchangeOfferDto,
): Int {
    val leftAmount = left.amountReceived
    val rightAmount = right.amountReceived
    val receivedPresence = compareValues(rightAmount != null, leftAmount != null)
    return when {
        receivedPresence != 0 -> receivedPresence
        leftAmount == null || rightAmount == null -> 0
        else -> rightAmount.compareTo(leftAmount)
    }
}
