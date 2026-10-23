package com.remito.feature.alerts

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.database.AlertEntity
import com.remito.core.network.AlertDirection
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.IOException
import java.math.BigDecimal
import javax.inject.Inject

data class AlertsUiState(
    val alerts: List<AlertEntity> = emptyList(),
    val pair: String = "OMR/INR",
    val direction: AlertDirection = AlertDirection.ABOVE,
    val target: String = "",
    val isOneTime: Boolean = false,
    val isLoading: Boolean = true,
    val isSaving: Boolean = false,
    val error: String? = null,
)

@HiltViewModel
class AlertsViewModel
    @Inject
    constructor(
        private val repository: AlertsRepository,
        savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private val _uiState =
            MutableStateFlow(
                AlertsUiState(
                    pair = savedStateHandle["pair"] ?: "OMR/INR",
                    target = savedStateHandle["target"] ?: "",
                ),
            )
        val uiState: StateFlow<AlertsUiState> = _uiState

        init {
            viewModelScope.launch {
                repository.observeAlerts().collect { alerts ->
                    _uiState.value = _uiState.value.copy(alerts = alerts, isLoading = false)
                }
            }
            refresh()
        }

        fun setPair(pair: String) {
            _uiState.value = _uiState.value.copy(pair = pair, error = null)
        }

        fun setDirection(direction: AlertDirection) {
            _uiState.value = _uiState.value.copy(direction = direction, error = null)
        }

        fun setTarget(target: String) {
            _uiState.value = _uiState.value.copy(target = target, error = null)
        }

        fun setOneTime(isOneTime: Boolean) {
            _uiState.value = _uiState.value.copy(isOneTime = isOneTime)
        }

        fun create() {
            val current = _uiState.value
            val target = current.target.toBigDecimalOrNull()
            if (current.pair.count { it == '/' } != 1 || target == null || target <= BigDecimal.ZERO) {
                _uiState.value = current.copy(error = "Enter a valid pair and a target greater than zero.")
                return
            }
            viewModelScope.launch {
                updateSaving(true)
                try {
                    repository.create(current.pair, current.direction, target, current.isOneTime)
                    _uiState.value = _uiState.value.copy(target = "", error = null)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value = _uiState.value.copy(error = error.message ?: "Could not save alert.")
                } finally {
                    updateSaving(false)
                }
            }
        }

        fun setActive(
            alert: AlertEntity,
            active: Boolean,
        ) {
            viewModelScope.launch {
                try {
                    repository.setActive(alert, active)
                    clearError()
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value = _uiState.value.copy(error = error.message ?: "Could not update alert.")
                }
            }
        }

        fun delete(alert: AlertEntity) {
            viewModelScope.launch {
                try {
                    repository.delete(alert)
                    clearError()
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value = _uiState.value.copy(error = error.message ?: "Could not delete alert.")
                }
            }
        }

        fun refresh() {
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(isLoading = _uiState.value.alerts.isEmpty(), error = null)
                try {
                    repository.refresh()
                    _uiState.value = _uiState.value.copy(isLoading = false)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message ?: "Could not load alerts.",
                        )
                }
            }
        }

        private fun updateSaving(value: Boolean) {
            _uiState.value = _uiState.value.copy(isSaving = value)
        }

        private fun clearError() {
            _uiState.value = _uiState.value.copy(error = null)
        }
    }
