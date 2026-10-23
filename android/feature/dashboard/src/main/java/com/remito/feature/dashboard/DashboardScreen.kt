package com.remito.feature.dashboard

import androidx.compose.foundation.gestures.detectVerticalDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.ui.EmptyState
import com.remito.core.ui.ErrorState
import com.remito.core.ui.RateCard
import com.remito.core.ui.SkeletonLoader
import com.remito.core.ui.StaleBanner
import com.remito.core.ui.relativeTimeText
import java.math.BigDecimal
import java.time.Instant

@Composable
fun DashboardScreen(viewModel: DashboardViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val locale = LocalConfiguration.current.locales[0] ?: java.util.Locale.getDefault()

    Column(
        modifier =
            Modifier.fillMaxSize().padding(16.dp).pointerInput(viewModel) {
                var dragDistance = 0f
                detectVerticalDragGestures(
                    onVerticalDrag = { _, amount -> dragDistance += amount },
                    onDragEnd = {
                        if (dragDistance > 96f) viewModel.refresh()
                        dragDistance = 0f
                    },
                )
            },
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        DashboardHeader(viewModel)
        when (state) {
            DashboardUiState.Loading -> SkeletonLoader()
            is DashboardUiState.Content -> DashboardContent(state as DashboardUiState.Content, viewModel, locale)
            is DashboardUiState.Error ->
                ErrorState(
                    (state as DashboardUiState.Error).message
                        ?: stringResource(com.remito.core.ui.R.string.unexpected_error),
                    onRetry = viewModel::refresh,
                )
        }
    }
}

@Composable
private fun DashboardHeader(viewModel: DashboardViewModel) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(stringResource(R.string.dashboard_title), style = MaterialTheme.typography.headlineMedium)
        Button(onClick = viewModel::refresh) { Text(stringResource(R.string.refresh)) }
    }
}

@Composable
private fun DashboardContent(
    content: DashboardUiState.Content,
    viewModel: DashboardViewModel,
    locale: java.util.Locale,
) {
    OutlinedTextField(
        value = content.query,
        onValueChange = viewModel::setQuery,
        modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp),
        label = { Text(stringResource(R.string.search_currencies)) },
        singleLine = true,
    )
    if (content.stale) StaleBanner()
    when {
        content.rates.isEmpty() && content.error != null -> ErrorState(content.error, onRetry = viewModel::refresh)
        content.rates.isEmpty() -> EmptyState(stringResource(R.string.no_rates))
        else -> DashboardRateList(content, viewModel, locale)
    }
}

@Composable
private fun DashboardRateList(
    content: DashboardUiState.Content,
    viewModel: DashboardViewModel,
    locale: java.util.Locale,
) {
    if (content.error != null) {
        Text(content.error, color = MaterialTheme.colorScheme.error)
    }

    LazyColumn(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        items(content.rates, key = { it.pair }) { item ->
            val timestamp =
                try {
                    Instant.parse(item.lastUpdated)
                } catch (_: java.time.format.DateTimeParseException) {
                    null
                }
            RateCard(
                currency = item.pair.substringAfter('/'),
                rate = item.rate,
                changePercent = BigDecimal(item.change24HoursPercent),
                lastUpdated =
                    stringResource(
                        R.string.updated_time,
                        timestamp?.let { relativeTimeText(it, locale) } ?: item.lastUpdated,
                    ),
                isFavourite = item.isFavourite,
                onFavouriteClick = { viewModel.toggleFavourite(item.pair) },
            )
        }
    }
}
