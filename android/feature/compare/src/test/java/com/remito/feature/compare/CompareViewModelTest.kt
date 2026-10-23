package com.remito.feature.compare

import androidx.lifecycle.SavedStateHandle
import com.remito.core.network.ExchangeComparisonResponseDto
import com.remito.core.network.ExchangeOfferDto
import com.remito.core.network.RedirectResponseDto
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.math.BigDecimal

@OptIn(ExperimentalCoroutinesApi::class)
class CompareViewModelTest {
    @Test
    fun sortsByBestAmountAndPinsSponsoredOffer() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val sponsored = offer("sponsored", amountReceived = "220", isSponsored = true)
                val bestAmount = offer("best-amount", amountReceived = "230")
                val bestRate = offer("best-rate", rate = "2.4", amountReceived = "210", isBestRate = true)
                val unknownAmount = offer("unknown-amount", amountReceived = null)
                val viewModel = viewModelWithOffers(listOf(bestRate, bestAmount, sponsored, unknownAmount))
                advanceUntilIdle()

                val state = viewModel.uiState.value as CompareUiState.Content
                assertEquals(
                    listOf("sponsored", "best-amount", "best-rate", "unknown-amount"),
                    state.visibleOffers.map(ExchangeOfferDto::id),
                )
                assertTrue(state.visibleOffers.last().isBestRate)
            } finally {
                Dispatchers.resetMain()
            }
        }

    @Test
    fun bestRateSortDoesNotRecalculateBackendBadge() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val backendBest = offer("backend-best", rate = "2.2", isBestRate = true)
                val other = offer("other", rate = "2.5", isBestRate = false)
                val viewModel = viewModelWithOffers(listOf(backendBest, other))
                advanceUntilIdle()
                viewModel.setSort(CompareSort.BEST_RATE)

                val state = viewModel.uiState.value as CompareUiState.Content
                assertEquals(listOf("other", "backend-best"), state.visibleOffers.map(ExchangeOfferDto::id))
                assertTrue(state.visibleOffers.last().isBestRate)
                assertFalse(state.visibleOffers.first().isBestRate)
            } finally {
                Dispatchers.resetMain()
            }
        }

    @Test
    fun confirmationCreatesOneRedirectAndOneStartedEvent() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val offer = offer("exchange")
                val repository = mockk<ComparisonRepository>()
                every { repository.observeCached(any(), any()) } returns flowOf(listOf(offer))
                coEvery { repository.refresh(any(), any(), any()) } returns response(listOf(offer))
                coEvery { repository.redirect(any(), any(), any(), any()) } returns
                    RedirectResponseDto(
                        destinationUrl = "remito-partner://pay",
                        fallbackUrl = "https://play.google.com/store/apps/details?id=example",
                        gatewayUrl = "https://pay.example",
                        clickId = "non-pii-click-id",
                        disclosureText = "Remito does not process payments.",
                    )
                val events = mutableListOf<RedirectEvent>()
                val viewModel = CompareViewModel(repository, RedirectAnalytics(events::add), SavedStateHandle())
                advanceUntilIdle()

                viewModel.requestRedirect(offer)
                viewModel.confirmRedirect()
                viewModel.confirmRedirect()
                advanceUntilIdle()

                coVerify(exactly = 1) { repository.redirect(offer, "OMR", "INR", BigDecimal("100")) }
                assertEquals(listOf(RedirectEvent.STARTED), events)
                viewModel.onDestinationLaunched(success = true, failureMessage = "failed")
                viewModel.onAppResumed()
                viewModel.onAppResumed()
                assertEquals(listOf(RedirectEvent.STARTED, RedirectEvent.RETURNED), events)
            } finally {
                Dispatchers.resetMain()
            }
        }

    private fun viewModelWithOffers(offers: List<ExchangeOfferDto>): CompareViewModel {
        val repository = mockk<ComparisonRepository>()
        every { repository.observeCached(any(), any()) } returns flowOf(offers)
        coEvery { repository.refresh(any(), any(), any()) } returns response(offers)
        coEvery { repository.markCachedStale(any(), any()) } returns Unit
        return CompareViewModel(repository, RedirectAnalytics { }, SavedStateHandle())
    }

    private fun response(offers: List<ExchangeOfferDto>) =
        ExchangeComparisonResponseDto(
            from = "OMR",
            to = "INR",
            amount = BigDecimal("100"),
            exchanges = offers.filterNot(ExchangeOfferDto::isSponsored),
            sponsoredSlot =
                offers
                    .filter(ExchangeOfferDto::isSponsored)
                    .takeIf(List<ExchangeOfferDto>::isNotEmpty)
                    ?.let {
                        com.remito.core.network
                            .SponsoredExchangeSlotDto("Sponsored", it)
                    },
            disclaimer = "Remito does not process payments.",
            page = 1,
            pageSize = 100,
            totalCount = offers.size,
        )

    private fun offer(
        id: String,
        rate: String = "2.3",
        amountReceived: String? = "220",
        isSponsored: Boolean = false,
        isBestRate: Boolean = false,
    ) = ExchangeOfferDto(
        id = id,
        name = id,
        logo = null,
        rate = BigDecimal(rate),
        fee = null,
        amountReceived = amountReceived?.let(::BigDecimal),
        feeDisclosure = "Fee not provided",
        lastUpdated = "2026-10-23T10:00:00Z",
        isStale = false,
        hasApp = true,
        isSponsored = isSponsored,
        isBestRate = isBestRate,
    )
}
