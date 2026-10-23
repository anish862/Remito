package com.remito.feature.dashboard

import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import com.remito.core.common.UserSettings
import com.remito.core.database.CachedRateEntity
import com.remito.core.network.EngagementRepository
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
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class DashboardViewModelTest {
    @Test
    fun cachedRatesRemainAvailableAndMarkedStaleAfterRefreshFailure() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val repository = mockk<RatesRepository>()
                val engagementRepository = mockk<EngagementRepository>()
                val userSettings = mockk<UserSettings>()
                every { userSettings.defaultCorridor() } returns "OMR/INR"
                every { repository.observeRates("OMR") } returns
                    flowOf(listOf(CachedRateEntity("OMR/INR", "216.7", "0.4", "2025-01-01T00:00:00Z", false)))
                every { engagementRepository.observeFavourites() } returns flowOf(emptyList())
                coEvery { engagementRepository.refreshFavourites() } returns emptyList()
                coEvery { repository.refreshRates(any(), any()) } throws IOException("Offline")
                coEvery { repository.markCachedRatesStale() } returns Unit

                val viewModel = DashboardViewModel(repository, engagementRepository, userSettings, SavedStateHandle())
                advanceUntilIdle()

                viewModel.uiState.test {
                    val state = awaitItem() as DashboardUiState.Content
                    assertEquals("OMR/INR", state.rates.single().pair)
                    assertTrue(state.stale)
                    assertEquals("Offline", state.error)
                }
            } finally {
                Dispatchers.resetMain()
            }
        }
}
