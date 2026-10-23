package com.remito.feature.alerts

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.network.AlertDirection
import com.remito.core.ui.EmptyState
import com.remito.core.ui.ErrorState
import com.remito.core.ui.SkeletonLoader

@Composable
fun AlertsScreen(viewModel: AlertsViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(stringResource(R.string.alerts_title), style = MaterialTheme.typography.headlineMedium)
        AlertForm(state, viewModel)
        state.error?.let { message ->
            ErrorState(message, onRetry = viewModel::refresh)
        }
        AlertList(state, viewModel)
    }
}

@Composable
private fun AlertForm(
    state: AlertsUiState,
    viewModel: AlertsViewModel,
) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        OutlinedTextField(
            value = state.pair,
            onValueChange = viewModel::setPair,
            label = { Text(stringResource(R.string.alert_pair)) },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(
                selected = state.direction == AlertDirection.ABOVE,
                onClick = { viewModel.setDirection(AlertDirection.ABOVE) },
                label = { Text(stringResource(R.string.alert_above)) },
            )
            FilterChip(
                selected = state.direction == AlertDirection.BELOW,
                onClick = { viewModel.setDirection(AlertDirection.BELOW) },
                label = { Text(stringResource(R.string.alert_below)) },
            )
        }
        OutlinedTextField(
            value = state.target,
            onValueChange = { value ->
                if (value.isEmpty() || value.matches(Regex("^\\d*(\\.\\d*)?$"))) viewModel.setTarget(value)
            },
            label = { Text(stringResource(R.string.alert_target)) },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(stringResource(R.string.alert_repeat))
            Switch(checked = !state.isOneTime, onCheckedChange = { viewModel.setOneTime(!it) })
        }
        Button(onClick = viewModel::create, enabled = !state.isSaving, modifier = Modifier.heightIn(min = 48.dp)) {
            Text(stringResource(R.string.alert_create))
        }
    }
}

@Composable
private fun AlertList(
    state: AlertsUiState,
    viewModel: AlertsViewModel,
) {
    when {
        state.isLoading -> SkeletonLoader()
        state.alerts.isEmpty() -> EmptyState(stringResource(R.string.alerts_empty))
        else ->
            LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                items(state.alerts, key = { it.id }) { alert ->
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Row(
                            modifier = Modifier.fillMaxWidth().padding(12.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text("${alert.pair} ${alert.direction} ${alert.target}")
                                Text(
                                    if (alert.isOneTime) {
                                        stringResource(
                                            R.string.alert_once,
                                        )
                                    } else {
                                        stringResource(R.string.alert_repeats)
                                    },
                                    style = MaterialTheme.typography.bodySmall,
                                )
                            }
                            Switch(
                                checked = alert.isActive,
                                onCheckedChange = { viewModel.setActive(alert, it) },
                            )
                            TextButton(onClick = { viewModel.delete(alert) }) {
                                Text(stringResource(R.string.alert_delete))
                            }
                        }
                    }
                }
            }
    }
}
