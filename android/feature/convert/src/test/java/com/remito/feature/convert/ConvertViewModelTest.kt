package com.remito.feature.convert

import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import com.remito.core.network.ConversionQuoteDto
import com.remito.core.network.RatesRepository
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.io.IOException
import java.math.BigDecimal

@OptIn(ExperimentalCoroutinesApi::class)
class ConvertViewModelTest {
    @Test
    fun offlineConversionUsesCachedQuote() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val repository = mockk<RatesRepository>()
                val cached =
                    ConversionQuoteDto(
                        BigDecimal.ONE,
                        "OMR",
                        "INR",
                        BigDecimal("216.7"),
                        BigDecimal("216.7"),
                        "2025-01-01T00:00:00Z",
                        true,
                    )
                coEvery { repository.convert(any(), any(), any()) } throws IOException("Offline")
                coEvery { repository.cachedConversionQuote(any(), any(), any()) } returns cached

                val viewModel = ConvertViewModel(repository, SavedStateHandle(mapOf("amount" to "1")))
                advanceUntilIdle()

                viewModel.uiState.test {
                    val state = awaitItem() as ConvertUiState.Content
                    assertTrue(state.isOffline)
                    assertTrue(state.result?.isStale == true)
                }
            } finally {
                Dispatchers.resetMain()
            }
        }
}
