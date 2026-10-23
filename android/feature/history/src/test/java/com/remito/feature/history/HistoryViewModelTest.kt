package com.remito.feature.history

import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import com.remito.core.database.RateDao
import com.remito.core.database.RateHistoryEntity
import com.remito.core.network.RateHistoryPointDto
import com.remito.core.network.RateHistoryResponseDto
import com.remito.core.network.RatesRepository
import io.mockk.coEvery
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
import org.junit.jupiter.api.Test
import java.math.BigDecimal

@OptIn(ExperimentalCoroutinesApi::class)
class HistoryViewModelTest {
    @Test
    fun cachedHistoryIsEmittedImmediately() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val repository = mockk<RatesRepository>()
                val dao = mockk<RateDao>()
                every { dao.observeHistory("OMR-INR", "1D") } returns
                    flowOf(listOf(RateHistoryEntity("OMR-INR", "1D", "2025-01-01T00:00:00Z", "216.7")))
                coEvery { repository.refreshHistory("OMR-INR", "1D") } returns
                    RateHistoryResponseDto(
                        pair = "OMR-INR",
                        range = "1D",
                        points = listOf(RateHistoryPointDto("2025-01-01T00:00:00Z", BigDecimal("216.7"))),
                        minimum = BigDecimal("216.7"),
                        maximum = BigDecimal("216.7"),
                        open = BigDecimal("216.7"),
                        close = BigDecimal("216.7"),
                    )

                val viewModel = HistoryViewModel(repository, dao, SavedStateHandle())
                advanceUntilIdle()

                viewModel.uiState.test {
                    val state = awaitItem() as HistoryUiState.Content
                    assertEquals("216.7", state.open.toPlainString())
                }
            } finally {
                Dispatchers.resetMain()
            }
        }
}
