package com.remito.feature.alerts

import androidx.lifecycle.SavedStateHandle
import com.remito.core.database.AlertEntity
import com.remito.core.network.AlertDirection
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
import org.junit.jupiter.api.Test

@OptIn(ExperimentalCoroutinesApi::class)
class AlertsViewModelTest {
    @Test
    fun invalidTargetIsRejectedWithoutCreatingAnAlert() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val repository = mockk<AlertsRepository>()
                every { repository.observeAlerts() } returns flowOf(emptyList<AlertEntity>())
                coEvery { repository.refresh() } returns emptyList()
                val viewModel = AlertsViewModel(repository, SavedStateHandle())
                advanceUntilIdle()

                viewModel.setTarget("0")
                viewModel.create()
                advanceUntilIdle()

                assertEquals("Enter a valid pair and a target greater than zero.", viewModel.uiState.value.error)
                coVerify(exactly = 0) { repository.create(any(), any(), any(), any()) }
            } finally {
                Dispatchers.resetMain()
            }
        }

    @Test
    fun createsAboveAlertWithRepeatSelection() =
        runTest {
            Dispatchers.setMain(StandardTestDispatcher(testScheduler))
            try {
                val repository = mockk<AlertsRepository>()
                every { repository.observeAlerts() } returns flowOf(emptyList())
                coEvery { repository.refresh() } returns emptyList()
                coEvery { repository.create(any(), any(), any(), any()) } returns Unit
                val viewModel = AlertsViewModel(repository, SavedStateHandle())
                advanceUntilIdle()
                viewModel.setTarget("220.5")
                viewModel.setOneTime(false)
                viewModel.create()
                advanceUntilIdle()

                coVerify(exactly = 1) {
                    repository.create("OMR/INR", AlertDirection.ABOVE, match { it.toPlainString() == "220.5" }, false)
                }
            } finally {
                Dispatchers.resetMain()
            }
        }
}
